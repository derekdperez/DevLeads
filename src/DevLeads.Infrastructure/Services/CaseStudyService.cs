using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Turns delivered engagements into publishable portfolio case studies: one AI call
/// drafts the study and the testimonial-request message together, grounded strictly in
/// the engagement/work-session record. Studies are operator-edited and only reach the
/// portfolio at Published status.
/// </summary>
public sealed class CaseStudyService
{
    private readonly DevLeadsDbContext _db;
    private readonly AiTextRouter _text;
    private readonly AuditService _audit;
    private readonly ILogger<CaseStudyService> _log;

    public CaseStudyService(DevLeadsDbContext db, AiTextRouter text, AuditService audit,
        ILogger<CaseStudyService> log)
    {
        _db = db;
        _text = text;
        _audit = audit;
        _log = log;
    }

    /// <summary>Drafts a case study from a delivered engagement (one AI call).</summary>
    public async Task<(CaseStudy? Study, string Message)> GenerateDraftAsync(
        long engagementId, bool anonymized, CancellationToken ct)
    {
        var engagement = await _db.Engagements.Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.Id == engagementId, ct);
        if (engagement is null) return (null, "Engagement not found.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                       ?? new OperatorSettings { Id = 1 };
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);

        // The richest ground truth is the work session on the source lead, when one exists.
        WorkSession? work = null;
        if (engagement.OpportunityId is { } oppId)
            work = await _db.WorkSessions.AsNoTracking()
                .Where(w => w.OpportunityId == oppId)
                .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

        var feeBand = engagement.AgreedFee is { } fee ? $"~${fee:0}" : "";
        var prompt = CaseStudyPrompts.BuildCaseStudyPrompt(
            engagement.Title, engagement.Description, feeBand,
            work?.FixSummary ?? "", work?.ClientConfirmation ?? "",
            engagement.Client?.Name ?? "", engagement.Client?.Company ?? "", anonymized,
            settings, SkillMatcher.PromptSummary(skills));

        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, output, error, model) = await _text.GenerateTextAsync(
            AiFeature.CaseStudy, prompt, settings, timeout, ct);
        if (!ok) return (null, "Case-study drafting failed: " + error);

        string title, slug, problem, solution, outcome, technologies, testimonialRequest;
        try
        {
            var json = AiCliSupport.ExtractJsonObject(output)
                       ?? throw new InvalidOperationException("no JSON object in output");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            title = Str(root, "title");
            slug = Str(root, "slug");
            problem = Str(root, "problem");
            solution = Str(root, "solution");
            outcome = Str(root, "outcome");
            technologies = Str(root, "technologies");
            testimonialRequest = Str(root, "testimonialRequest");
        }
        catch (Exception ex)
        {
            return (null, "Case-study drafting returned invalid JSON: " + ex.Message);
        }
        if (title.Length == 0 || problem.Length == 0)
            return (null, "Case-study drafting returned an empty result.");

        var now = DateTimeOffset.UtcNow;
        var study = new CaseStudy
        {
            Title = title,
            Slug = await UniqueSlugAsync(slug.Length > 0 ? slug : title, ct),
            ProblemSummary = problem,
            SolutionSummary = solution,
            OutcomeSummary = outcome,
            Technologies = technologies,
            TestimonialRequestDraft = testimonialRequest,
            Anonymized = anonymized,
            Status = CaseStudyStatus.Draft,
            OpportunityId = engagement.OpportunityId,
            EngagementId = engagement.Id,
            WorkSessionId = work?.Id,
            Provider = settings.AiFor(AiFeature.CaseStudy).Provider,
            Model = model,
            GeneratedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.CaseStudies.Add(study);
        await _db.SaveChangesAsync(ct);
        _audit.Record("CaseStudy", study.Id, "Drafted",
            $"Case study drafted from engagement #{engagement.Id}: {title}", "operator");
        await _db.SaveChangesAsync(ct);
        return (study, $"Case study drafted: {title}. Review, edit, and publish it from the Portfolio page.");
    }

    public async Task SetStatusAsync(long id, CaseStudyStatus status, CancellationToken ct)
    {
        var study = await _db.CaseStudies.FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new InvalidOperationException("Case study not found");
        study.Status = status;
        study.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Record("CaseStudy", id, "StatusChanged", $"Case study marked {status}", "operator");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Kebab-case, URL-safe, unique across existing studies.</summary>
    private async Task<string> UniqueSlugAsync(string raw, CancellationToken ct)
    {
        var slug = Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length == 0) slug = "case-study";
        if (slug.Length > 60) slug = slug[..60].Trim('-');
        var candidate = slug;
        for (var i = 2; await _db.CaseStudies.AnyAsync(c => c.Slug == candidate, ct); i++)
            candidate = $"{slug}-{i}";
        return candidate;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? "").Trim() : "";
}
