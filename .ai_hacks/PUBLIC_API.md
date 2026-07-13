# Type and Method Catalog

All source-authored types and callable members are listed; implementation bodies and literal data are omitted.

## DevLeads.Core

### `DevLeads.Core.Ai`

#### AiTriagePrompts

public class `AiTriagePrompts` · `src/DevLeads.Core/Ai/AiTriagePrompts.cs:4`

The unified system prompt, user template, and strict JSON schema for single-pass triage.

Data: `SystemPrompt`: string, `JsonSchema`: string.

- public `BuildUserPrompt(AiTriageRequest r) → string` — Fills the user-prompt template with post + pre-filter context.
- public `BuildBatchUserPrompt(IReadOnlyList<AiBatchTriageItem> items) → string` — Fills the user-prompt template for a batched call: several posts, one response object per post keyed by id. Bodies should be pre-compacted by the caller.

#### ContentPrompts

public class `ContentPrompts` · `src/DevLeads.Core/Ai/ContentPrompts.cs:8`

Prompts for the content studio: topic suggestion and long-form draft generation.

- public `BuildTopicPrompt(IReadOnlyList<TrendSignal> signals, string operatorSkills, IReadOnlyList<string> existingTopicTitles, int maxTopics) → string` — Asks for publishable topics distilled from trend signals. Output is strict JSON…
- public `BuildDraftPrompt(ContentTopic topic, ContentFormat format, OperatorSettings op, string operatorSkills) → string` — Asks for a complete piece in the requested format. Output is plain markdown whose first line is "# {title}" — no JSON, so long bodies can't break on escaping.
- private `FormatSpec(ContentFormat format) → string` — Transforms or resolves spec. _(inferred)_
- public `ParseEvidence(string json) → List<(string Title, string Url)>` — Transforms or resolves evidence. _(inferred)_
- private `Compact(string value, int max) → string` — Transforms or resolves compact. _(inferred)_

#### AiTriageRequest

public class `AiTriageRequest` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:6`

Input to the single-pass triage call.

Data: `Title`: string, `Body`: string, `SourceKey`: string, `PostedAt`: DateTimeOffset, `MatchedTerms`: IReadOnlyList<string>, `HeuristicScore`: decimal, `OperatorSkills`: string, `CampaignObjective`: string.

#### AiTriageResponse

public class `AiTriageResponse` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:23`

Outcome of a triage call, including provider metadata for the audit trail.

Data: `Succeeded`: bool, `Result`: AiTriageResult?, `Provider`: string, `Model`: string, `RequestJson`: string, `ResponseJson`: string?, `ErrorMessage`: string?, `Retryable`: bool.

#### AiShortlistItem

public class `AiShortlistItem` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:36`

Compact candidate shown to an AI provider before spending a full triage call.

Data: `Id`: string, `Title`: string, `Snippet`: string, `SourceKey`: string, `PostedAt`: DateTimeOffset, `MatchedTerms`: IReadOnlyList<string>, `HeuristicScore`: decimal.

#### AiShortlistDecision

public class `AiShortlistDecision` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:47`

Represents ai shortlist decision. _(inferred)_

Data: `Id`: string, `ShouldTriage`: bool, `Reason`: string.

#### AiShortlistResponse

public class `AiShortlistResponse` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:54`

Represents ai shortlist response. _(inferred)_

Data: `Succeeded`: bool, `Decisions`: IReadOnlyList<AiShortlistDecision>, `Provider`: string, `Model`: string, `RequestJson`: string, `ResponseJson`: string?, `ErrorMessage`: string?, `Retryable`: bool.

#### IAiBatchShortlistProvider

public interface `IAiBatchShortlistProvider` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:66`

Defines the ai batch shortlist provider contract. _(inferred)_

- public `ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) → Task<AiShortlistResponse>` — Handles shortlist. _(inferred)_

#### AiBatchTriageItem

public class `AiBatchTriageItem` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:77`

One item inside a batched triage call, keyed so results map back.

Data: `Id`: string, `Request`: AiTriageRequest.

#### AiBatchTriageResponse

public class `AiBatchTriageResponse` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:84`

Outcome of one batched triage call: per-item results keyed by item id.

Data: `Succeeded`: bool, `Results`: Dictionary<string, AiTriageResult>, `Provider`: string, `Model`: string, `RequestJson`: string, `ResponseJson`: string?, `ErrorMessage`: string?, `Retryable`: bool.

#### IAiBatchTriageProvider

