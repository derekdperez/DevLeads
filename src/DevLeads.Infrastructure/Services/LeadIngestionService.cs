using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Scoring;
using DevLeads.Core.Skills;
using DevLeads.Core.Templates;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// The two-step triage funnel: heuristic pre-filter, then (for survivors) a single structured
/// AI call, followed by weighted scoring and optional draft generation. This is the pipeline
/// heart described in the design document.
/// </summary>
public sealed class LeadIngestionService
{
    private readonly DevLeadsDbContext _db;
    private readonly HeuristicPreFilter _preFilter;
    private readonly AiTriageRouter _ai;
    private readonly AuditService _audit;
    private readonly ILogger<LeadIngestionService> _log;

    public LeadIngestionService(DevLeadsDbContext db, HeuristicPreFilter preFilter, AiTriageRouter ai,
        AuditService audit, ILogger<LeadIngestionService> log)
    {
        _db = db;
        _preFilter = preFilter;
        _ai = ai;
        _audit = audit;
        _log = log;
    }

    /// <summary>
    /// Runs a discovered item through the full pipeline. Returns null if a duplicate.
    /// <paramref name="precomputedTriage"/> carries this item's result from a batched AI
    /// call so no further per-item AI call is spent.
    /// </summary>
    public async Task<Opportunity?> IngestAsync(RawSourceItem item, SourceConfig source, CancellationToken ct,
        AiTriageResponse? precomputedTriage = null)
    {
        var sourceUrl = NormalizeSourceUrl(item.Url)
            ?? throw new InvalidOperationException($"Source item '{item.SourceKey}/{item.ExternalId}' has no valid source URL.");
        item.Url = sourceUrl;

        var existing = await _db.RawSourceItems
            .FirstOrDefaultAsync(r => r.SourceKey == item.SourceKey && r.ExternalId == item.ExternalId, ct);
        if (existing is not null)
        {
            existing.Title = item.Title;
            existing.BodyText = item.BodyText;
            existing.Url = item.Url;
            existing.RawJson = item.RawJson;
            existing.ContentHash = item.ContentHash;
            existing.PostedAt = item.PostedAt;
            existing.FetchedAt = item.FetchedAt;

            if (existing.OpportunityId is { } oppId)
            {
                var opp = await _db.Opportunities.FindAsync(new object[] { oppId }, ct);
                if (opp is not null)
                {
                    opp.LastSeenAt = item.PostedAt > opp.LastSeenAt ? item.PostedAt : DateTimeOffset.UtcNow;
                    opp.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            await _db.SaveChangesAsync(ct);
            return null; // already seen
        }

        // Cross-source duplicate: the same post can arrive via two feeds or two connectors
        // (an Opire bounty and a GitHub bounty search hit the same issue; a forum topic and
        // its replies share one canonical URL). One lead per canonical URL.
        var urlDupe = await _db.Opportunities.FirstOrDefaultAsync(o => o.SourceUrl == sourceUrl, ct);
        if (urlDupe is not null)
        {
            _db.RawSourceItems.Add(item);
            urlDupe.LastSeenAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct); // keep the raw item so per-source dedup holds
            return null;
        }

        var nearDupe = await FindNearDuplicateOpportunityAsync(item, sourceUrl, ct);
        if (nearDupe is not null)
        {
            _db.RawSourceItems.Add(item);
            nearDupe.LastSeenAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return null;
        }

        _db.RawSourceItems.Add(item);

        var settings = await GetSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var opportunity = new Opportunity
        {
            Title = item.Title,
            Summary = Truncate(item.BodyText, 240),
            CampaignId = source.CampaignId,
            SourceKey = item.SourceKey,
            SourceUrl = sourceUrl,
            AuthorName = item.AuthorName,
            AuthorProfileUrl = item.AuthorProfileUrl,
            PostedAt = item.PostedAt,
            FirstSeenAt = now,
            LastSeenAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Status = OpportunityStatus.New
        };

        // Step 1: heuristic pre-filter, scoped to the source's own query packs so one
        // campaign's trigger vocabulary never qualifies another campaign's items.
        var pre = _preFilter.Analyze(item, PackNames(source));
        ApplyPreFilter(opportunity, pre);

        if (!pre.ShouldAnalyzeWithAi || (double)pre.HeuristicScore < source.MinPreFilterScore)
        {
            // Non-actionable: keep only the raw item (so dedup prevents re-ingesting the
            // same post) but never create a lead row — the lead list is for payable work.
            await _db.SaveChangesAsync(ct);
            return null;
        }

        _db.Opportunities.Add(opportunity);
        item.Opportunity = opportunity;
        await _db.SaveChangesAsync(ct);
        item.OpportunityId = opportunity.Id;

        // Step 2: single-pass AI triage + scoring + optional draft.
        await RunTriageScoreAndDraftAsync(opportunity, pre, settings, source, ct, precomputedTriage);

        // Automated discovery keeps only payable leads. If triage decided this post is
        // irrelevant, unpaid, or unsafe to contact, drop the lead row entirely — the raw
        // item remains (detached) so dedup never re-ingests it.
        if (opportunity.Status is OpportunityStatus.Rejected
            or OpportunityStatus.PreFilteredRejected
            or OpportunityStatus.DoNotContact)
        {
            item.OpportunityId = null;
            item.Opportunity = null;
            _db.Opportunities.Remove(opportunity);
            await _db.SaveChangesAsync(ct);
            return null;
        }

        await _db.SaveChangesAsync(ct);
        return opportunity;
    }

    /// <summary>
    /// Records a fetched item as seen without creating an opportunity. Used when a batch
    /// shortlist decides the item is not worth a full AI triage call.
    /// </summary>
    public async Task<bool> RecordRawOnlyAsync(RawSourceItem item, CancellationToken ct)
    {
        var sourceUrl = NormalizeSourceUrl(item.Url)
            ?? throw new InvalidOperationException($"Source item '{item.SourceKey}/{item.ExternalId}' has no valid source URL.");
        item.Url = sourceUrl;

        var existing = await _db.RawSourceItems
            .FirstOrDefaultAsync(r => r.SourceKey == item.SourceKey && r.ExternalId == item.ExternalId, ct);
        if (existing is not null)
        {
            existing.Title = item.Title;
            existing.BodyText = item.BodyText;
            existing.Url = item.Url;
            existing.RawJson = item.RawJson;
            existing.ContentHash = item.ContentHash;
            existing.PostedAt = item.PostedAt;
            existing.FetchedAt = item.FetchedAt;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        _db.RawSourceItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Manual lead entry that still runs the pre-filter, AI triage, and scoring.</summary>
    public async Task<Opportunity> CreateManualAsync(string title, string body, string sourceUrl,
        string? author, string? authorUrl, CancellationToken ct, long? campaignId = null)
    {
        var normalizedSourceUrl = NormalizeSourceUrl(sourceUrl)
            ?? throw new ArgumentException("A valid original source URL is required for every opportunity.", nameof(sourceUrl));
        var settings = await GetSettingsAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var item = new RawSourceItem
        {
            SourceKey = "manual",
            ExternalId = Guid.NewGuid().ToString("N"),
            Title = title,
            BodyText = body,
            Url = normalizedSourceUrl,
            AuthorName = author,
            AuthorProfileUrl = authorUrl,
            PostedAt = now,
            FetchedAt = now,
            ContentHash = Connectors.ConnectorSupport.ContentHash("manual", Guid.NewGuid().ToString(), title),
            RawJson = "{}"
        };
        _db.RawSourceItems.Add(item);

        var opportunity = new Opportunity
        {
            Title = title,
            Summary = Truncate(body, 240),
            CampaignId = campaignId,
            SourceKey = "manual",
            SourceUrl = normalizedSourceUrl,
            AuthorName = author,
            AuthorProfileUrl = authorUrl,
            PostedAt = now, FirstSeenAt = now, LastSeenAt = now, CreatedAt = now, UpdatedAt = now,
            Status = OpportunityStatus.New
        };

        var pre = _preFilter.Analyze(item);
        ApplyPreFilter(opportunity, pre);
        _db.Opportunities.Add(opportunity);
        item.Opportunity = opportunity;
        await _db.SaveChangesAsync(ct);
        item.OpportunityId = opportunity.Id;

        // Manual leads always get an AI pass (operator asked for it explicitly).
        await RunTriageScoreAndDraftAsync(opportunity, pre, settings, source: null, ct);
        await _db.SaveChangesAsync(ct);
        return opportunity;
    }

    /// <summary>Re-runs triage + scoring for an existing opportunity (used by the "rerun" endpoint).</summary>
    public async Task RerunAsync(long opportunityId, CancellationToken ct)
    {
        var opp = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, ct)
                  ?? throw new InvalidOperationException("Opportunity not found");
        var settings = await GetSettingsAsync(ct);
        var pre = new PreFilterResult
        {
            ShouldAnalyzeWithAi = true,
            HeuristicScore = (decimal)opp.HeuristicScore,
            MatchedTerms = DeserializeList(opp.MatchedTermsJson)
        };
        var source = await _db.SourceConfigs.AsNoTracking().FirstOrDefaultAsync(s => s.SourceKey == opp.SourceKey, ct);
        await RunTriageScoreAndDraftAsync(opp, pre, settings, source, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task RunTriageScoreAndDraftAsync(Opportunity opp, PreFilterResult pre, OperatorSettings settings,
        SourceConfig? source, CancellationToken ct, AiTriageResponse? precomputed = null)
    {
        opp.AiJobStatus = AiJobStatus.Running;
        var triageSettings = ResolveTriageSettings(settings, source);
        if (precomputed is not { Succeeded: true, Result: not null })
            precomputed = null; // a failed batch result never blocks the normal path

        // Hard budget guard: once the hourly AI call cap is reached, fall back to the
        // zero-cost heuristic provider instead of dropping or delaying the lead.
        // A precomputed batch result is already paid for — no budget check needed.
        if (precomputed is null &&
            !triageSettings.AiProvider.Equals("Heuristic", StringComparison.OrdinalIgnoreCase) &&
            await IsOverAiBudgetAsync(settings, ct))
        {
            _log.LogInformation("Hourly AI call budget ({Max}) reached — triaging opportunity {Id} heuristically.",
                settings.MaxAiCallsPerHour, opp.Id);
            triageSettings = CloneWithProvider(triageSettings, "Heuristic");
        }
        var run = new AiTriageRun
        {
            OpportunityId = opp.Id,
            PromptVersion = triageSettings.PromptVersion,
            Status = AiJobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        _db.AiTriageRuns.Add(run);

        var skills = await GetSkillsAsync(ct);
        var body = await GetBodyAsync(opp, ct);

        // An amount the poster explicitly stated ("Reward: $15", "[Bounty $250]") is fact —
        // it overrides the category-based fee suggestion and is displayed as the offer.
        var offered = OfferedCompensation.Extract(opp.Title, body);
        if (offered is { } o)
        {
            opp.EstimatedFeeMin = o.Min;
            opp.EstimatedFeeMax = o.Max;
            opp.FeeIsEstimate = false;
        }
        var request = new AiTriageRequest
        {
            Title = opp.Title,
            Body = body,
            SourceKey = opp.SourceKey,
            PostedAt = opp.PostedAt,
            MatchedTerms = pre.MatchedTerms,
            HeuristicScore = pre.HeuristicScore,
            OperatorSkills = skills.Count == 0 ? "" : SkillMatcher.PromptSummary(skills),
            CampaignObjective = await GetCampaignObjectiveAsync(opp.CampaignId, ct)
        };

        AiTriageResult? aiResult = null;
        try
        {
            var resp = precomputed ?? await _ai.TriageAsync(request, triageSettings, ct);
            run.Provider = resp.Provider;
            run.Model = resp.Model;
            run.RequestJson = resp.RequestJson;
            run.ResponseJson = resp.ResponseJson;
            run.CompletedAt = DateTimeOffset.UtcNow;

            if (resp.Succeeded && resp.Result is not null)
            {
                aiResult = resp.Result;
                run.Status = AiJobStatus.Succeeded;
                opp.AiJobStatus = AiJobStatus.Succeeded;
            }
            else
            {
                run.Status = AiJobStatus.FailedFinal;
                run.ErrorMessage = resp.ErrorMessage;
                // Never drop a high-heuristic-score item just because AI failed.
                opp.AiJobStatus = AiJobStatus.NeedsManualReview;
                opp.Status = OpportunityStatus.NeedsReview;
                opp.RejectionReason = "AI triage failed: " + resp.ErrorMessage;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            run.Status = AiJobStatus.FailedFinal;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
            opp.AiJobStatus = AiJobStatus.NeedsManualReview;
            opp.Status = OpportunityStatus.NeedsReview;
            _log.LogError(ex, "Triage failed for opportunity {Id}", opp.Id);
        }

        if (aiResult is not null)
            ApplyAiResult(opp, aiResult);

        if (offered is { } stated)
        {
            // Re-assert after ApplyAiResult: the stated amount always wins the suggestion.
            opp.EstimatedFeeMin = stated.Min;
            opp.EstimatedFeeMax = stated.Max;
            opp.FeeIsEstimate = false;
        }

        // Scoring blends whatever signals we have. Skill fit matches against the post
        // text plus whatever stack the AI detected.
        var redFlag = RedFlagDetector.Scan(opp.Title, body);
        var matchText = $"{opp.Title}\n{body}\n{string.Join(' ', aiResult?.DetectedStack ?? new List<string>())}";
        var competitionText = $"{opp.Title}\n{body}";
        var skillMatches = skills.Count == 0 ? null : SkillMatcher.Match(matchText, skills);
        var foreignStacks = skills.Count == 0
            ? new List<string>()
            : SkillMatcher.ForeignStackDemands(matchText, skills);
        var score = OpportunityScorer.Score(new ScoringInput
        {
            Ai = aiResult,
            PreFilter = pre,
            SourceKey = opp.SourceKey,
            PostedAt = opp.PostedAt,
            RedFlagged = redFlag.IsRedFlagged,
            HasContact = !string.IsNullOrWhiteSpace(opp.AuthorProfileUrl),
            LanguageCode = aiResult?.LanguageCode ?? "en",
            SkillMatches = skillMatches,
            OfferedAmount = offered?.Max,
            ClaimedByOthers = LeadQualityRules.IsAlreadyClaimed(competitionText),
            CompetingResponses = LeadQualityRules.CompetingResponseCount(competitionText),
            ForeignStackDemands = foreignStacks
        }, DateTimeOffset.UtcNow);
        ApplyScore(opp, score);

        DecideStatusAndDraft(opp, aiResult, redFlag, settings, source, body, skillMatches, foreignStacks);
        ApplyEnglishTranslationIfRetained(opp, aiResult);
        opp.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void DecideStatusAndDraft(Opportunity opp, AiTriageResult? ai, RedFlagResult redFlag,
        OperatorSettings settings, SourceConfig? source, string body,
        IReadOnlyList<SkillMatch>? skillMatches, IReadOnlyList<string> foreignStacks)
    {
        if (redFlag.IsRedFlagged)
        {
            opp.Status = OpportunityStatus.DoNotContact;
            opp.OutreachRecommendation = OutreachRecommendation.DoNotContact;
            opp.RejectionReason = "Red flag: " + string.Join("; ", redFlag.Reasons);
            _audit.Record("Opportunity", opp.Id, "RedFlag", opp.RejectionReason);
            return;
        }

        // Wrong stack: the post demands work in a stack outside the operator's profile
        // and never touches the operator's own stack — unactionable regardless of pay.
        // Manual entries are exempt (the operator chose to add them).
        if (opp.SourceKey != "manual" && foreignStacks.Count > 0 &&
            !(skillMatches is { } sm && SkillMatcher.HasStackIdentityMatch(sm)))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = $"Requires {string.Join("/", foreignStacks.Take(4))} — outside the operator's stack.";
            _audit.Record("Opportunity", opp.Id, "ForeignStackRejected", opp.RejectionReason);
            return;
        }

        if (ai is null)
            return; // status already NeedsReview from the failure path

        var text = $"{opp.Title}\n{opp.Summary}\n{body}";
        if (LeadQualityRules.IsReplyFeedItem(opp.Title))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = "Feed item is a reply into an existing thread, not a new request.";
            _audit.Record("Opportunity", opp.Id, "ReplyItemRejected", opp.RejectionReason);
            return;
        }

        if (LeadQualityRules.IsAiAgentTaskPost(opp.Title, text))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = "Task published for autonomous AI agents, not hirable human work.";
            _audit.Record("Opportunity", opp.Id, "AgentTaskRejected", opp.RejectionReason);
            return;
        }

        if (LeadQualityRules.IsPromotionalAnnouncement(text))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = "Promotional product/launch announcement, not a request for help.";
            _audit.Record("Opportunity", opp.Id, "PromotionalRejected", opp.RejectionReason);
            return;
        }

        if (LeadQualityRules.IsResolvedOrClosedRequest(text))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = "Thread is already solved or has an accepted answer.";
            _audit.Record("Opportunity", opp.Id, "ResolvedThreadRejected", opp.RejectionReason);
            return;
        }

        if (LeadQualityRules.IsNonHirableVendorSupportRequest(text))
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = "Vendor account/billing support request, not hirable third-party repair work.";
            _audit.Record("Opportunity", opp.Id, "VendorSupportRejected", opp.RejectionReason);
            return;
        }

        var rec = MapRecommendation(ai.OutreachRecommendation);
        opp.OutreachRecommendation = rec;

        if (source is not null &&
            rec is not OutreachRecommendation.DoNotContact and not OutreachRecommendation.Ignore &&
            opp.Score < source.MinOpportunityScore)
        {
            opp.Status = OpportunityStatus.Rejected;
            opp.OutreachRecommendation = OutreachRecommendation.Ignore;
            opp.RejectionReason = $"Below source opportunity threshold ({opp.Score:0.#} < {source.MinOpportunityScore:0.#}).";
            _audit.Record("Opportunity", opp.Id, "SourceQualityRejected", opp.RejectionReason);
            return;
        }

        switch (rec)
        {
            case OutreachRecommendation.DoNotContact:
                opp.Status = OpportunityStatus.DoNotContact;
                return;
            case OutreachRecommendation.Ignore:
                opp.Status = OpportunityStatus.Rejected;
                opp.RejectionReason = ai.RejectReason ?? "Not relevant.";
                return;
            case OutreachRecommendation.Watch:
                opp.Status = OpportunityStatus.FollowUpLater;
                return;
            case OutreachRecommendation.ManualReview:
                opp.Status = OpportunityStatus.NeedsReview;
                return;
        }

        // Draft Reply: generate a draft only if score + confidence thresholds are met.
        var draftThreshold = source is not null && source.DraftThreshold > 0
            ? source.DraftThreshold
            : settings.DraftScoreThreshold;
        var alertThreshold = source is not null && source.AlertThreshold > 0
            ? source.AlertThreshold
            : settings.AlertScoreThreshold;
        bool meetsThresholds = opp.Score >= draftThreshold &&
                               (double)ai.AiConfidence >= settings.MinAiConfidenceForDraft;
        if (meetsThresholds)
        {
            CreateDraft(opp, ai, settings);
            opp.Status = OpportunityStatus.DraftReady;
            opp.AutoEligible = settings.GlobalAutoModeEnabled &&
                               (source?.AutoModeEligible ?? false) &&
                               (double)ai.AiConfidence >= settings.RequireApprovalBelowConfidence &&
                               opp.Score >= alertThreshold;
        }
        else
        {
            opp.Status = OpportunityStatus.NeedsReview;
        }
    }

    private void CreateDraft(Opportunity opp, AiTriageResult ai, OperatorSettings settings)
    {
        // High-scoring leads are queued for batched AI generation instead of getting a
        // template mad-lib: real reply text is written from the original post the next
        // time the generation runs (hourly, or on demand from the approval queue) — one
        // model call covers the whole queue.
        _db.OutreachAttempts.Add(new OutreachAttempt
        {
            OpportunityId = opp.Id,
            Channel = OutreachChannel.ManualCopy,
            Mode = settings.DefaultOutreachMode,
            TemplateKey = "ai_batch_v1",
            Body = OutreachService.QueuedPlaceholder,
            Status = OutreachStatus.QueuedForGeneration,
            RequiresApproval = settings.DefaultOutreachMode != OutreachMode.Auto,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _audit.Record("Opportunity", opp.Id, "ResponseQueued", "High-score lead queued for batched AI response generation");
    }

    // ----- mapping helpers -----

    private static void ApplyPreFilter(Opportunity opp, PreFilterResult pre)
    {
        opp.HeuristicScore = (double)pre.HeuristicScore;
        opp.MatchedTermsJson = JsonSerializer.Serialize(pre.MatchedTerms);
        opp.PreFilterRejectReason = pre.RejectReason;
    }

    private static void ApplyAiResult(Opportunity opp, AiTriageResult ai)
    {
        // Models occasionally emit null where the schema says string — never let that
        // reach the NOT NULL columns.
        opp.LanguageCode = string.IsNullOrWhiteSpace(ai.LanguageCode) ? "en" : ai.LanguageCode;
        opp.ProblemType = ai.ProblemCategory ?? "";
        opp.PaymentIntent = ai.PaymentIntent ?? "";
        opp.AssistanceRequested = ai.AssistanceRequested;
        opp.DetectedStackJson = JsonSerializer.Serialize(ai.DetectedStack ?? new List<string>());
        opp.EstimatedCause = ai.EstimatedCause ?? "";
        opp.SuggestedFirstStep = ai.FirstDiagnosticStep ?? "";
        opp.EstimatedFixMinutesMin = ai.EstimatedFixMinutesMin;
        opp.EstimatedFixMinutesMax = ai.EstimatedFixMinutesMax;
        opp.AiConfidence = (double)ai.AiConfidence;
        var (feeMin, feeMax) = PricingTiers.SuggestFor(ai.ProblemCategory);
        opp.EstimatedFeeMin = feeMin;
        opp.EstimatedFeeMax = feeMax;
        if (!string.IsNullOrWhiteSpace(ai.EstimatedCause))
            opp.Summary = ai.ProblemCategory + " — " + Truncate(ai.EstimatedCause, 200);
    }

    private void ApplyEnglishTranslationIfRetained(Opportunity opp, AiTriageResult? ai)
    {
        if (ai is null || !OpportunityScorer.IsNonEnglish(ai.LanguageCode) ||
            opp.Status is OpportunityStatus.Rejected or OpportunityStatus.PreFilteredRejected or OpportunityStatus.DoNotContact)
            return;

        if (string.IsNullOrWhiteSpace(ai.EnglishTitle) || string.IsNullOrWhiteSpace(ai.EnglishBody))
        {
            _audit.Record("Opportunity", opp.Id, "TranslationMissing",
                $"Detected {ai.LanguageCode}, but the triage provider returned no complete English translation.");
            return;
        }

        opp.Title = ai.EnglishTitle.Trim();
        opp.TranslatedBody = ai.EnglishBody.Trim();
        _audit.Record("Opportunity", opp.Id, "TranslatedToEnglish",
            $"Original language {ai.LanguageCode}; English title and body stored for review.");
    }

    private static void ApplyScore(Opportunity opp, ScoreBreakdown s)
    {
        opp.Score = s.Total;
        opp.UrgencyScore = s.UrgencyScore;
        opp.StackFitScore = s.StackFitScore;
        opp.BusinessValueScore = s.BusinessValueScore;
        opp.ReachabilityScore = s.ReachabilityScore;
        opp.CompetitionScore = s.CompetitionScore;
        opp.TrustScore = s.TrustScore;
        opp.LanguagePenalty = s.LanguagePenalty;
        opp.Priority = s.Priority;
    }

    public static OutreachRecommendation MapRecommendation(string value) => value switch
    {
        "Ignore" => OutreachRecommendation.Ignore,
        "Watch" => OutreachRecommendation.Watch,
        "Draft Reply" => OutreachRecommendation.DraftReply,
        "Do Not Contact" => OutreachRecommendation.DoNotContact,
        _ => OutreachRecommendation.ManualReview
    };

    private async Task<OperatorSettings> GetSettingsAsync(CancellationToken ct) =>
        await _db.OperatorSettings.FirstOrDefaultAsync(ct) ?? new OperatorSettings();

    private List<Skill>? _skillsCache;

    private async Task<List<Skill>> GetSkillsAsync(CancellationToken ct) =>
        _skillsCache ??= await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);

    private readonly Dictionary<long, string> _campaignObjectiveCache = new();

    private async Task<string> GetCampaignObjectiveAsync(long? campaignId, CancellationToken ct)
    {
        if (campaignId is not { } id) return "";
        if (_campaignObjectiveCache.TryGetValue(id, out var cached)) return cached;
        var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return _campaignObjectiveCache[id] = campaign?.Objective ?? "";
    }

    private static string[] PackNames(SourceConfig source) =>
        source.QueryPacksCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static OperatorSettings ResolveTriageSettings(OperatorSettings settings, SourceConfig? source)
    {
        // Start from the Triage feature's configured provider/model, then let a source's
        // explicit triageProvider parameter override the provider for that source.
        var baseSettings = settings.WithAiFor(AiFeature.Triage);
        var provider = GetSourceParameter(source, "triageProvider");
        if (string.IsNullOrWhiteSpace(provider) ||
            provider.Equals(baseSettings.AiProvider, StringComparison.OrdinalIgnoreCase))
        {
            return baseSettings;
        }

        return CloneWithProvider(baseSettings, provider);
    }

    private static OperatorSettings CloneWithProvider(OperatorSettings settings, string provider)
    {
        // A provider switch takes that provider's default model unless the current model
        // already belongs to it (heuristic ignores the model entirely).
        var model = provider.Equals(settings.AiProvider, StringComparison.OrdinalIgnoreCase)
            ? settings.AiModel
            : OperatorSettings.DefaultModelFor(provider);
        return new()
        {
            AiProvider = provider,
            AiModel = model,
            AiRetryCount = settings.AiRetryCount,
            AiTimeoutSeconds = settings.AiTimeoutSeconds,
            OpenCodeCliPath = settings.OpenCodeCliPath,
            CodexCliPath = settings.CodexCliPath,
            PromptVersion = settings.PromptVersion
        };
    }

    /// <summary>
    /// True when the count of real (non-heuristic) AI calls in the last hour has hit the
    /// cap. Batched runs record one row per lead but share one model call, so they count
    /// as ceil(rows / chunk size) calls.
    /// </summary>
    public async Task<bool> IsOverAiBudgetAsync(OperatorSettings settings, CancellationToken ct)
    {
        if (settings.MaxAiCallsPerHour <= 0) return false;
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var soloCalls = await _db.AiTriageRuns.CountAsync(
            r => r.StartedAt >= cutoff && r.Provider != "" && r.Provider != "Heuristic" &&
                 !r.Provider.EndsWith("/batch"), ct);
        var batchRows = await _db.AiTriageRuns.CountAsync(
            r => r.StartedAt >= cutoff && r.Provider.EndsWith("/batch"), ct);
        var chunk = AiTriageRouter.BatchTriageChunkSize;
        var batchCalls = (batchRows + chunk - 1) / chunk;
        return soloCalls + batchCalls >= settings.MaxAiCallsPerHour;
    }

    private static string? GetSourceParameter(SourceConfig? source, string key)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.ParametersJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(source.ParametersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetBodyAsync(Opportunity opp, CancellationToken ct)
    {
        var raw = await _db.RawSourceItems
            .Where(r => r.OpportunityId == opp.Id)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        return raw?.BodyText ?? opp.Summary;
    }

    private static List<string> DeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string? NormalizeSourceUrl(string? sourceUrl) =>
        SourceUrlCanonicalizer.Canonicalize(sourceUrl);

    private async Task<Opportunity?> FindNearDuplicateOpportunityAsync(RawSourceItem item, string sourceUrl, CancellationToken ct)
    {
        var titleKey = LeadQualityRules.NormalizeDuplicateTitle(item.Title);
        if (titleKey.Length < 8) return null;

        var host = LeadQualityRules.HostFromUrl(sourceUrl);
        if (string.IsNullOrWhiteSpace(host)) return null;

        var cutoff = item.PostedAt.AddDays(-30);
        var candidates = await _db.RawSourceItems
            .Where(r => r.SourceKey == item.SourceKey && r.PostedAt >= cutoff && r.OpportunityId != null)
            .Select(r => new
            {
                r.OpportunityId,
                r.Url,
                r.Title,
                r.BodyText
            })
            .ToListAsync(ct);

        foreach (var c in candidates)
        {
            if (LeadQualityRules.HostFromUrl(c.Url) != host) continue;
            if (LeadQualityRules.NormalizeDuplicateTitle(c.Title) != titleKey) continue;
            if (!LeadQualityRules.SharesDuplicateClue($"{item.Title}\n{item.BodyText}", $"{c.Title}\n{c.BodyText}")) continue;

            return await _db.Opportunities.FindAsync(new object[] { c.OpportunityId!.Value }, ct);
        }

        return null;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";
}