public interface `IAiBatchTriageProvider` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:100`

Providers that can triage several posts in one model call. Batching is the main AI cost lever: N shortlisted items become ceil(N/chunk) calls instead of N calls.

- public `TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) → Task<AiBatchTriageResponse>` — Coordinates batch. _(inferred)_

#### IAiTriageProvider

public interface `IAiTriageProvider` · `src/DevLeads.Core/Ai/IAiTriageProvider.cs:113`

Abstraction over the AI triage backend. Providers are registered by name and selected at runtime through operator settings, so decision-making AI is always switchable.

Data: `Name`: string.

- public `IsAvailable(OperatorSettings settings) → bool` — Whether the provider can currently make calls (CLI present, key set, …).
- public `AvailabilityMessage(OperatorSettings settings) → string` — Human-readable explanation when IsAvailable is false.
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage. _(inferred)_

#### OutreachGenerationItem

public class `OutreachGenerationItem` · `src/DevLeads.Core/Ai/OutreachPrompts.cs:7`

One queued lead inside a batched response-generation call.

Data: `Id`: string, `Title`: string, `OriginalPost`: string, `SourceKey`: string, `Url`: string, `AuthorName`: string?, `CampaignObjective`: string.

#### OutreachPrompts

public class `OutreachPrompts` · `src/DevLeads.Core/Ai/OutreachPrompts.cs:22`

Prompt for batched outreach-response generation: every queued lead in one model call, each reply grounded strictly in that lead's original post.

- public `BuildBatchResponsePrompt(IReadOnlyList<OutreachGenerationItem> items, OperatorSettings op, string operatorSkills) → string` — Creates batch response prompt. _(inferred)_
- private `Compact(string value, int max) → string` — Transforms or resolves compact. _(inferred)_

#### PlatformPostPrompts

public class `PlatformPostPrompts` · `src/DevLeads.Core/Ai/PlatformPostPrompts.cs:11`

Prompt for drafting the operator's OWN posts/ads/profiles for a specific platform (reddit, craigslist, LinkedIn, Upwork, gmail outreach template) in the operator's real identity and voice, informed by which past posts…

Data: `SupportedPlatforms`: string[].

- public `BuildPostPrompt(string platform, OperatorSettings op, string operatorSkills, string campaignObjective, IReadOnlyList<(string Title, string Body, int Replies)> referencePosts, string extraInstructions) → string` — Creates post prompt. _(inferred)_
- public `BuildOptimizationPrompt(OperatorSettings op, string operatorSkills, IReadOnlyList<(long Id, string Community, string Title, string Body, double AgeDays, int Views, int Replies, int Upvotes)> posts, string extraInstructions) → string` — One batched call for the post-optimization experiment: each selected post gets a rewrite with a DISTINCT named strategy, so the operator can A/B the approaches against a…
- private `PlatformLabel(string platform) → string` — Handles platform label. _(inferred)_
- private `PlatformSpec(string platform) → string` — Handles platform spec. _(inferred)_
- private `Compact(string value, int max) → string` — Transforms or resolves compact. _(inferred)_

### `DevLeads.Core`

#### AiTriageResult

public class `AiTriageResult` · `src/DevLeads.Core/AiTriageResult.cs:10`

The strict structured object returned by the single-pass AI triage call. Satisfies all former pipeline stages (relevance, emergency, category, stack, cause, first step, fix time, confidence, recommendation) at once.

Data: `LanguageCode`: string, `EnglishTitle`: string, `EnglishBody`: string, `IsTechnicalProblem`: bool, `IsEmergency`: bool, `PaymentIntent`: string, `AssistanceRequested`: bool, `RejectReason`: string?, `ProblemCategory`: string, `DetectedStack`: List<string>, `EstimatedCause`: string, `FirstDiagnosticStep`: string, `EstimatedFixMinutesMin`: int?, `EstimatedFixMinutesMax`: int?, `AiConfidence`: decimal, `OutreachRecommendation`: string, `ProblemCategories`: string[], `PaymentIntents`: string[], `OutreachRecommendations`: string[].

### `DevLeads.Core.Connectors`

#### SourceConnectorConfig

public class `SourceConnectorConfig` · `src/DevLeads.Core/Connectors/ISourceConnector.cs:6`

Runtime configuration passed to a connector for a single fetch.

Data: `SourceKey`: string, `MaxItems`: int, `Terms`: IReadOnlyList<string>, `Parameters`: IReadOnlyDictionary<string, string>, `Since`: DateTimeOffset?, `SkillTerms`: IReadOnlyList<string>.

#### ConnectorHealth

public class `ConnectorHealth` · `src/DevLeads.Core/Connectors/ISourceConnector.cs:19`

Reported health of a connector after a run or health check.

Data: `Healthy`: bool, `Message`: string, `ItemCount`: int, `CheckedAt`: DateTimeOffset.

#### ISourceConnector

public interface `ISourceConnector` · `src/DevLeads.Core/Connectors/ISourceConnector.cs:31`

A read-only ingestion source. Fetches recent public items, respects rate limits, and never sends messages. Implementations must be resilient to network failure.

Data: `SourceKey`: string, `DisplayName`: string.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken cancellationToken) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch. _(inferred)_
- public `CheckHealthAsync(CancellationToken cancellationToken) → Task<ConnectorHealth>` — Checks health. _(inferred)_

### `DevLeads.Core.Entities`

#### AiTriageRun

public class `AiTriageRun` · `src/DevLeads.Core/Entities/AiTriageRun.cs:4`

An auditable record of a single-pass structured AI triage call.

Data: `Id`: long, `OpportunityId`: long, `Provider`: string, `Model`: string, `PromptVersion`: string, `Status`: AiJobStatus, `RequestJson`: string, `ResponseJson`: string?, `ErrorMessage`: string?, `StartedAt`: DateTimeOffset, `CompletedAt`: DateTimeOffset?, `Opportunity`: Opportunity?.

#### AuditEvent

public class `AuditEvent` · `src/DevLeads.Core/Entities/AuditEvent.cs:4`

An immutable audit-trail entry for anything the system generates, sends, or changes.

Data: `Id`: long, `EntityType`: string, `EntityId`: long, `EventType`: string, `Actor`: string, `Description`: string, `MetadataJson`: string, `CreatedAt`: DateTimeOffset.

#### Campaign

public class `Campaign` · `src/DevLeads.Core/Entities/Campaign.cs:8`

A lead-generation campaign: a named objective (e.g. emergency rescue work,.NET legacy modernization consulting) that owns a set of sources and the leads they produce.

Data: `Id`: long, `Key`: string, `Name`: string, `Emoji`: string, `Objective`: string, `Enabled`: bool, `CreatedAt`: DateTimeOffset.

#### ContentDraft

public class `ContentDraft` · `src/DevLeads.Core/Entities/ContentDraft.cs:7`

A generated piece of publishable content (blog post, article, white paper, research paper, or LinkedIn post) for the operator to edit and post on their own channels.

Data: `Id`: long, `TopicId`: long, `Format`: ContentFormat, `Title`: string, `BodyMarkdown`: string, `WordCount`: int, `Status`: ContentDraftStatus, `Provider`: string, `Model`: string, `CreatedAt`: DateTimeOffset, `UpdatedAt`: DateTimeOffset, `Topic`: ContentTopic?.

#### ContentTopic

public class `ContentTopic` · `src/DevLeads.Core/Entities/ContentTopic.cs:7`

An AI-suggested publishing topic distilled from trend signals: what to write about, the specific angle, and why an audience would care right now.

Data: `Id`: long, `Title`: string, `Angle`: string, `Rationale`: string, `InterestScore`: double, `SkillsJson`: string, `EvidenceJson`: string, `SuggestedFormatsCsv`: string, `Status`: ContentTopicStatus, `CreatedAt`: DateTimeOffset, `UpdatedAt`: DateTimeOffset, `Drafts`: List<ContentDraft>.

#### OperatorMessage

public class `OperatorMessage` · `src/DevLeads.Core/Entities/OperatorMessage.cs:10`

A private message or reply RECEIVED by the operator on an external platform (a reddit DM, a comment reply on one of their [For Hire] posts, an Upwork message…).

Data: `Id`: long, `Platform`: string, `ExternalId`: string, `Kind`: OperatorMessageKind, `Author`: string, `Subject`: string, `Body`: string, `Community`: string, `Url`: string, `Status`: OperatorMessageStatus, `OperatorPostId`: long?, `Post`: OperatorPost?, `Notes`: string, `ReceivedAt`: DateTimeOffset, `CreatedAt`: DateTimeOffset, `UpdatedAt`: DateTimeOffset.

#### OperatorPost

public class `OperatorPost` · `src/DevLeads.Core/Entities/OperatorPost.cs:9`

One of the operator's OWN posts on an external platform (a [For Hire] reddit post, an Upwork profile/proposal, a Craigslist ad…).

Data: `Id`: long, `Platform`: string, `ExternalId`: string, `Url`: string, `Title`: string, `Body`: string, `Community`: string, `Status`: OperatorPostStatus, `CampaignId`: long?, `ReplyCount`: int, `UpvoteCount`: int, `ViewCount`: int, `ViewCountKnown`: bool, `ThreadSummary`: string, `SummarizedAt`: DateTimeOffset?, `LastCheckedAt`: DateTimeOffset?, `Notes`: string, `PostedAt`: DateTimeOffset, `CreatedAt`: DateTimeOffset, `UpdatedAt`: DateTimeOffset, `Snapshots`: List<OperatorPostSnapshot>.

#### OperatorPostRevision

public class `OperatorPostRevision` · `src/DevLeads.Core/Entities/OperatorPostRevision.cs:11`

One AI-proposed (or operator-made) rewrite of a tracked post — the experiment unit for post optimization. Generated as a Proposed variant with a named approach; when the operator applies it on the platform, the…

Data: `Id`: long, `OperatorPostId`: long, `Post`: OperatorPost?, `Approach`: string, `Rationale`: string, `OldTitle`: string, `OldBody`: string, `NewTitle`: string, `NewBody`: string, `Status`: OperatorPostRevisionStatus, `Provider`: string, `Model`: string, `BaselineViewCount`: int, `BaselineReplyCount`: int, `BaselineUpvoteCount`: int, `BaselineViewsPerDay`: double, `BaselineRepliesPerDay`: double, `ResultPostId`: long?, `Notes`: string, `CreatedAt`: DateTimeOffset, `AppliedAt`: DateTimeOffset?.

#### OperatorPostSnapshot

public class `OperatorPostSnapshot` · `src/DevLeads.Core/Entities/OperatorPostSnapshot.cs:4`

Point-in-time engagement reading for an operator post — the "learn" trail.

Data: `Id`: long, `OperatorPostId`: long, `At`: DateTimeOffset, `ReplyCount`: int, `UpvoteCount`: int, `ViewCount`: int, `Post`: OperatorPost?.

#### OperatorSettings

public class `OperatorSettings` · `src/DevLeads.Core/Entities/OperatorSettings.cs:4`

Single-row settings for the solo operator: profile, AI, outreach, and safety controls.

Data: `Id`: long, `OperatorName`: string, `BusinessName`: string, `Location`: string, `ContactEmail`: string, `RemoteAvailability`: string, `CoreSkills`: string, `SecondarySkills`: string, `MinimumFee`: double, `PreferredPaymentTerms`: string, `EmergencyAvailability`: bool, `AiProvider`: string, `AiModel`: string, `OpenCodeCliPath`: string, `CodexCliPath`: string, `DefaultOpenCodeModel`: string, `DefaultAnthropicModel`: string, `DefaultCodexModel`: string, `TriageAiProvider`: string, `TriageAiModel`: string, `OutreachAiProvider`: string, `OutreachAiModel`: string, `ContentTopicsAiProvider`: string, `ContentTopicsAiModel`: string, `ContentDraftsAiProvider`: string, `ContentDraftsAiModel`: string, `PostDraftAiProvider`: string, `PostDraftAiModel`: string, `ThreadSummaryAiProvider`: string, `ThreadSummaryAiModel`: string, `PostOptimizationAiProvider`: string, `PostOptimizationAiModel`: string, `PromptVersion`: string, `MaxAiCallsPerHour`: int, `MaxAiSpendPerDay`: double, `MinPreFilterScoreForAi`: double, `MinAiConfidenceForDraft`: double, `ManualReviewConfidenceThreshold`: double, `AiRetryCount`: int, `AiTimeoutSeconds`: int, `DefaultOutreachMode`: OutreachMode, `GlobalAutoModeEnabled`: bool, `GlobalKillSwitch`: bool, `MaxSendsPerHour`: int, `MaxSendsPerDay`: int, `RequireApprovalAboveRisk`: double, `RequireApprovalBelowConfidence`: double, `SuppressionListEnabled`: bool, `AuditLoggingEnabled`: bool, `DraftScoreThreshold`: double, `AlertScoreThreshold`: double, `SelectedCampaignId`: long?, `DiscoveryEnabled`: bool, `ContentDiscoveryEnabled`: bool, `RedditUsername`: string, `RedditClientId`: string, `RedditClientSecret`: string, `RedditAppPassword`: string, `RedditInboxFeedToken`: string, `StaleItemMaxAgeHours`: int, `FollowUpDefaultHours`: int.

- public `AiFor(AiFeature feature) → (string Provider, string Model)` — The provider/model pair a feature actually uses, after override resolution.
- public `WithAiFor(AiFeature feature) → OperatorSettings` — Copy of these settings with AiProvider/AiModel resolved for a feature.
- public `DefaultModelFor(string provider) → string` — Handles default model for. _(inferred)_

#### Opportunity

public class `Opportunity` · `src/DevLeads.Core/Entities/Opportunity.cs:6`

A triaged, scored emergency-repair lead. The central aggregate the whole app revolves around.

Data: `Id`: long, `Title`: string, `Summary`: string, `CampaignId`: long?, `SourceKey`: string, `SourceUrl`: string, `AuthorName`: string?, `AuthorProfileUrl`: string?, `Status`: OpportunityStatus, `Priority`: Priority, `Score`: double, `UrgencyScore`: double, `StackFitScore`: double, `BusinessValueScore`: double, `ReachabilityScore`: double, `CompetitionScore`: double, `TrustScore`: double, `LanguagePenalty`: double, `LanguageCode`: string, `TranslatedBody`: string, `ProblemType`: string, `PaymentIntent`: string, `AssistanceRequested`: bool?, `DetectedStackJson`: string, `SuggestedFirstStep`: string, `EstimatedCause`: string, `EstimatedFeeMin`: double?, `EstimatedFeeMax`: double?, `FeeIsEstimate`: bool, `EstimatedFixMinutesMin`: int?, `EstimatedFixMinutesMax`: int?, `AiConfidence`: double, `OutreachRecommendation`: OutreachRecommendation, `RejectionReason`: string?, `AiJobStatus`: AiJobStatus, `HeuristicScore`: double, `MatchedTermsJson`: string, `PreFilterRejectReason`: string?, `AutoEligible`: bool, `PostedAt`: DateTimeOffset, `FirstSeenAt`: DateTimeOffset, `LastSeenAt`: DateTimeOffset, `CreatedAt`: DateTimeOffset, `UpdatedAt`: DateTimeOffset, `NextFollowUpAt`: DateTimeOffset?, `WorkNotes`: string?, `TriageRuns`: List<AiTriageRun>, `OutreachAttempts`: List<OutreachAttempt>, `Quotes`: List<Quote>, `WorkSessions`: List<WorkSession>.

#### OutreachAttempt

public class `OutreachAttempt` · `src/DevLeads.Core/Entities/OutreachAttempt.cs:4`

A drafted, approved, or sent outreach message tied to an opportunity.

Data: `Id`: long, `OpportunityId`: long, `Channel`: OutreachChannel, `Mode`: OutreachMode, `Subject`: string?, `Body`: string, `TemplateKey`: string, `Status`: OutreachStatus, `RequiresApproval`: bool, `ApprovedAt`: DateTimeOffset?, `SentAt`: DateTimeOffset?, `ResponseReceivedAt`: DateTimeOffset?, `ErrorMessage`: string?, `CreatedAt`: DateTimeOffset, `Opportunity`: Opportunity?.

#### QueryPack

public class `QueryPack` · `src/DevLeads.Core/Entities/QueryPack.cs:4`

A named set of search/keyword terms used by connectors and the heuristic pre-filter.

Data: `Id`: long, `Name`: string, `Description`: string, `Terms`: string, `IsHighPriority`: bool, `IsNegative`: bool.

#### Quote

public class `Quote` · `src/DevLeads.Core/Entities/Quote.cs:4`

A flat-fee emergency-repair quote and its payment lifecycle.

Data: `Id`: long, `OpportunityId`: long, `Amount`: double, `PaymentDueUponCompletion`: bool, `DiagnosticFee`: double?, `Scope`: string, `Exclusions`: string, `PaymentUrl`: string?, `Status`: QuoteStatus, `CreatedAt`: DateTimeOffset, `SentAt`: DateTimeOffset?, `PaidAt`: DateTimeOffset?, `DueAt`: DateTimeOffset?, `Opportunity`: Opportunity?.

#### RawSourceItem

public class `RawSourceItem` · `src/DevLeads.Core/Entities/RawSourceItem.cs:7`

A normalized public item fetched from a source connector, stored before/after triage. Also serves as the connector output DTO.

Data: `Id`: long, `OpportunityId`: long?, `SourceKey`: string, `ExternalId`: string, `Url`: string, `AuthorName`: string?, `AuthorProfileUrl`: string?, `Title`: string, `BodyText`: string, `PostedAt`: DateTimeOffset, `FetchedAt`: DateTimeOffset, `RawJson`: string, `ContentHash`: string, `Opportunity`: Opportunity?.

#### Skill

public class `Skill` · `src/DevLeads.Core/Entities/Skill.cs:8`

One operator skill (language, framework, application, capability…). Used to score how well a lead fits the operator, to filter bounty/issue searches, and to describe the operator inside the AI triage prompt.

Data: `Id`: long, `Name`: string, `Category`: string, `Weight`: int, `Enabled`: bool, `Aliases`: string.

#### SourceConfig

public class `SourceConfig` · `src/DevLeads.Core/Entities/SourceConfig.cs:4`

Per-connector configuration and health, editable from the Sources page.

Data: `Id`: long, `SourceKey`: string, `DisplayName`: string, `Enabled`: bool, `CampaignId`: long?, `PollIntervalMinutes`: int, `MaxItemsPerRun`: int, `QueryPacksCsv`: string, `ParametersJson`: string, `MinPreFilterScore`: double, `MinOpportunityScore`: double, `DraftThreshold`: double, `AlertThreshold`: double, `AutoModeEligible`: bool, `LastRunHealthy`: bool, `LastRunMessage`: string?, `LastRunItemCount`: int, `LastRunAt`: DateTimeOffset?, `NextRunAt`: DateTimeOffset?.

#### SuppressionEntry

public class `SuppressionEntry` · `src/DevLeads.Core/Entities/SuppressionEntry.cs:4`

A contact that must never be messaged (opt-out, complaint, or manual block).

Data: `Id`: long, `ContactValue`: string, `ContactType`: SuppressionContactType, `Reason`: string, `Source`: string, `CreatedAt`: DateTimeOffset.

#### TrendSignal

public class `TrendSignal` · `src/DevLeads.Core/Entities/TrendSignal.cs:7`

One piece of evidence that something is trending: a hot post, a release note, an announcement. Signals feed AI topic generation and are pruned after ~30 days.

Data: `Id`: long, `SourceKey`: string, `ExternalId`: string, `Url`: string, `Title`: string, `Snippet`: string, `PostedAt`: DateTimeOffset, `FetchedAt`: DateTimeOffset, `Engagement`: double, `MatchedSkillsJson`: string, `Hotness`: double, `UsedInTopic`: bool.

#### TrendSource

public class `TrendSource` · `src/DevLeads.Core/Entities/TrendSource.cs:8`

A feed/community polled for *content* signals (trending topics, releases, updates) rather than leads. Kept separate from SourceConfig on purpose: trend sources have no triage thresholds, and their items become…

Data: `Id`: long, `SeedKey`: string, `Kind`: string, `DisplayName`: string, `ParametersJson`: string, `Enabled`: bool, `PollIntervalMinutes`: int, `MaxItemsPerRun`: int, `RequireSkillMatch`: bool, `LastRunHealthy`: bool, `LastRunMessage`: string?, `LastRunItemCount`: int, `LastRunAt`: DateTimeOffset?, `NextRunAt`: DateTimeOffset?.

#### WorkSession

public class `WorkSession` · `src/DevLeads.Core/Entities/WorkSession.cs:4`

Tracks execution once a lead becomes real work: checklist, notes, fix summary.

Data: `Id`: long, `OpportunityId`: long, `StartedAt`: DateTimeOffset, `EndedAt`: DateTimeOffset?, `Status`: WorkSessionStatus, `AccessChecklistJson`: string, `Notes`: string, `FixSummary`: string, `ClientConfirmation`: string, `Opportunity`: Opportunity?.

### `DevLeads.Core`

#### OpportunityStatus

public enum `OpportunityStatus` · `src/DevLeads.Core/Enums.cs:4`

Workflow states an opportunity moves through from discovery to payment.

#### Priority

public enum `Priority` · `src/DevLeads.Core/Enums.cs:34`

Priority band derived from the weighted opportunity score.

#### AiJobStatus

public enum `AiJobStatus` · `src/DevLeads.Core/Enums.cs:44`

Lifecycle of the single-pass AI triage job for an item.

#### OutreachRecommendation

public enum `OutreachRecommendation` · `src/DevLeads.Core/Enums.cs:56`

What the system recommends doing with an opportunity.

#### OutreachMode

public enum `OutreachMode` · `src/DevLeads.Core/Enums.cs:66`

Outreach delivery mode for a given source/template/contact combination.

#### OutreachStatus

public enum `OutreachStatus` · `src/DevLeads.Core/Enums.cs:76`

Lifecycle of a single outreach attempt.

#### OutreachChannel

public enum `OutreachChannel` · `src/DevLeads.Core/Enums.cs:90`

Channel an outreach attempt is delivered over.

#### QuoteStatus

public enum `QuoteStatus` · `src/DevLeads.Core/Enums.cs:100`

Payment lifecycle for a quote.

#### WorkSessionStatus

public enum `WorkSessionStatus` · `src/DevLeads.Core/Enums.cs:115`

Execution state for a hands-on work session.

#### ContentTopicStatus

public enum `ContentTopicStatus` · `src/DevLeads.Core/Enums.cs:126`

Lifecycle of an AI-suggested publishing topic.

#### ContentDraftStatus

public enum `ContentDraftStatus` · `src/DevLeads.Core/Enums.cs:134`

Lifecycle of a generated content draft.

#### ContentFormat

public enum `ContentFormat` · `src/DevLeads.Core/Enums.cs:143`

Publishable formats the content studio can generate.

#### OperatorPostStatus

public enum `OperatorPostStatus` · `src/DevLeads.Core/Enums.cs:153`

Lifecycle of one of the operator's own posts on an external platform.

#### OperatorPostRevisionStatus

public enum `OperatorPostRevisionStatus` · `src/DevLeads.Core/Enums.cs:163`

Lifecycle of an AI-proposed rewrite of one of the operator's posts.

#### OperatorMessageKind

public enum `OperatorMessageKind` · `src/DevLeads.Core/Enums.cs:173`

What kind of inbox item a received operator message is.

#### OperatorMessageStatus

public enum `OperatorMessageStatus` · `src/DevLeads.Core/Enums.cs:187`

Operator-side lifecycle of a received message.

#### SuppressionContactType

public enum `SuppressionContactType` · `src/DevLeads.Core/Enums.cs:196`

How a contact was added to the suppression list.

#### AiFeature

public enum `AiFeature` · `src/DevLeads.Core/Enums.cs:209`

The distinct AI call sites in the app. Each can carry its own provider/model override in Entities.OperatorSettings; an unset override inherits the global AiProvider/AiModel pair.

#### HeuristicPreFilter

public class `HeuristicPreFilter` · `src/DevLeads.Core/HeuristicPreFilter.cs:11`

Zero-cost keyword/rule filter deciding whether a raw item is worth an LLM call. Protects the AI budget, cuts latency, and rejects obvious noise before triage.

Depends on: `IQueryPackProvider queryPacks`.

Data: `_queryPacks`: IQueryPackProvider, `UrgencySignals`: string[], `TechnicalSignals`: string[], `CommercialSignals`: string[], `PayIntentSignals`: string[], `MoneyPattern`: Regex, `AntiPaySignals`: string[].

- public `HasPayLanguage(string text) → bool` — True when the text contains explicit hire/pay language or a money amount, un-negated.
- public `Analyze(RawSourceItem item, IReadOnlyCollection<string>? packNames) → PreFilterResult` — Analyzes an item. When packNames is given, high-priority term matching is scoped to those packs (the source's own QueryPacksCsv) so one campaign's trigger vocabulary never…
- private `MatchSignals(string text, IEnumerable<string> signals) → List<string>` — Handles match signals. _(inferred)_

#### LeadQualityRules

public class `LeadQualityRules` · `src/DevLeads.Core/LeadQualityRules.cs:6`

Shared lead-quality rules used before a post reaches the review queue.

Data: `EmailPattern`: Regex, `UrlHostPattern`: Regex, `NonWordPattern`: Regex, `ThirdPartyPaySignals`: string[], `AntiPaySignals`: string[], `VendorSupportSignals`: string[], `ResolvedSignals`: string[], `ConcretePaidSources`: string[], `LaunchSignals`: string[], `PricingCopySignals`: string[], `ProblemReportSignals`: string[], `GitHubMetaPattern`: Regex, `DiscourseFooterPattern`: Regex, `ClaimedWorkSignals`: string[].

- public `IsPromotionalAnnouncement(string text) → bool` — True for product-launch/showcase posts: launch language plus the poster's own pricing copy, with no actual problem being reported.
- public `IsReplyFeedItem(string title) → bool` — True for feed items that are replies into an existing thread (WordPress.org reply feeds emit "Reply To: …" items) — the reply author is answering, not asking.
- public `IsAlreadyClaimed(string text) → bool` — True when the post shows someone else already owns the work: the issue is assigned, or the visible text contains claim/PR-in-flight language.
- public `CompetingResponseCount(string text) → int` — How many other people are already engaging with the post: GitHub comments, or Discourse thread participants beyond the author. 0 when unknown.
- public `IsAiAgentTaskPost(string title, string text) → bool` — Checks ai agent task post. _(inferred)_
- public `HasThirdPartyPayOffer(string text) → bool` — Checks third party pay offer. _(inferred)_
- public `IsVendorControlledSupportRequest(string text) → bool` — Checks vendor controlled support request. _(inferred)_
- public `IsNonHirableVendorSupportRequest(string text) → bool` — Checks non hirable vendor support request. _(inferred)_
- public `IsResolvedOrClosedRequest(string text) → bool` — Checks resolved or closed request. _(inferred)_
- public `IsConcretePaidSource(string sourceKey) → bool` — Checks concrete paid source. _(inferred)_
- public `IsDashboardWorthyLead(string sourceKey, string paymentIntent, bool? assistanceRequested, bool feeIsEstimate, string text) → bool` — Checks dashboard worthy lead. _(inferred)_
- public `NormalizeDuplicateTitle(string title) → string` — Transforms or resolves duplicate title. _(inferred)_
- public `HostFromUrl(string? url) → string?` — Handles host from url. _(inferred)_
- public `SharesDuplicateClue(string leftText, string rightText) → bool` — Handles shares duplicate clue. _(inferred)_
- private `ExtractDuplicateClues(string text) → HashSet<string>` — Transforms or resolves duplicate clues. _(inferred)_
- private `NormalizeHost(string host) → string` — Transforms or resolves host. _(inferred)_

#### OfferedCompensation

public class `OfferedCompensation` · `src/DevLeads.Core/OfferedCompensation.cs:11`

Extracts a compensation amount the poster explicitly stated ("Reward: $15", "[Bounty $250]", "budget of $500–$800"). Only amounts adjacent to a compensation keyword count — a stray "$120k ARR" in a post body is not an…

Data: `Money`: Regex, `Keyword`: Regex, `RateSuffix`: Regex, `CryptoMoney`: Regex, `CryptoUsdRate`: Dictionary<string, double>.

- public `Extract(string title, string body) → (double Min, double Max)?` — Returns the stated amount range, or null when no explicit offer exists.

#### PreFilterResult

public class `PreFilterResult` · `src/DevLeads.Core/PreFilterResult.cs:4`

Result of the zero-cost heuristic pre-filter that gates AI analysis.

Data: `ShouldAnalyzeWithAi`: bool, `KeywordHitCount`: int, `HighPriorityHitCount`: int, `NegativeHitCount`: int, `PayIntentHitCount`: int, `HeuristicScore`: decimal, `MatchedTerms`: List<string>, `RejectReason`: string?.

### `DevLeads.Core.QueryPacks`

#### QueryPackSeed

public record class `QueryPackSeed` · `src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs:4`

Seed definition for a query pack.

Depends on: `string Name`, `string Description`, `bool IsHighPriority`, `bool IsNegative`, `string[] Terms`.

#### DefaultQueryPacks

public class `DefaultQueryPacks` · `src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs:7`

The built-in query packs from the design document, used to seed the database.

Data: `EmergencyGeneric`: QueryPackSeed, `DotNetSqlPriority`: QueryPackSeed, `PaymentEcommerce`: QueryPackSeed, `AgencyClientUrgency`: QueryPackSeed, `SaaSApiAuth`: QueryPackSeed, `InfraOps`: QueryPackSeed, `WordPressHosting`: QueryPackSeed, `ContractProjectWork`: QueryPackSeed, `SupportPain`: QueryPackSeed, `HireIntent`: QueryPackSeed, `PaidFeatureRequest`: QueryPackSeed, `DotNetModernization`: QueryPackSeed, `AiAutomationProjects`: QueryPackSeed, `NegativeExclusions`: QueryPackSeed, `All`: QueryPackSeed[].

#### IQueryPackProvider

public interface `IQueryPackProvider` · `src/DevLeads.Core/QueryPacks/IQueryPackProvider.cs:4`

Supplies keyword sets (query packs) to connectors and the heuristic pre-filter.

- public `GetTerms(string packName) → IReadOnlyList<string>` — Returns the terms for a named pack (empty if unknown).
- public `GetHighPriorityTerms() → IReadOnlyList<string>` — All high-priority emergency terms across packs.
- public `GetHighPriorityTerms(IReadOnlyCollection<string> packNames) → IReadOnlyList<string>` — High-priority terms restricted to the named packs, so a source (and its campaign) is pre-filtered against its own signals instead of every campaign's.
- public `GetNegativeTerms() → IReadOnlyList<string>` — All negative / exclusion terms.

### `DevLeads.Core`

#### RedFlagResult

public record class `RedFlagResult` · `src/DevLeads.Core/RedFlagDetector.cs:4`

Outcome of a red-flag scan.

Depends on: `bool IsRedFlagged`, `IReadOnlyList<string> Reasons`.

Data: `None`: RedFlagResult.

#### RedFlagDetector

public class `RedFlagDetector` · `src/DevLeads.Core/RedFlagDetector.cs:13`

Flags posts that request unauthorized access, credential theft, malware, fraud, or otherwise carry ownership/authorization risk. Such posts must never be auto-contacted.

Data: `Patterns`: (string Phrase, string Reason)[].

- public `Scan(string title, string body) → RedFlagResult` — Coordinates scan. _(inferred)_

### `DevLeads.Core.Scoring`

#### ScoreBreakdown

public class `ScoreBreakdown` · `src/DevLeads.Core/Scoring/OpportunityScorer.cs:6`

The blended score plus its weighted components.

Data: `UrgencyScore`: double, `StackFitScore`: double, `BusinessValueScore`: double, `ReachabilityScore`: double, `CompetitionScore`: double, `TrustScore`: double, `LanguagePenalty`: double, `Total`: double, `Priority`: Priority.

#### ScoringInput

public class `ScoringInput` · `src/DevLeads.Core/Scoring/OpportunityScorer.cs:21`

Inputs the scorer needs, decoupled from persistence.

Data: `Ai`: AiTriageResult?, `PreFilter`: PreFilterResult?, `SourceKey`: string, `PostedAt`: DateTimeOffset, `RedFlagged`: bool, `HasContact`: bool, `LanguageCode`: string, `SkillMatches`: IReadOnlyList<SkillMatch>?, `OfferedAmount`: double?, `ClaimedByOthers`: bool, `CompetingResponses`: int, `ForeignStackDemands`: IReadOnlyList<string>.

#### OpportunityScorer

public class `OpportunityScorer` · `src/DevLeads.Core/Scoring/OpportunityScorer.cs:59`

Blends heuristic, AI, source-reputation, recency, stack-fit, business-value, reachability and trust signals into a single weighted opportunity score.

Data: `WUrgency`: double, `WStack`: double, `WBusiness`: double, `WReach`: double, `WCompetition`: double, `WTrust`: double, `NonEnglishPenalty`: double, `PreferredStack`: string[], `StrongStack`: string[], `MediumHighStack`: string[], `MediumStack`: string[], `PayIntentSources`: string[].

- public `Score(ScoringInput input, DateTimeOffset now) → ScoreBreakdown` — Handles score. _(inferred)_
- public `IsNonEnglish(string? languageCode) → bool` — Checks non english. _(inferred)_
- private `PayHits(ScoringInput i) → int` — Count of explicit "pay:" hits the pre-filter tagged (hire language, budgets, money amounts).
- private `HasPaySignal(ScoringInput i) → bool` — Any evidence the poster would actually pay: pay-intent source, AI judgment, or pay language.
- public `ToPriority(double total) → Priority` — Handles to priority. _(inferred)_
- private `Urgency(ScoringInput i, DateTimeOffset now) → double` — Handles urgency. _(inferred)_
- private `CategorySeverityBonus(string category) → double` — Handles category severity bonus. _(inferred)_
- private `StackFit(ScoringInput i) → double` — Handles stack fit. _(inferred)_
- private `BusinessValue(ScoringInput i) → double` — Handles business value. _(inferred)_
- private `Reachability(ScoringInput i) → double` — Handles reachability. _(inferred)_
- private `Competition(ScoringInput i) → double` — Handles competition. _(inferred)_
- private `SourceBaseCompetition(ScoringInput i) → double` — Handles source base competition. _(inferred)_
- private `Trust(ScoringInput i) → double` — Handles trust. _(inferred)_
- private `SourceReputation(string sourceKey) → double` — Handles source reputation. _(inferred)_
- private `IsPayIntentSource(string sourceKey) → bool` — Checks pay intent source. _(inferred)_

### `DevLeads.Core.Skills`

#### DefaultSkills

public class `DefaultSkills` · `src/DevLeads.Core/Skills/DefaultSkills.cs:10`

The operator's seeded skill profile (from the operator's own skillset document). Only seeds when the Skills table is empty — the Skills page is the source of truth after that.

Data: `All`: IReadOnlyList<Skill>.

- private `S(string category, string name, int weight, string aliases) → Skill` — Handles s. _(inferred)_

#### SkillMatch

public record class `SkillMatch` · `src/DevLeads.Core/Skills/SkillMatcher.cs:7`

A skill that matched a piece of lead text, with its profile weight and category.

Depends on: `string Name`, `int Weight`, `string Category`.

#### SkillMatcher

public class `SkillMatcher` · `src/DevLeads.Core/Skills/SkillMatcher.cs:10`

Matches lead text against the operator's skill profile and scores the fit.

Data: `StackIdentityCategories`: string[], `ForeignStacks`: (string Name, Regex Pattern)[].

- public `Match(string text, IEnumerable<Skill> skills) → List<SkillMatch>` — All enabled skills whose name or any alias appears in the text (case-insensitive).
- public `HasStackIdentityMatch(IEnumerable<SkillMatch> matches) → bool` — True when the text matched at least one weight-3 skill from an identity category.
- public `ForeignStackDemands(string text, IEnumerable<Skill> skills) → List<string>` — Foreign primary-stack demands found in the text, excluding stacks the operator has an enabled skill for (adding a "Python" skill makes Python stop being foreign).
- public `FitScore(IReadOnlyList<SkillMatch> matches) → double` — 0–100 fit score mirroring the legacy stack tiers: a core-skill match scores like the preferred stack, strong like the secondary stack; breadth adds a small bonus.
- public `PromptSummary(IEnumerable<Skill> skills, int maxItems) → string` — Compact profile description for the AI triage prompt, strongest skills first.
- public `SearchTerms(IEnumerable<Skill> skills, int max) → List<string>` — Search keywords for connectors (bounty/issue queries): short, high-weight names first.
- private `ContainsTerm(string text, string term) → bool` — Checks term. _(inferred)_
- private `SplitAliases(string aliases) → IEnumerable<string>` — Transforms or resolves aliases. _(inferred)_

### `DevLeads.Core`

#### SourceUrlCanonicalizer

public class `SourceUrlCanonicalizer` · `src/DevLeads.Core/SourceUrlCanonicalizer.cs:8`

Canonicalizes source URLs so the same post always yields the same string: drops the fragment (forum reply anchors like topic#post-123 point at the same topic), tracking params, and trailing slashes.

- public `Canonicalize(string? sourceUrl) → string?` — Returns the canonical http(s) URL, or null when the input isn't one.

### `DevLeads.Core.Templates`

#### EmergencyChecklist

public record class `EmergencyChecklist` · `src/DevLeads.Core/Templates/EmergencyChecklists.cs:3`

Represents emergency checklist. _(inferred)_

Depends on: `string Key`, `string Name`, `string[] Items`.

#### EmergencyChecklists

public class `EmergencyChecklists` · `src/DevLeads.Core/Templates/EmergencyChecklists.cs:6`

Diagnostic checklists surfaced when a lead becomes real work.

Data: `WebsiteDown`: EmergencyChecklist, `DotNetIis`: EmergencyChecklist, `Database`: EmergencyChecklist, `All`: IReadOnlyList<EmergencyChecklist>.

- public `SuggestFor(string problemCategory) → EmergencyChecklist` — Picks the most relevant checklist for a problem category.

#### PricingTier

public record class `PricingTier` · `src/DevLeads.Core/Templates/PricingTiers.cs:3`

Represents pricing tier. _(inferred)_

Depends on: `string Name`, `string UseCase`, `double SuggestedMin`, `double SuggestedMax`.

#### PricingTiers

public class `PricingTiers` · `src/DevLeads.Core/Templates/PricingTiers.cs:6`

Suggested pricing tiers used by the quote generator and detail UI.

Data: `All`: IReadOnlyList<PricingTier>.

- public `SuggestFor(string problemCategory) → (double Min, double Max)` — Chooses a tier from a category, returning a (min,max) suggested fee.

#### ResponseTemplate

public record class `ResponseTemplate` · `src/DevLeads.Core/Templates/ResponseTemplates.cs:3`

Represents response template. _(inferred)_

Depends on: `string Key`, `string Name`, `string Channel`, `string Body`.

#### ResponseTemplates

public class `ResponseTemplates` · `src/DevLeads.Core/Templates/ResponseTemplates.cs:6`

Vetted response templates. Placeholders in [brackets] are filled per-opportunity.

Data: `PublicTechnicalReply`: string, `DirectOutreach`: string, `CompletionSmallJob`: string, `QuoteMessage`: string, `All`: IReadOnlyList<ResponseTemplate>.

- public `Get(string key) → ResponseTemplate` — Loads or resolves get. _(inferred)_

## DevLeads.Infrastructure

### `DevLeads.Infrastructure.Ai`

#### AiCliSupport

public class `AiCliSupport` · `src/DevLeads.Infrastructure/Ai/AiCliSupport.cs:13`

Prompt building and output parsing shared by the CLI-backed AI providers (OpenCode, Codex). Both speak the same contract — a strict-JSON triage/shortlist prompt in, arbitrary agent output out — so the schema knowledge…

Data: `ParseOptions`: JsonSerializerOptions, `AnsiPattern`: Regex.

- public `BuildTriagePrompt(AiTriageRequest request) → string` — Creates triage prompt. _(inferred)_
- public `BuildBatchTriagePrompt(IReadOnlyList<AiBatchTriageItem> items) → string` — Creates batch triage prompt. _(inferred)_
- public `BuildShortlistPrompt(IReadOnlyList<AiShortlistItem> items, int maxSelections, string campaignObjective) → string` — Creates shortlist prompt. _(inferred)_
- public `StripAnsi(string text) → string` — Handles strip ansi. _(inferred)_
- public `ExtractJsonObject(string text) → string?` — Extracts the first balanced JSON object from arbitrary CLI output.
- public `IsSchemaValid(AiTriageResult r) → bool` — Checks schema valid. _(inferred)_
- public `Normalize(AiTriageResult r) → void` — Coerces near-miss enum values back onto the strict schema instead of failing the call.
- public `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_

#### ShortlistOutput

public class `ShortlistOutput` · `src/DevLeads.Infrastructure/Ai/AiCliSupport.cs:62`

Represents shortlist output. _(inferred)_

Data: `Selected`: List<ShortlistSelection>.

#### ShortlistSelection

public class `ShortlistSelection` · `src/DevLeads.Infrastructure/Ai/AiCliSupport.cs:67`

Represents shortlist selection. _(inferred)_

Data: `Id`: string, `Reason`: string?.

#### AiTextRouter

public class `AiTextRouter` · `src/DevLeads.Infrastructure/Ai/AiTextRouter.cs:16`

Routes the app's long-form / free-text generation calls (outreach replies, content drafts, the operator's own posts, thread summaries, optimization rewrites) to the right CLI provider for each AiFeature.

Depends on: `OpenCodeTriageProvider openCode`, `CodexCliProvider codex`, `ILogger<AiTextRouter> log`.

Data: `_openCode`: OpenCodeTriageProvider, `_codex`: CodexCliProvider, `_log`: ILogger<AiTextRouter>.

- public `GenerateTextAsync(AiFeature feature, string prompt, OperatorSettings settings, TimeSpan timeout, CancellationToken ct) → Task<(bool Succeeded, string Text, string Error, string Model)>` — Runs prompt through the provider/model configured for feature. The returned tuple mirrors the CLI providers' GenerateTextAsync contract; callers own the output format.
- public `ProviderFor(OperatorSettings settings, AiFeature feature) → string` — The provider name a feature will actually use — for pre-flight UI/guards.

#### AiTriageRouter

public class `AiTriageRouter` · `src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs:13`

Registry of AI triage providers, selected by name from operator settings. All decision-making AI flows through here, so the backend is always switchable: OpenCode CLI (default) → Anthropic API → Heuristic rules (final…

Depends on: `IEnumerable<IAiTriageProvider> providers`, `HeuristicTriageProvider heuristic`, `ILogger<AiTriageRouter> log`.

Data: `BatchTriageChunkSize`: int, `_providers`: IReadOnlyList<IAiTriageProvider>, `_heuristic`: HeuristicTriageProvider, `_log`: ILogger<AiTriageRouter>, `Providers`: IReadOnlyList<IAiTriageProvider>.

- public `Resolve(OperatorSettings settings) → IAiTriageProvider` — The provider that will actually serve calls for these settings.
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage. _(inferred)_
- public `TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) → Task<AiBatchTriageResponse>` — Triages several posts in one model call when the resolved provider supports it. Returns a failed response when it doesn't — callers then use per-item triage.
- public `ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) → Task<AiShortlistResponse>` — Handles shortlist. _(inferred)_
- private `BuildHeuristicShortlist(IReadOnlyList<AiShortlistItem> items, int maxSelections) → AiShortlistResponse` — Creates heuristic shortlist. _(inferred)_

#### AnthropicTriageProvider

public class `AnthropicTriageProvider` : `IAiTriageProvider` · `src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs:14`

Calls Claude via the official Anthropic SDK with a single structured triage request. Returns strict JSON validated against AiTriageResult.

Data: `Name`: string, `HasApiKey`: bool, `ParseOptions`: JsonSerializerOptions.

- public `IsAvailable(OperatorSettings settings) → bool` — Whether the provider can currently make calls (CLI present, key set, …).
- public `AvailabilityMessage(OperatorSettings settings) → string` — Human-readable explanation when IsAvailable is false.
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage.

#### CodexCliProvider

public class `CodexCliProvider` : `IAiTriageProvider`, `IAiBatchShortlistProvider`, `IAiBatchTriageProvider` · `src/DevLeads.Infrastructure/Ai/CodexCliProvider.cs:19`

OpenAI-backed provider: runs the same structured triage/shortlist/generation calls through the local `codex` CLI (https://github.com/openai/codex) in non-interactive `exec` mode.

Depends on: `ILogger<CodexCliProvider> log`.

Data: `_log`: ILogger<CodexCliProvider>, `ProbeLock`: object, `_probedPath`: string?, `_probeResult`: bool, `_probeMessage`: string, `Name`: string.

- public `IsAvailable(OperatorSettings settings) → bool` — Whether the provider can currently make calls (CLI present, key set, …).
- public `AvailabilityMessage(OperatorSettings settings) → string` — Human-readable explanation when IsAvailable is false.
- private `ResolveModel(OperatorSettings settings) → string` — Transforms or resolves model. _(inferred)_
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage.
- public `TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) → Task<AiBatchTriageResponse>` — Coordinates batch.
- public `ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) → Task<AiShortlistResponse>` — Handles shortlist.
- public `GenerateTextAsync(string prompt, OperatorSettings settings, TimeSpan timeout, CancellationToken ct) → Task<(bool Succeeded, string Text, string Error, string Model)>` — Generic long-form generation, mirroring the OpenCode provider's contract: one prompt through `codex exec`, raw final-message text back.
- public `ResolveCliPath(OperatorSettings settings) → string` — Resolves the configured CLI path, falling back to the standard install location.
- private `OnPath(string command) → bool` — Handles on path. _(inferred)_
- private `Probe(string cliPath) → (bool Available, string Message)` — Handles probe. _(inferred)_
- public `ResetProbe() → void` — Clears the cached probe so a changed CLI path takes effect immediately.
- private `RunCliAsync(string cliPath, string model, string prompt, TimeSpan timeout, CancellationToken ct) → Task<(int ExitCode, string Output, string Stderr)>` — One `codex exec` call. Runs in an isolated scratch directory with a read-only sandbox (codex is a coding agent; the prompts already forbid tool use, and the sandbox enforces it).

#### HeuristicTriageProvider

public class `HeuristicTriageProvider` : `IAiTriageProvider` · `src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs:12`

A zero-cost, no-network triage provider. Infers a plausible structured result from keywords so the full pipeline runs end-to-end without an API key.

Data: `Name`: string, `CategoryMap`: (string[] Keywords, string Category)[], `SoftwareContextSignals`: string[], `AssistanceSignals`: string[], `ImpliedPaySignals`: string[].

- public `IsAvailable(OperatorSettings settings) → bool` — Whether the provider can currently make calls (CLI present, key set, …).
- public `AvailabilityMessage(OperatorSettings settings) → string` — Human-readable explanation when IsAvailable is false.
- private `IsPayIntent(AiTriageRequest request) → bool` — Job boards and hiring threads: the poster is already committed to paying.
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage.
- private `DetectLanguage(string text) → string` — Handles detect language. _(inferred)_
- private `Classify(string text) → string` — Handles classify. _(inferred)_
- private `DetectStack(string text) → IEnumerable<string>` — Handles detect stack. _(inferred)_
- private `AddIf(string text, HashSet<string> stack, string needle, string label) → void` — Creates if. _(inferred)_
- private `BuildCause(string category) → string` — Creates cause. _(inferred)_
- private `BuildStep(string category) → string` — Creates step. _(inferred)_

#### OpenCodeTriageProvider

public class `OpenCodeTriageProvider` : `IAiTriageProvider`, `IAiBatchShortlistProvider`, `IAiBatchTriageProvider` · `src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs:17`

Default AI provider: runs the single-pass structured triage through the local `opencode` CLI (https://opencode.ai). The CLI brings its own provider/model configuration, so triage works with whatever the operator has…

Depends on: `ILogger<OpenCodeTriageProvider> log`.

Data: `_log`: ILogger<OpenCodeTriageProvider>, `ProbeLock`: object, `_probedPath`: string?, `_probeResult`: bool, `_probeMessage`: string, `Name`: string, `FallbackModels`: string[], `_lastWorkingFallback`: string?, `ParseOptions`: JsonSerializerOptions.

- public `IsAvailable(OperatorSettings settings) → bool` — Whether the provider can currently make calls (CLI present, key set, …).
- public `AvailabilityMessage(OperatorSettings settings) → string` — Human-readable explanation when IsAvailable is false.
- public `TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) → Task<AiTriageResponse>` — Coordinates triage.
- public `TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) → Task<AiBatchTriageResponse>` — Coordinates batch.
- public `ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) → Task<AiShortlistResponse>` — Handles shortlist.
- public `GenerateTextAsync(string prompt, OperatorSettings settings, TimeSpan timeout, CancellationToken ct) → Task<(bool Succeeded, string Text, string Error, string Model)>` — Generic long-form generation for the content studio: sends one prompt through the CLI and returns the raw (ANSI-stripped) text.
- public `ResolveCliPath(OperatorSettings settings) → string` — Resolves the configured CLI path, falling back to the standard install location.
- private `OnPath(string command) → bool` — Handles on path. _(inferred)_
- private `Probe(string cliPath) → (bool Available, string Message)` — Handles probe. _(inferred)_
- public `ResetProbe() → void` — Clears the cached probe so a changed CLI path takes effect immediately.
- private `RunWithModelFallbackAsync(string cli, string configuredModel, string prompt, TimeSpan timeout, CancellationToken ct) → Task<(int ExitCode, string Stdout, string Stderr, string ModelUsed)>` — Runs the prompt against the configured model, then walks the fallback chain on any non-zero exit. Timeouts propagate immediately (retrying a slow call elsewhere would multiply…
- private `RunCliAsync(string cliPath, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct) → Task<(int ExitCode, string Stdout, string Stderr)>` — Coordinates cli. _(inferred)_
- private `StripAnsi(string text) → string` — Handles strip ansi. _(inferred)_
- public `ExtractJsonObject(string text) → string?` — Extracts the first balanced JSON object from arbitrary CLI output.
- private `IsSchemaValid(AiTriageResult r) → bool` — Checks schema valid. _(inferred)_
- private `Normalize(AiTriageResult r) → void` — Transforms or resolves normalize. _(inferred)_
- private `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_

### `DevLeads.Infrastructure.Connectors`

#### ConnectorSupport

public class `ConnectorSupport` · `src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs:8`

Shared helpers for connectors: content hashing and parameter parsing.

Data: `HttpClientName`: string.

- public `ContentHash(string sourceKey, string externalId, string title) → string` — Stable hash used for duplicate detection across fetches.
- public `NewItem(string sourceKey, string externalId, string title, string body, string url, string? author, string? authorUrl, DateTimeOffset postedAt, string rawJson) → RawSourceItem` — Handles new item. _(inferred)_

#### GitHubSearchConnector

public class `GitHubSearchConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs:15`

Searches public GitHub issues for money-attached work: bounty-platform issues (BountyHub, Algora, IssueHunt and friends all anchor their bounties to GitHub issues) and feature requests where the poster says they'd pay.

Depends on: `IHttpClientFactory httpFactory`, `ILogger<GitHubSearchConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<GitHubSearchConnector>, `SourceKey`: string, `DisplayName`: string.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `ParseIssue(JsonElement issue, SourceConnectorConfig config, bool requireSkillMatch) → RawSourceItem?` — Transforms or resolves issue. _(inferred)_
- private `CreateClient() → HttpClient` — Creates client. _(inferred)_
- private `GetInt(SourceConnectorConfig config, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### HackerNewsConnector

public class `HackerNewsConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs:9`

Discovers founder/operator pain via the Hacker News (Algolia) search API.

Depends on: `IHttpClientFactory httpFactory`, `ILogger<HackerNewsConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<HackerNewsConnector>, `SourceKey`: string, `DisplayName`: string.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `PickSearchTerms(IEnumerable<string> terms, int max) → List<string>` — Handles pick search terms. _(inferred)_
- private `GetInt(SourceConnectorConfig config, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### OpireConnector

public class `OpireConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/OpireConnector.cs:13`

Open bounties from Opire (https://opire.dev): money attached to public GitHub issues, paid out on merge. Public JSON API, no auth.

Depends on: `IHttpClientFactory httpFactory`, `ILogger<OpireConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<OpireConnector>, `SourceKey`: string, `DisplayName`: string.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `ParseReward(JsonElement reward, SourceConnectorConfig config, int minUsd, bool requireSkillMatch) → RawSourceItem?` — Transforms or resolves reward. _(inferred)_
- private `GetInt(SourceConnectorConfig config, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### RedditConnector

public class `RedditConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/RedditConnector.cs:13`

Read-only ingestion of new posts from configured subreddits (manual response preferred). Uses the Atom feeds (new.rss / search.rss) — Reddit's unauthenticated JSON API returns 403 from datacenter IPs, while the RSS…

Depends on: `IHttpClientFactory httpFactory`, `ILogger<RedditConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<RedditConnector>, `SourceKey`: string, `DisplayName`: string, `Atom`: XNamespace.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `FetchListingAsync(HttpClient http, string url, string sub, string sourceKey, Dictionary<string, RawSourceItem> items, int maxItems, DateTimeOffset cutoff, CancellationToken ct, Func<string, bool> titleFilter) → Task<bool>` — Fetches one subreddit feed. Returns false when rate-limited (callers stop the run).
- private `StripHtml(string html) → string` — Handles strip html. _(inferred)_
- private `PickSearchTerms(IEnumerable<string> terms, int max) → List<string>` — Handles pick search terms. _(inferred)_
- private `GetInt(SourceConnectorConfig config, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### RemotiveConnector

public class `RemotiveConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs:13`

Real companies posting paid remote software work via the Remotive job API. Defaults to contract/freelance software roles — businesses actively hiring and willing to spend money — which the pre-filter and AI triage then…

Depends on: `IHttpClientFactory httpFactory`, `ILogger<RemotiveConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<RemotiveConnector>, `SourceKey`: string, `DisplayName`: string, `DevRoleSignals`: string[].

- private `LooksLikeDevRole(string title, string tags) → bool` — Handles looks like dev role. _(inferred)_
- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `StripHtml(string html) → string` — Handles strip html. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### RssConnector

public class `RssConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/RssConnector.cs:10`

Simple, reliable ingestion of RSS / Atom feeds configured per source.

Depends on: `IHttpClientFactory httpFactory`, `ILogger<RssConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<RssConnector>, `SourceKey`: string, `DisplayName`: string.

- private `Feeds(SourceConnectorConfig config) → string[]` — Handles feeds. _(inferred)_
- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `StripHtml(string html) → string` — Handles strip html. _(inferred)_
- private `EnrichForumThreadAsync(HttpClient http, RawSourceItem item, CancellationToken ct) → Task` — Handles enrich forum thread. _(inferred)_
- private `HasTruthyProperty(JsonElement root, string name) → bool` — Checks truthy property. _(inferred)_
- private `GetDate(JsonElement root, string name) → DateTimeOffset?` — Loads or resolves date. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

#### StackExchangeConnector

public class `StackExchangeConnector` : `ISourceConnector` · `src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs:9`

Fresh technical problem detection via the Stack Exchange API.

Depends on: `IHttpClientFactory httpFactory`, `ILogger<StackExchangeConnector> log`.

Data: `_httpFactory`: IHttpClientFactory, `_log`: ILogger<StackExchangeConnector>, `SourceKey`: string, `DisplayName`: string.

- public `FetchAsync(SourceConnectorConfig config, CancellationToken ct) → Task<IReadOnlyList<RawSourceItem>>` — Loads or resolves fetch.
- private `StripHtml(string html) → string` — Handles strip html. _(inferred)_
- private `PickSearchTerms(IEnumerable<string> terms, int max) → List<string>` — Handles pick search terms. _(inferred)_
- private `GetInt(SourceConnectorConfig config, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(CancellationToken ct) → Task<ConnectorHealth>` — Checks health.

### `DevLeads.Infrastructure.Data`

#### DatabaseSeeder

public class `DatabaseSeeder` · `src/DevLeads.Infrastructure/Data/DatabaseSeeder.cs:14`

Creates the database and seeds query packs, source configs, and settings. Also migrates older databases: removes retired sources (GitHub Issues) and purges leads that cannot lead to paid work.

Data: `LegacyPackNames`: Dictionary<string, string>, `EmergencyCampaignKey`: string, `ModernizationCampaignKey`: string, `AiAutomationCampaignKey`: string, `EngagedStatuses`: OpportunityStatus[].

- public `InitializeAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Coordinates initialize. _(inferred)_
- private `RequeueTemplateDraftsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — One-time (2026-07-11): unapproved template mad-lib drafts ("I saw your post about [title]…") are moved into the AI generation queue so the batched generator rewrites them…
- private `ApplyStackIdentityCapsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Applies the stack-identity score cap (50, below Medium) to leads scored before the gate existed, so off-stack posts stop outranking.NET work without waiting for a re-triage.
- private `DemoteGenericCapabilitySkillsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — One-time data fix (2026-07-11): "REST API" was seeded as a weight-3 "Primary stack" skill, which made every Go/Python job post score as a core.NET fit.
- private `PurgeForeignStackLeadsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Removes discovery leads that demand a stack outside the operator's profile without touching the operator's own stack (Go/Python/Java job posts etc.) — they scored high on pay…
- private `ApplySchemaUpgradesAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — EnsureCreated never alters existing tables, so columns added after first release are applied here with idempotent ALTERs (SQLite raises on duplicates — ignored).
- private `MigrateAiProviderDefaultsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Moves settings still on an old AI default onto the current one (OpenCode CLI). Explicit operator choices (anything not matching an old default) are untouched.
- private `SeedQueryPacksAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Creates query packs. _(inferred)_
- private `SeedSkillsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Seeds the operator skill profile once; the Skills page owns it afterwards.
- private `SeedCampaignsAsync(DevLeadsDbContext db, CancellationToken ct) → Task<Dictionary<string, long>>` — Ensures the built-in campaigns exist (add-only: name/objective edits belong to the operator afterwards). Returns campaign ids keyed by stable campaign key.
- private `SeedSourceConfigsAsync(DevLeadsDbContext db, Dictionary<string, long> campaigns, CancellationToken ct) → Task<bool>` — Creates source configs. _(inferred)_
- private `BackfillLeadCampaignsAsync(DevLeadsDbContext db, long emergencyId, CancellationToken ct) → Task` — Assigns campaign-less leads to their source's campaign (manual/unknown → emergency).
- private `IsLegacyDefaultSource(SourceConfig source) → bool` — Detects configs still carrying earlier seeded defaults so we can upgrade them in place.
- private `IsInitialAiTopicGate(SourceConfig source) → bool` — Checks initial ai topic gate. _(inferred)_
- private `ApplySourceDefaults(SourceConfig target, SourceConfig seed) → bool` — Reapplies seeded defaults, returning whether anything actually changed — a boot with unchanged defaults must NOT count as a migration (that would purge + re-triage all discovery…
- private `IsAdditiveHiringSubredditExpansion(SourceConfig target, SourceConfig seed) → bool` — Checks additive hiring subreddit expansion. _(inferred)_
- private `DefaultSources(long emergencyCampaignId, long modernizationCampaignId, long aiAutomationCampaignId) → IEnumerable<SourceConfig>` — Handles default sources. _(inferred)_
- private `EmergencySources() → IEnumerable<SourceConfig>` — Default sources are chosen for commercial intent: places where a business owner, manager, or agency is describing a problem they are prepared to pay to solve.
- private `ModernizationSources() → IEnumerable<SourceConfig>` — Sources for the.NET legacy modernization consulting campaign. Feeds/queries are chosen to be disjoint from the emergency sources where possible; when a post qualifies for both…
- private `AiAutomationSources() → IEnumerable<SourceConfig>` — Searches every registered connector for paid AI/automation implementation work. Topic matching finds the project; campaign-aware AI triage still requires explicit hire/pay intent…
- private `SeedTrendSourcesAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Content-studio trend sources (add-only; the operator owns them afterwards). Feed URLs verified live 2026-07-11.
- private `RssParams(string daysBack, string[] feeds, string? triageProvider, string? requiredQueryPack) → string` — Handles rss params. _(inferred)_
- private `RemoveRetiredSourcesAsync(DevLeadsDbContext db, CancellationToken ct) → Task<bool>` — Deletes retired source configs and every item/lead they produced (e.g. GitHub Issues).
- private `RemoveReplacedSourceConfigsAsync(DevLeadsDbContext db, CancellationToken ct) → Task<bool>` — Removes old broad source config rows after splitting them into tuned variants.
- private `PurgeStaleDiscoveryLeadsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — One-time after a source-lineup migration: leads still sitting in triage stages were collected under the old, lower-quality configuration — drop them (manual entries and…
- private `PurgeNonActionableLeadsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Purges leads that will not lead to financial compensation: pre-filter rejects, triage rejects, do-not-contact posts, and non-urgent/irrelevant help requests.
- private `PurgeNonHirableVendorSupportLeadsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Removes or transitions non hirable vendor support leads. _(inferred)_
- private `PurgeSourceLessLeadsAsync(DevLeadsDbContext db, CancellationToken ct) → Task` — Every visible opportunity must point back to its original public source.
- private `DeleteLeadsKeepDedupAsync(DevLeadsDbContext db, List<Opportunity> leads, CancellationToken ct) → Task` — Removes lead rows while detaching raw items, so dedup never re-ingests the same post.

#### DevLeadsDbContext

public class `DevLeadsDbContext` : `DbContext` · `src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:9`

EF Core context for the SQLite solo database.

Depends on: `DbContextOptions<DevLeadsDbContext> options`.

Data: `Opportunities`: DbSet<Opportunity>, `RawSourceItems`: DbSet<RawSourceItem>, `AiTriageRuns`: DbSet<AiTriageRun>, `OutreachAttempts`: DbSet<OutreachAttempt>, `Quotes`: DbSet<Quote>, `WorkSessions`: DbSet<WorkSession>, `SuppressionEntries`: DbSet<SuppressionEntry>, `AuditEvents`: DbSet<AuditEvent>, `SourceConfigs`: DbSet<SourceConfig>, `QueryPacks`: DbSet<QueryPack>, `OperatorSettings`: DbSet<OperatorSettings>, `Skills`: DbSet<Skill>, `Campaigns`: DbSet<Campaign>, `TrendSources`: DbSet<TrendSource>, `TrendSignals`: DbSet<TrendSignal>, `ContentTopics`: DbSet<ContentTopic>, `ContentDrafts`: DbSet<ContentDraft>, `OperatorPosts`: DbSet<OperatorPost>, `OperatorPostSnapshots`: DbSet<OperatorPostSnapshot>, `OperatorMessages`: DbSet<OperatorMessage>, `OperatorPostRevisions`: DbSet<OperatorPostRevision>.

- protected `ConfigureConventions(ModelConfigurationBuilder b) → void` — Handles configure conventions. _(inferred)_
- protected `OnModelCreating(ModelBuilder mb) → void` — Handles on model creating. _(inferred)_

#### DateTimeOffsetToTicksConverter

private class `DateTimeOffsetToTicksConverter` : `ValueConverter<DateTimeOffset, long>` · `src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:37`

Represents date time offset to ticks converter. _(inferred)_

### `DevLeads.Infrastructure`

#### DependencyInjection

public class `DependencyInjection` · `src/DevLeads.Infrastructure/DependencyInjection.cs:17`

Represents dependency injection. _(inferred)_

- public `AddDevLeads(this IServiceCollection services, string connectionString) → IServiceCollection` — Registers the database, connectors, AI providers, domain services, and worker.
- public `InitializeDevLeadsAsync(this IServiceProvider services) → Task` — Creates the database schema and seeds default settings, query packs, and sources.

### `DevLeads.Infrastructure.QueryPacks`

#### DbQueryPackProvider

public class `DbQueryPackProvider` : `IQueryPackProvider` · `src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs:7`

Loads query-pack terms from the database (cached per scope).

Depends on: `DevLeadsDbContext db`.

Data: `_db`: DevLeadsDbContext, `_cache`: List<Core.Entities.QueryPack>?, `Packs`: List<Core.Entities.QueryPack>.

- private `Split(string terms) → string[]` — Transforms or resolves split. _(inferred)_
- public `GetTerms(string packName) → IReadOnlyList<string>` — Returns the terms for a named pack (empty if unknown).
- public `GetHighPriorityTerms() → IReadOnlyList<string>` — All high-priority emergency terms across packs.
- public `GetHighPriorityTerms(IReadOnlyCollection<string> packNames) → IReadOnlyList<string>` — All high-priority emergency terms across packs.
- public `GetNegativeTerms() → IReadOnlyList<string>` — All negative / exclusion terms.

### `DevLeads.Infrastructure.Services`

#### AuditService

public class `AuditService` · `src/DevLeads.Infrastructure/Services/AuditService.cs:8`

Writes audit-trail entries for generated/sent messages and state changes.

Depends on: `DevLeadsDbContext db`.

Data: `_db`: DevLeadsDbContext.

- public `Record(string entityType, long entityId, string eventType, string description, string actor, object? metadata) → void` — Handles record. _(inferred)_

#### ContentStudioService

public class `ContentStudioService` · `src/DevLeads.Infrastructure/Services/ContentStudioService.cs:18`

Turns trend signals into publishable output: AI-suggested topics, then full drafts (blog posts, articles, white/research papers, LinkedIn posts) for the operator to edit and publish on their own channels.

Depends on: `DevLeadsDbContext db`, `AiTextRouter text`, `AuditService audit`, `DiscoveryActivityTracker activity`, `ILogger<ContentStudioService> log`.

Data: `_db`: DevLeadsDbContext, `_text`: AiTextRouter, `_audit`: AuditService, `_activity`: DiscoveryActivityTracker, `_log`: ILogger<ContentStudioService>.

- public `GenerateTopicsAsync(int maxTopics, CancellationToken ct) → Task<(int Created, string Message)>` — One AI call: distills the hottest recent signals into up to maxTopics new topic suggestions. Returns (created, message).
- public `GenerateDraftAsync(long topicId, ContentFormat format, CancellationToken ct) → Task<(ContentDraft? Draft, string Message)>` — One AI call: writes a full draft for a topic in the requested format.
- private `SplitTitle(string markdown, string fallbackTitle) → (string Title, string Body)` — The output contract is "# Title" on line one; fall back to the topic title.
- private `Spaced(ContentFormat format) → string` — Handles spaced. _(inferred)_
- private `GetSettingsAsync(CancellationToken ct) → Task<OperatorSettings>` — Loads or resolves settings. _(inferred)_

#### TopicOutput

private class `TopicOutput` · `src/DevLeads.Infrastructure/Services/ContentStudioService.cs:199`

Represents topic output. _(inferred)_

Data: `Topics`: List<TopicSuggestion>.

#### TopicSuggestion

private class `TopicSuggestion` · `src/DevLeads.Infrastructure/Services/ContentStudioService.cs:204`

Represents topic suggestion. _(inferred)_

Data: `Title`: string, `Angle`: string?, `Rationale`: string?, `InterestScore`: double, `Skills`: List<string>?, `Formats`: List<string>?, `Evidence`: List<int>?.

#### DiscoveryActivityTracker

public class `DiscoveryActivityTracker` · `src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:8`

In-memory, app-wide record of what discovery is doing right now: which sources are mid-fetch and a rolling feed of recent events (runs, new leads, failures).

Data: `MaxEvents`: int, `_gate`: object, `_events`: LinkedList<ActivityEvent>, `_running`: Dictionary<string, RunningSource>.

- public `RunStarted(string sourceKey, string displayName) → void` — Coordinates started. _(inferred)_
- public `RunCompleted(string sourceKey, bool healthy, string message) → void` — Coordinates completed. _(inferred)_
- public `LeadCreated(string sourceKey, string title, double score) → void` — Handles lead created. _(inferred)_
- public `Snapshot() → (IReadOnlyList<RunningSource> Running, IReadOnlyList<ActivityEvent> Events)` — Handles snapshot. _(inferred)_
- private `AddLocked(string kind, string sourceKey, string message) → void` — Creates locked. _(inferred)_

#### ActivityEvent

public record class `ActivityEvent` · `src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:10`

Represents activity event. _(inferred)_

Depends on: `DateTimeOffset At`, `string Kind`, `string SourceKey`, `string Message`.

#### RunningSource

public record class `RunningSource` · `src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:11`

Represents running source. _(inferred)_

Depends on: `string SourceKey`, `string DisplayName`, `DateTimeOffset StartedAt`.

#### LeadIngestionService

public class `LeadIngestionService` · `src/DevLeads.Infrastructure/Services/LeadIngestionService.cs:20`

The two-step triage funnel: heuristic pre-filter, then (for survivors) a single structured AI call, followed by weighted scoring and optional draft generation. This is the pipeline heart described in the design document.

Depends on: `DevLeadsDbContext db`, `HeuristicPreFilter preFilter`, `AiTriageRouter ai`, `AuditService audit`, `ILogger<LeadIngestionService> log`.

Data: `_db`: DevLeadsDbContext, `_preFilter`: HeuristicPreFilter, `_ai`: AiTriageRouter, `_audit`: AuditService, `_log`: ILogger<LeadIngestionService>, `_skillsCache`: List<Skill>?, `_campaignObjectiveCache`: Dictionary<long, string>.

- public `IngestAsync(RawSourceItem item, SourceConfig source, CancellationToken ct, AiTriageResponse? precomputedTriage) → Task<Opportunity?>` — Runs a discovered item through the full pipeline. Returns null if a duplicate. precomputedTriage carries this item's result from a batched AI call so no further per-item AI call…
- public `RecordRawOnlyAsync(RawSourceItem item, CancellationToken ct) → Task<bool>` — Records a fetched item as seen without creating an opportunity. Used when a batch shortlist decides the item is not worth a full AI triage call.
- public `CreateManualAsync(string title, string body, string sourceUrl, string? author, string? authorUrl, CancellationToken ct, long? campaignId) → Task<Opportunity>` — Manual lead entry that still runs the pre-filter, AI triage, and scoring.
- public `RerunAsync(long opportunityId, CancellationToken ct) → Task` — Re-runs triage + scoring for an existing opportunity (used by the "rerun" endpoint).
- private `RunTriageScoreAndDraftAsync(Opportunity opp, PreFilterResult pre, OperatorSettings settings, SourceConfig? source, CancellationToken ct, AiTriageResponse? precomputed) → Task` — Coordinates triage score and draft. _(inferred)_
- private `DecideStatusAndDraft(Opportunity opp, AiTriageResult? ai, RedFlagResult redFlag, OperatorSettings settings, SourceConfig? source, string body, IReadOnlyList<SkillMatch>? skillMatches, IReadOnlyList<string> foreignStacks) → void` — Handles decide status and draft. _(inferred)_
- private `CreateDraft(Opportunity opp, AiTriageResult ai, OperatorSettings settings) → void` — Creates draft. _(inferred)_
- private `ApplyPreFilter(Opportunity opp, PreFilterResult pre) → void` — Updates pre filter. _(inferred)_
- private `ApplyAiResult(Opportunity opp, AiTriageResult ai) → void` — Updates ai result. _(inferred)_
- private `ApplyEnglishTranslationIfRetained(Opportunity opp, AiTriageResult? ai) → void` — Updates english translation if retained. _(inferred)_
- private `ApplyScore(Opportunity opp, ScoreBreakdown s) → void` — Updates score. _(inferred)_
- public `MapRecommendation(string value) → OutreachRecommendation` — Transforms or resolves recommendation. _(inferred)_
- private `GetSettingsAsync(CancellationToken ct) → Task<OperatorSettings>` — Loads or resolves settings. _(inferred)_
- private `GetSkillsAsync(CancellationToken ct) → Task<List<Skill>>` — Loads or resolves skills. _(inferred)_
- private `GetCampaignObjectiveAsync(long? campaignId, CancellationToken ct) → Task<string>` — Loads or resolves campaign objective. _(inferred)_
- private `PackNames(SourceConfig source) → string[]` — Handles pack names. _(inferred)_
- private `ResolveTriageSettings(OperatorSettings settings, SourceConfig? source) → OperatorSettings` — Transforms or resolves triage settings. _(inferred)_
- private `CloneWithProvider(OperatorSettings settings, string provider) → OperatorSettings` — Handles clone with provider. _(inferred)_
- public `IsOverAiBudgetAsync(OperatorSettings settings, CancellationToken ct) → Task<bool>` — True when the count of real (non-heuristic) AI calls in the last hour has hit the cap. Batched runs record one row per lead but share one model call, so they count as ceil(rows /…
- private `GetSourceParameter(SourceConfig? source, string key) → string?` — Loads or resolves source parameter. _(inferred)_
- private `GetBodyAsync(Opportunity opp, CancellationToken ct) → Task<string>` — Loads or resolves body. _(inferred)_
- private `DeserializeList(string json) → List<string>` — Handles deserialize list. _(inferred)_
- private `NormalizeSourceUrl(string? sourceUrl) → string?` — Transforms or resolves source url. _(inferred)_
- private `FindNearDuplicateOpportunityAsync(RawSourceItem item, string sourceUrl, CancellationToken ct) → Task<Opportunity?>` — Loads or resolves near duplicate opportunity. _(inferred)_
- private `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_

#### MaintenanceService

public class `MaintenanceService` · `src/DevLeads.Infrastructure/Services/MaintenanceService.cs:9`

Periodic housekeeping: stale-lead archiving and overdue-quote flagging.

Depends on: `DevLeadsDbContext db`, `AuditService audit`.

Data: `_db`: DevLeadsDbContext, `_audit`: AuditService.

- public `ArchiveStaleLeadsAsync(CancellationToken ct) → Task<int>` — Removes or transitions stale leads. _(inferred)_
- public `RejectNonHirableVendorSupportAsync(CancellationToken ct) → Task<int>` — Removes or transitions non hirable vendor support. _(inferred)_
- public `FlagOverdueQuotesAsync(CancellationToken ct) → Task<int>` — Handles flag overdue quotes. _(inferred)_
- public `DueFollowUpCountAsync(CancellationToken ct) → Task<int>` — Count of opportunities whose follow-up is now due (surfaced on the dashboard).

#### OperatorPostService

public class `OperatorPostService` · `src/DevLeads.Infrastructure/Services/OperatorPostService.cs:20`

Syncs the operator's OWN reddit posts into "My posts" and tracks their reply counts over time. Uses reddit's anonymous RSS endpoints with 6s pacing (the JSON API 403s from this host and the per-IP RSS budget bursts 429…

Depends on: `DevLeadsDbContext db`, `IHttpClientFactory httpFactory`, `AiTextRouter text`, `DiscoveryActivityTracker activity`, `AuditService audit`, `ILogger<OperatorPostService> log`.

Data: `Atom`: XNamespace, `RequestPacing`: TimeSpan, `JobCommunities`: string[], `_db`: DevLeadsDbContext, `_httpFactory`: IHttpClientFactory, `_text`: AiTextRouter, `_activity`: DiscoveryActivityTracker, `_audit`: AuditService, `_log`: ILogger<OperatorPostService>, `_cachedToken`: string?, `_tokenExpiresAt`: DateTimeOffset, `TokenLock`: SemaphoreSlim.

- public `GeneratePostAsync(string platform, long? campaignId, string extraInstructions, CancellationToken ct) → Task<(OperatorPost? Post, string Message)>` — One AI call drafts a platform-appropriate post (reddit/craigslist/linkedin/upwork/ gmail template) in the operator's real identity, using the best-performing tracked posts as…
- private `SplitTitle(string text) → (string Title, string Body)` — Transforms or resolves title. _(inferred)_
- public `SyncRedditAsync(bool jobPostsOnly, CancellationToken ct) → Task<(int Imported, int Refreshed, string Message)>` — Imports the account's submitted posts and refreshes reply counts on tracked ones. jobPostsOnly keeps personal posts out (default): a post imports when its subreddit is…
- private `ImportSubmittedAsync(HttpClient http, string username, bool jobPostsOnly, CancellationToken ct) → Task<int>` — Handles import submitted. _(inferred)_
- public `HasApiCredentials(OperatorSettings s) → bool` — Checks api credentials. _(inferred)_
- private `GetAccessTokenAsync(OperatorSettings settings, string username, CancellationToken ct) → Task<string>` — Loads or resolves access token. _(inferred)_
- private `SyncViaApiAsync(OperatorSettings settings, string username, bool jobPostsOnly, CancellationToken ct) → Task<(int Imported, int Refreshed, int ViewsReported)>` — Authenticated sync combines the submitted listing with one batched /api/info request. Reddit may still omit view_count; ViewCountKnown records that distinction.
- private `GetPostDetailsAsync(HttpClient http, string token, IEnumerable<string> fullnames, CancellationToken ct) → Task<Dictionary<string, System.Text.Json.JsonElement>>` — Gets the fullest available representation for up to 100 posts in one official API call.
- private `TryGetNonNegativeInt(System.Text.Json.JsonElement data, string name, out int value) → bool` — Handles try get non negative int. _(inferred)_
- public `SyncRedditInboxAsync(CancellationToken ct) → Task<(int Imported, string Message)>` — Syncs the account's reddit inbox — DMs plus comment/post replies — into tracked messages. Prefers the authenticated API (full listing JSON with unread flags); otherwise uses the…
- public `MarkMessageReadAsync(long messageId, CancellationToken ct) → Task<bool>` — Marks a message read locally as soon as it is opened. For Reddit messages, also makes a best-effort official /api/read_message call when OAuth credentials exist.
- private `UpsertInboxListingAsync(string json, CancellationToken ct) → Task<int>` — Handles upsert inbox listing. _(inferred)_
- private `GetString(System.Text.Json.JsonElement e, string name) → string` — Loads or resolves string. _(inferred)_
- private `UpsertInboxFeedAsync(string xml, CancellationToken ct) → Task<int>` — Anonymous fallback: the private inbox feed as Atom. Entry ids are fullnames (t4_/t1_), titles read "from X via sub sent N ago: post reply" for replies (the DM subject for t4s)…
- private `ParseFeedSubject(string title) → string` — Drops the "from X via sub sent N ago:" feed prefix, keeping the subject/label.
- private `ExtractFeedBody(string html) → string` — Keeps only the message markdown (between reddit's SC_OFF/SC_ON markers), dropping the header line.
- private `BackfillCrossPostBodiesAsync(CancellationToken ct) → Task` — A cross-posted ad gets its full selftext in only ONE feed entry ("submitted by" stubs elsewhere). The copies share a title, so the longest same-title body is the authoritative…
- public `SummarizeThreadAsync(long postId, CancellationToken ct) → Task<(bool Ok, string Message)>` — Pulls the post plus every visible reply, then one AI call summarizes the thread's main points and suggests how to move forward as the original poster.
- public `OptimizePostsAsync(IReadOnlyList<long> postIds, string extraInstructions, CancellationToken ct) → Task<(int Created, string Message)>` — One batched AI call rewrites each selected post with a DISTINCT strategy and saves them as Proposed revisions (nothing goes live: the operator applies each one on the platform…
- public `ApplyRevisionAsync(long revisionId, CancellationToken ct) → Task<(bool Ok, string Message)>` — Marks a proposed rewrite as live: freezes the pre-change baseline (counts and views/day) and snapshots the change moment for the graph.
- private `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_
- private `IsJobPost(string title, string community) → bool` — Checks job post. _(inferred)_
- private `RefreshRepliesAsync(HttpClient http, CancellationToken ct) → Task<int>` — Refreshes reply counts on tracked reddit posts (stalest first, paced at 6s per request, capped per run to protect the reddit RSS budget). Snapshots every change.
- private `StripHtml(string html) → string` — Handles strip html. _(inferred)_
- private `StripBoilerplate(string text) → string` — Removes reddit's "submitted by /u/… [link] [comments]" feed footer.

#### OutreachService

public class `OutreachService` · `src/DevLeads.Infrastructure/Services/OutreachService.cs:19`

Manages the human-in-the-loop outreach queue: drafts, approvals, sends, and suppression. Sending never bypasses the kill switch, suppression list, or approval requirement.

Depends on: `DevLeadsDbContext db`, `AuditService audit`, `AiTextRouter text`, `DiscoveryActivityTracker activity`.

Data: `_db`: DevLeadsDbContext, `_audit`: AuditService, `_text`: AiTextRouter, `_activity`: DiscoveryActivityTracker, `QueuedPlaceholder`: string, `GenerationChunkSize`: int.

- public `QueueForGenerationAsync(long opportunityId, CancellationToken ct) → Task<OutreachAttempt>` — Adds a lead to the AI generation queue. Idempotent: an existing queued/pending/ approved attempt for the same lead is returned instead of duplicated.
- public `QueuedCountAsync(CancellationToken ct) → Task<int>` — Count of attempts currently waiting in the generation queue.
- public `GenerateQueuedResponsesAsync(CancellationToken ct) → Task<(int Generated, string Message)>` — Writes every queued reply in a single model call (chunked only past GenerationChunkSize items as a prompt-size guard).
- private `ParseResponses(string json) → Dictionary<string, string>` — Transforms or resolves responses. _(inferred)_
- public `GenerateDraftAsync(long opportunityId, string templateKey, CancellationToken ct) → Task<OutreachAttempt>` — Creates draft. _(inferred)_
- public `ApproveAsync(long attemptId, CancellationToken ct) → Task` — Handles approve. _(inferred)_
- public `SendAsync(long attemptId, CancellationToken ct) → Task<(bool Sent, string Message)>` — "Sends" the outreach. In this MVP this records a Gmail draft / manual send and marks the opportunity contacted; it never transmits automatically without passing all safety gates.
- public `CancelAsync(long attemptId, CancellationToken ct) → Task` — Removes or transitions cancel. _(inferred)_
- public `MarkRespondedAsync(long opportunityId, CancellationToken ct) → Task` — Updates responded. _(inferred)_
- public `IsSuppressedAsync(string contact, CancellationToken ct) → Task<bool>` — Checks suppressed. _(inferred)_
- public `AddSuppressionAsync(string contact, SuppressionContactType type, string reason, CancellationToken ct) → Task` — Creates suppression. _(inferred)_
- private `GetSettings(CancellationToken ct) → Task<OperatorSettings>` — Loads or resolves settings. _(inferred)_
- private `Get(long id, CancellationToken ct) → Task<OutreachAttempt>` — Loads or resolves get. _(inferred)_

#### QuoteService

public class `QuoteService` · `src/DevLeads.Infrastructure/Services/QuoteService.cs:10`

Quote generation and payment-state tracking for bounded emergency fixes.

Depends on: `DevLeadsDbContext db`, `AuditService audit`.

Data: `_db`: DevLeadsDbContext, `_audit`: AuditService.

- public `GenerateAsync(long opportunityId, double? amount, bool dueOnCompletion, CancellationToken ct) → Task<Quote>` — Creates generate. _(inferred)_
- public `SendAsync(long quoteId, CancellationToken ct) → Task` — Handles send. _(inferred)_
- public `MarkPaidAsync(long quoteId, CancellationToken ct) → Task` — Updates paid. _(inferred)_
- public `MarkOverdueAsync(long quoteId, CancellationToken ct) → Task` — Updates overdue. _(inferred)_
- private `Get(long id, CancellationToken ct) → Task<Quote>` — Loads or resolves get. _(inferred)_

#### SourceRunner

public class `SourceRunner` · `src/DevLeads.Infrastructure/Services/SourceRunner.cs:15`

Runs one source end-to-end: fetch via its connector, then ingest each item.

Depends on: `DevLeadsDbContext db`, `IEnumerable<ISourceConnector> connectors`, `IQueryPackProvider queryPacks`, `LeadIngestionService ingestion`, `HeuristicPreFilter preFilter`, `AiTriageRouter ai`, `DiscoveryActivityTracker activity`, `ILogger<SourceRunner> log`.

Data: `_db`: DevLeadsDbContext, `_connectors`: IEnumerable<ISourceConnector>, `_queryPacks`: IQueryPackProvider, `_ingestion`: LeadIngestionService, `_preFilter`: HeuristicPreFilter, `_ai`: AiTriageRouter, `_activity`: DiscoveryActivityTracker, `_log`: ILogger<SourceRunner>.

- public `RunAsync(SourceConfig source, CancellationToken ct) → Task<int>` — Fetches and ingests for a single source config. Returns the number of new opportunities.
- private `GetSeenExternalIdsAsync(string sourceKey, IReadOnlyList<RawSourceItem> items, CancellationToken ct) → Task<HashSet<string>>` — Which of these items' external ids are already recorded for this source.
- private `BatchTriageAsync(IReadOnlyList<RawSourceItem> newItems, IReadOnlyDictionary<RawSourceItem, PreFilterResult> preFilterByItem, ShortlistGate shortlist, SourceConfig source, IReadOnlyDictionary<string, string> parameters, OperatorSettings settings, string campaignObjective, CancellationToken ct) → Task<Dictionary<RawSourceItem, AiTriageResponse>>` — Triages the shortlist survivors in chunked batch calls (one model call per AiTriageRouter.BatchTriageChunkSize items) so ingestion doesn't spend a call per lead.
- private `BuildShortlistGateAsync(IReadOnlyList<RawSourceItem> items, IReadOnlyDictionary<RawSourceItem, PreFilterResult> preFilterByItem, SourceConfig source, IReadOnlyDictionary<string, string> parameters, OperatorSettings settings, string campaignObjective, CancellationToken ct) → Task<ShortlistGate>` — Creates shortlist gate. _(inferred)_
- private `ShouldUseBatchShortlist(IReadOnlyDictionary<string, string> parameters, string provider) → bool` — Checks use batch shortlist. _(inferred)_
- private `ResolveShortlistMax(IReadOnlyDictionary<string, string> parameters, int candidateCount) → int` — Transforms or resolves shortlist max. _(inferred)_
- private `ResolveTriageProvider(IReadOnlyDictionary<string, string> parameters, OperatorSettings settings) → string` — Transforms or resolves triage provider. _(inferred)_
- private `ResolveTriageSettings(OperatorSettings settings, IReadOnlyDictionary<string, string> parameters) → OperatorSettings` — Transforms or resolves triage settings. _(inferred)_
- private `BuildRunMessage(int fetched, int created, int skipped, ShortlistGate shortlist, int shortlistRejected, int skippedByRequiredPack, string? requiredPack) → string` — Creates run message. _(inferred)_
- private `MatchesAny(RawSourceItem item, IReadOnlyCollection<string> terms) → bool` — Handles matches any. _(inferred)_
- private `BuildTerms(SourceConfig source) → IReadOnlyList<string>` — Creates terms. _(inferred)_
- private `PackNames(SourceConfig source) → string[]` — Handles pack names. _(inferred)_
- private `GetCampaignObjectiveAsync(SourceConfig source, CancellationToken ct) → Task<string>` — Loads or resolves campaign objective. _(inferred)_
- private `ParseParameters(string json) → IReadOnlyDictionary<string, string>` — Transforms or resolves parameters. _(inferred)_
- private `GetBool(IReadOnlyDictionary<string, string> parameters, string key, bool fallback) → bool` — Loads or resolves bool. _(inferred)_
- private `GetInt(IReadOnlyDictionary<string, string> parameters, string key, int fallback) → int` — Loads or resolves int. _(inferred)_
- public `CheckHealthAsync(string sourceKey, CancellationToken ct) → Task<ConnectorHealth>` — Checks health. _(inferred)_
- private `ResolveConnectorKey(string sourceKey, IReadOnlyDictionary<string, string> parameters) → string` — Transforms or resolves connector key. _(inferred)_
- private `TrimJsonString(string value) → string` — Transforms or resolves json string. _(inferred)_
- private `Compact(string value, int max) → string` — Transforms or resolves compact. _(inferred)_

#### ShortlistGate

private class `ShortlistGate` · `src/DevLeads.Infrastructure/Services/SourceRunner.cs:442`

Represents shortlist gate. _(inferred)_

Depends on: `bool enabled`, `IReadOnlyDictionary<RawSourceItem, string> candidateIds`, `IReadOnlySet<string> selectedIds`, `int candidateCount`, `string provider`.

Data: `Disabled`: ShortlistGate, `Enabled`: bool, `CandidateIds`: IReadOnlyDictionary<RawSourceItem, string>, `SelectedIds`: IReadOnlySet<string>, `CandidateCount`: int, `SelectedCount`: int, `Provider`: string.

- public `ShouldRecordRawOnly(RawSourceItem item) → bool` — Checks record raw only. _(inferred)_

#### TrendScanService

public class `TrendScanService` · `src/DevLeads.Infrastructure/Services/TrendScanService.cs:16`

Polls trend sources (release feeds, vendor blogs, HN, subreddits) and stores skill-relevant items as TrendSignals ranked by hotness. Reuses the lead connectors for fetching but never touches the lead pipeline.

Depends on: `DevLeadsDbContext db`, `IEnumerable<ISourceConnector> connectors`, `DiscoveryActivityTracker activity`, `ILogger<TrendScanService> log`.

Data: `_db`: DevLeadsDbContext, `_connectors`: IEnumerable<ISourceConnector>, `_activity`: DiscoveryActivityTracker, `_log`: ILogger<TrendScanService>.

- public `RunDueAsync(bool force, CancellationToken ct) → Task<int>` — Runs every enabled trend source that is due. Returns new signal count.
- public `RunSourceAsync(TrendSource source, CancellationToken ct) → Task<int>` — Coordinates source. _(inferred)_
- private `ComputeHotness(IReadOnlyList<SkillMatch> matches, double engagement, DateTimeOffset postedAt, DateTimeOffset now) → double` — Skill relevance dominates; platform engagement and freshness break ties.
- private `ExtractEngagement(string rawJson) → double` — HN Algolia hits carry points/num_comments in the raw JSON; other feeds don't.
- private `GetSeenExternalIdsAsync(string sourceKey, IEnumerable<string> ids, CancellationToken ct) → Task<HashSet<string>>` — Loads or resolves seen external ids. _(inferred)_
- private `PruneOldSignalsAsync(CancellationToken ct) → Task` — Trend evidence goes stale fast; anything past 30 days is dead weight.
- private `ParseParameters(string json) → IReadOnlyDictionary<string, string>` — Transforms or resolves parameters. _(inferred)_
- private `Compact(string value, int max) → string` — Transforms or resolves compact. _(inferred)_

### `DevLeads.Infrastructure.Workers`

#### ContentTrendWorker

public class `ContentTrendWorker` : `BackgroundService` · `src/DevLeads.Infrastructure/Workers/ContentTrendWorker.cs:16`

Slow background loop for the content studio: polls due trend sources (default twice a day per source) and, at most once a day, spends one AI call turning fresh signals into topic suggestions.

Depends on: `IServiceScopeFactory scopeFactory`, `ILogger<ContentTrendWorker> log`.

Data: `_scopeFactory`: IServiceScopeFactory, `_log`: ILogger<ContentTrendWorker>.

- protected `ExecuteAsync(CancellationToken stoppingToken) → Task` — Coordinates execute. _(inferred)_
- private `TickAsync(CancellationToken ct) → Task` — Handles tick. _(inferred)_
- private `MaybeSuggestTopicsAsync(IServiceProvider services, DevLeadsDbContext db, CancellationToken ct) → Task` — One automatic topic-suggestion call per day, and only when there is fresh material to work with — keeps the AI budget impact negligible.

#### DiscoveryWorker

public class `DiscoveryWorker` : `BackgroundService` · `src/DevLeads.Infrastructure/Workers/DiscoveryWorker.cs:15`

The core background loop. Every minute it runs any sources that are due (respecting each source's poll interval), and hourly it runs maintenance.

Depends on: `IServiceScopeFactory scopeFactory`, `ILogger<DiscoveryWorker> log`.

Data: `_scopeFactory`: IServiceScopeFactory, `_log`: ILogger<DiscoveryWorker>, `_lastMaintenance`: DateTimeOffset, `_lastMyPostsSync`: DateTimeOffset, `_lastInboxSync`: DateTimeOffset.

- protected `ExecuteAsync(CancellationToken stoppingToken) → Task` — Coordinates execute. _(inferred)_
- private `TickAsync(CancellationToken ct) → Task` — Handles tick. _(inferred)_

## DevLeads.Web

### `DevLeads.Web.Api`

#### ApiEndpoints

public class `ApiEndpoints` · `src/DevLeads.Web/Api/ApiEndpoints.cs:9`

Internal HTTP API used for automation and integration (the UI calls services directly).

- public `MapDevLeadsApi(this WebApplication app) → void` — Transforms or resolves dev leads api. _(inferred)_
- private `MapStatusAction(RouteGroupBuilder api, string action, OpportunityStatus status) → void` — Transforms or resolves status action. _(inferred)_

#### ManualLeadDto

public record class `ManualLeadDto` · `src/DevLeads.Web/Api/ApiEndpoints.cs:243`

Transfers manual lead data. _(inferred)_

Depends on: `string Title`, `string Body`, `string? SourceUrl`, `string? Author`, `string? AuthorUrl`, `long? CampaignId`.

#### DraftDto

public record class `DraftDto` · `src/DevLeads.Web/Api/ApiEndpoints.cs:244`

Transfers draft data. _(inferred)_

Depends on: `string TemplateKey`.

#### QuoteDto

public record class `QuoteDto` · `src/DevLeads.Web/Api/ApiEndpoints.cs:245`

Transfers quote data. _(inferred)_

Depends on: `double? Amount`, `bool DueOnCompletion`.

### `DevLeads.Web`

#### AppRestartService

public class `AppRestartService` · `src/DevLeads.Web/AppRestartService.cs:12`

Full-process restart so the app picks up the latest code. Spawns a detached supervisor script that waits for this process to exit, rebuilds the project, and relaunches it with the same working directory and environment…

Depends on: `IHostApplicationLifetime lifetime`, `IHostEnvironment env`, `ILogger<AppRestartService> log`.

Data: `_lifetime`: IHostApplicationLifetime, `_env`: IHostEnvironment, `_log`: ILogger<AppRestartService>.

- public `Restart() → string?` — Schedules the restart. Returns an error message, or null when underway.

### `DevLeads.Web.Components`

#### App

public component `App` : `ComponentBase` · `src/DevLeads.Web/Components/App.razor:1`

Blazor component for app.

### `DevLeads.Web.Components.Layout`

#### MainLayout

public component `MainLayout` : `LayoutComponentBase` · `src/DevLeads.Web/Components/Layout/MainLayout.razor:1`

Blazor component for main layout.

#### NavMenu

public component `NavMenu` : `ComponentBase` · `src/DevLeads.Web/Components/Layout/NavMenu.razor:1`

Blazor component for nav menu.

#### ReconnectModal

public component `ReconnectModal` : `ComponentBase` · `src/DevLeads.Web/Components/Layout/ReconnectModal.razor:1`

Blazor component for reconnect modal.

- module `handleReconnectStateChanged() → JavaScript` — Handles handle reconnect state changed. _(inferred)_
- module `retry() → JavaScript` — Handles retry. _(inferred)_
- module `resume() → JavaScript` — Handles resume. _(inferred)_
- module `retryWhenDocumentBecomesVisible() → JavaScript` — Handles retry when document becomes visible. _(inferred)_

### `DevLeads.Web.Components.Pages`

#### Campaigns

public component `Campaigns` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Campaigns.razor:1`

Campaign objectives and source/lead ownership management.

- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `Load() → Task` — Loads or resolves load. _(inferred)_
- private `Save(Campaign edited) → Task` — Updates save. _(inferred)_
- private `Create() → Task` — Creates create. _(inferred)_
- private `Delete(Campaign campaign) → Task` — Removes or transitions delete. _(inferred)_
- private `MakeKey(string name) → string` — Creates key. _(inferred)_

#### Content

public component `Content` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Content.razor:1`

Trend signals, suggested topics, and publishable draft management.

- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `Load() → Task` — Loads or resolves load. _(inferred)_
- private `ScanNow() → Task` — Coordinates now. _(inferred)_
- private `SuggestTopics() → Task` — Handles suggest topics. _(inferred)_
- private `Draft(long topicId, ContentFormat format) → Task` — Handles draft. _(inferred)_
- private `Run(string busyMessage, Func<IServiceProvider, Task<string>> action) → Task` — Coordinates run. _(inferred)_
- private `DismissTopic(long id) → Task` — Removes or transitions topic. _(inferred)_
- private `ToggleEditor(long id) → void` — Handles editor. _(inferred)_
- private `SaveDraft(ContentDraft edited) → Task` — Updates draft. _(inferred)_
- private `SetDraftStatus(ContentDraft edited, ContentDraftStatus status) → Task` — Updates draft status. _(inferred)_
- private `Copy(string text) → Task` — Handles copy. _(inferred)_
- private `DraftChip(ContentDraftStatus s) → string` — Handles draft chip. _(inferred)_
- private `FormatLabel(ContentFormat f) → string` — Transforms or resolves label. _(inferred)_
- private `Shorten(string s) → string` — Handles shorten. _(inferred)_

#### Drafts

public component `Drafts` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Drafts.razor:1`

Outreach generation and human approval queues.

#### Error

public component `Error` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Error.razor:1`

Unhandled-error page.

#### Home

public component `Home` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Home.razor:1`

Campaign-scoped dashboard with lead KPIs, activity, and top opportunities.

- private `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_
- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `IsDashboardLead(Opportunity o, IReadOnlyDictionary<long, string> bodyByOpportunity) → bool` — Checks dashboard lead. _(inferred)_
- private `DashboardDuplicateKey(Opportunity o, IReadOnlyDictionary<long, string> bodyByOpportunity) → string` — Handles dashboard duplicate key. _(inferred)_

#### MyPosts

public component `MyPosts` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/MyPosts.razor:1`

Tracks the operator's posts, platform performance, optimization experiments, and received messages.

- private `if(p.Platform == "reddit" && !_redditApiConfigured) → else` — Handles if. _(inferred)_
- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `DraftWithAi() → Task` — Handles draft with ai. _(inferred)_
- private `Load() → Task` — Loads or resolves load. _(inferred)_
- private `SyncReddit() → Task` — Handles sync reddit. _(inferred)_
- private `ToggleOptimize() → void` — Handles optimize. _(inferred)_
- private `ToggleOptimizeId(long id, ChangeEventArgs e) → void` — Handles optimize id. _(inferred)_
- private `Optimize() → Task` — Handles optimize. _(inferred)_
- private `ApplyRevision(long id) → Task` — Updates revision. _(inferred)_
- private `DismissRevision(long id) → Task` — Removes or transitions revision. _(inferred)_
- private `SaveRevisionNotes(OperatorPostRevision edited) → Task` — Updates revision notes. _(inferred)_
- private `Delta(double before, double after) → string` — Handles delta. _(inferred)_
- private `Truncate(string s, int max) → string` — Handles truncate. _(inferred)_
- private `SyncInbox() → Task` — Handles sync inbox. _(inferred)_
- private `ToggleMessage(OperatorMessage message) → Task` — Handles message. _(inferred)_
- private `AddMessage() → Task` — Creates message. _(inferred)_
- private `SaveMessage(OperatorMessage edited) → Task` — Updates message. _(inferred)_
- private `SetMessageStatus(OperatorMessage edited, OperatorMessageStatus status) → Task` — Updates message status. _(inferred)_
- private `KindLabel(OperatorMessageKind k) → string` — Handles kind label. _(inferred)_
- private `MessageChip(OperatorMessageStatus s) → string` — Handles message chip. _(inferred)_
- private `SaveUsername() → Task` — Updates username. _(inferred)_
- private `AddPost() → Task` — Creates post. _(inferred)_
- private `Toggle(long id) → void` — Handles toggle. _(inferred)_
- private `Summarize(long id) → Task` — Handles summarize. _(inferred)_
- private `Save(OperatorPost edited) → Task` — Updates save. _(inferred)_
- private `SetStatus(OperatorPost edited, OperatorPostStatus status) → Task` — Updates status. _(inferred)_
- private `StatusChip(OperatorPostStatus s) → string` — Handles status chip. _(inferred)_

#### NewOpportunity

public component `NewOpportunity` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/NewOpportunity.razor:1`

Manual lead entry through the normal triage pipeline.

- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `Create() → Task` — Creates create. _(inferred)_

#### NotFound

public component `NotFound` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/NotFound.razor:1`

Missing-route page.

#### Opportunities

public component `Opportunities` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Opportunities.razor:1`

Searchable and filterable lead-review queue.

- private `if(o.PaymentIntent == "Implied") → else` — Handles if. _(inferred)_
- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `SetFilter(string key) → void` — Updates filter. _(inferred)_
- private `Archive(Opportunity o) → Task` — Removes or transitions archive. _(inferred)_
- private `Restore(Opportunity o) → Task` — Handles restore. _(inferred)_
- private `QueueResponse(Opportunity o) → Task` — Handles response. _(inferred)_
- private `SetStatus(Opportunity o, OpportunityStatus status) → Task` — Updates status. _(inferred)_
- private `Apply() → void` — Updates apply. _(inferred)_
- private `Sort(IEnumerable<Opportunity> query) → IEnumerable<Opportunity>` — Handles sort. _(inferred)_
- private `SearchText(Opportunity o) → string` — Handles search text. _(inferred)_
- private `MatchesAny(Opportunity o, params string[] needles) → bool` — Handles matches any. _(inferred)_
- private `Spaced(string value) → string` — Handles spaced. _(inferred)_

#### OpportunityDetail

public component `OpportunityDetail` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/OpportunityDetail.razor:1`

Lead detail, triage, scoring, outreach, quotes, work tracking, and audit history.

- protected `OnParametersSetAsync() → Task` — Runs the component on parameters set lifecycle step. _(inferred)_
- private `Load() → Task` — Loads or resolves load. _(inferred)_
- private `LoadChecklist(WorkSession ws) → void` — Loads or resolves checklist. _(inferred)_
- private `LanguageLabel(string? code) → string` — Handles language label. _(inferred)_
- private `RunScoped(Func<IServiceProvider, Task> action, string okMsg) → Task` — Coordinates scoped. _(inferred)_
- private `Rerun() → Task` — Handles rerun. _(inferred)_
- private `Status(OpportunityStatus status) → Task` — Handles status. _(inferred)_
- private `GenerateDraft() → Task` — Creates draft. _(inferred)_
- private `QueueResponse() → Task` — Handles response. _(inferred)_
- private `SaveDraft(long attemptId, string body) → Task` — Updates draft. _(inferred)_
- private `ApproveDraft(long id) → Task` — Handles draft. _(inferred)_
- private `SendDraft(long id) → Task` — Handles draft. _(inferred)_
- private `CancelDraft(long id) → Task` — Removes or transitions draft. _(inferred)_
- private `GenerateQuote() → Task` — Creates quote. _(inferred)_
- private `SendQuote(long id) → Task` — Handles quote. _(inferred)_
- private `MarkPaid(long id) → Task` — Updates paid. _(inferred)_
- private `StartWork() → Task` — Handles start work. _(inferred)_
- private `ToggleChecklist(string item, bool value) → Task` — Handles checklist. _(inferred)_
- private `CopyText(string text) → Task` — Handles text. _(inferred)_
- private `FixTime() → string` — Handles fix time. _(inferred)_
- private `Bar(string label, double value) → RenderFragment` — Handles bar. _(inferred)_
- private `OppFromOutreach(OutreachStatus s) → OpportunityStatus` — Handles opp from outreach. _(inferred)_

#### Quotes

public component `Quotes` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Quotes.razor:1`

Quote and payment-state management.

#### Settings

public component `Settings` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Settings.razor:1`

Operator, AI, safety, discovery, and restart settings.

- private `RestartServer() → Task` — Handles server. _(inferred)_
- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `Save() → Task` — Updates save. _(inferred)_
- private `OnProviderChanged() → void` — Handles on provider changed. _(inferred)_
- private `OnOpenCodeCliPathChanged() → void` — Handles on open code cli path changed. _(inferred)_
- private `OnCodexCliPathChanged() → void` — Handles on codex cli path changed. _(inferred)_
- private `ModelHint(string provider) → string` — Handles model hint. _(inferred)_
- private `GetFeatureProvider(AiFeature f) → string` — Loads or resolves feature provider. _(inferred)_
- private `SetFeatureProvider(AiFeature f, string? value) → void` — Updates feature provider. _(inferred)_
- private `GetFeatureModel(AiFeature f) → string` — Loads or resolves feature model. _(inferred)_
- private `SetFeatureModel(AiFeature f, string? value) → void` — Updates feature model. _(inferred)_

#### SkillProfile

public component `SkillProfile` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/SkillProfile.razor:1`

Operator skill-profile management.

#### Sources

public component `Sources` : `ComponentBase` · `src/DevLeads.Web/Components/Pages/Sources.razor:1`

Source configuration, health checks, and manual discovery runs.

### `DevLeads.Web.Components`

#### Routes

public component `Routes` : `ComponentBase` · `src/DevLeads.Web/Components/Routes.razor:1`

Blazor component for routes.

### `DevLeads.Web.Components.Shared`

#### ActivityFeed

public component `ActivityFeed` : `ComponentBase`, `IDisposable` · `src/DevLeads.Web/Components/Shared/ActivityFeed.razor:1`

Blazor component for activity feed.

- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- protected `OnAfterRender(bool firstRender) → void` — Runs the component on after render lifecycle step. _(inferred)_
- private `PollLoopAsync() → Task` — Handles poll loop. _(inferred)_
- private `RefreshAsync() → Task` — Handles refresh. _(inferred)_
- private `Elapsed(DateTimeOffset started) → string` — Handles elapsed. _(inferred)_
- private `Due(DateTimeOffset? at) → string` — Handles due. _(inferred)_
- private `KindChip(string kind) → string` — Handles kind chip. _(inferred)_
- private `KindLabel(string kind) → string` — Handles kind label. _(inferred)_
- public `Dispose() → void` — Handles dispose. _(inferred)_

#### CampaignSwitcher

public component `CampaignSwitcher` : `ComponentBase` · `src/DevLeads.Web/Components/Shared/CampaignSwitcher.razor:1`

Blazor component for campaign switcher.

- protected `OnInitializedAsync() → Task` — Runs the component on initialized lifecycle step. _(inferred)_
- private `OnChanged(ChangeEventArgs e) → Task` — Handles on changed. _(inferred)_

#### PostPerformanceChart

public component `PostPerformanceChart` : `ComponentBase` · `src/DevLeads.Web/Components/Shared/PostPerformanceChart.razor:1`

Blazor component for post performance chart.

#### UiHelpers

public class `UiHelpers` · `src/DevLeads.Web/Components/Shared/UiHelpers.cs:8`

Presentation helpers: badge classes, labels, and formatting used across pages.

- public `PriorityClass(Priority p) → string` — Handles priority class. _(inferred)_
- public `StatusClass(OpportunityStatus s) → string` — Handles status class. _(inferred)_
- public `AiStatusClass(AiJobStatus s) → string` — Handles ai status class. _(inferred)_
- public `Spaced(Enum e) → string` — Handles spaced. _(inferred)_
- public `Age(DateTimeOffset from) → string` — Handles age. _(inferred)_
- public `AgeClass(DateTimeOffset from) → string` — Freshness badge: green < 1 day, yellow 1–3 days, red 3+ days.
- public `CompensationOffered(Opportunity o) → bool?` — Did the author indicate they'd pay someone? True on an explicit pay-intent verdict or explicit pay language in the post; false when triage judged intent Implied/None; null when…
- public `YesNo(bool? value) → (string Css, string Label)` — Yes/No/— chip for tri-state judgments.
- public `Fee(double? min, double? max) → string` — Handles fee. _(inferred)_
- public `Fee(Opportunity o) → string` — Fee with provenance: an amount the poster stated is fact ("$15 offered"); a category-based suggestion is clearly marked as an estimate ("~$100–$250 est.").
- public `ParseStringList(string json) → List<string>` — Transforms or resolves string list. _(inferred)_

### `DevLeads.Web.Components`

#### _Imports

public component `_Imports` : `ComponentBase` · `src/DevLeads.Web/Components/_Imports.razor:1`

Blazor component for imports.
