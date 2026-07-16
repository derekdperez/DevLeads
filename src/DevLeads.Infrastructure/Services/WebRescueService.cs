using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Connectors;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Site rescue: actively but passively probes business web assets for public errors/outages
/// that match an operator-defined <see cref="WebScanProbe"/>, records confirmed breakage as
/// <see cref="WebAssetFinding"/> repair leads, discovers an owner contact email, and drafts
/// affordable repair-offer emails in one batched AI call. Scanning is read-only GETs of
/// public pages; nothing is ever sent from here — outreach is drafted and sent by hand.
/// </summary>
public sealed class WebRescueService
{
    // Never GET these: not public content, or explicitly the kind of thing a passive scan avoids.
    private static readonly string[] ProbePathDenyList =
        { "/wp-admin", "/admin", "/login", "/wp-login", "/user/login", "/.env", "/.git" };

    // Big platforms discovery should never surface as "a small business's own broken site".
    private static readonly string[] PlatformHosts =
    {
        "duckduckgo.com", "google.com", "bing.com", "youtube.com", "facebook.com", "twitter.com",
        "x.com", "reddit.com", "wikipedia.org", "amazon.com", "github.com", "linkedin.com",
        "instagram.com", "pinterest.com", "medium.com", "wordpress.org", "stackoverflow.com",
        "microsoft.com", "apple.com", "yelp.com", "tripadvisor.com"
    };

    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    private const int MaxBodyChars = 200_000;

    private readonly DevLeadsDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AiTextRouter _text;
    private readonly AuditService _audit;
    private readonly EmailService _email;
    private readonly ILogger<WebRescueService> _log;

    public WebRescueService(DevLeadsDbContext db, IHttpClientFactory httpFactory,
        AiTextRouter text, AuditService audit, EmailService email, ILogger<WebRescueService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _text = text;
        _audit = audit;
        _email = email;
        _log = log;
    }

    /// <summary>
    /// Runs a probe against pasted targets and (optionally) search-discovered targets. Each
    /// target is verified; only broken/degraded assets become findings. Returns a summary.
    /// </summary>
    public async Task<(int Checked, int Found, string Message)> ScanAsync(
        long probeId, string manualTargets, bool useDiscovery, CancellationToken ct)
    {
        var probe = await _db.WebScanProbes.FirstOrDefaultAsync(p => p.Id == probeId, ct);
        if (probe is null) return (0, 0, "Probe not found.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                       ?? new OperatorSettings { Id = 1 };
        var cap = Math.Clamp(settings.WebScanMaxTargetsPerRun, 1, 200);

        var targets = new List<(string Url, WebAssetDetection Detection)>();
        foreach (var url in ParseTargets(manualTargets))
            targets.Add((url, WebAssetDetection.ManualTarget));

        var discoveryNote = "";
        if (useDiscovery)
        {
            var (discovered, note) = await DiscoverTargetsAsync(probe, settings, ct);
            discoveryNote = note;
            foreach (var url in discovered) targets.Add((url, WebAssetDetection.Discovery));
        }

        // De-duplicate by canonical URL, keep manual targets ahead of discovered ones.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = targets
            .Select(t => (Canonical: SourceUrlCanonicalizer.Canonicalize(t.Url) ?? t.Url, t.Detection))
            .Where(t => seen.Add(t.Canonical))
            .Take(cap)
            .ToList();

        if (ordered.Count == 0)
            return (0, 0, useDiscovery
                ? "No targets to scan. Paste domains/URLs, or add discovery queries to the probe. " + discoveryNote
                : "No targets to scan. Paste one domain or URL per line.");

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var found = 0;
        var checkedCount = 0;
        foreach (var (url, detection) in ordered)
        {
            ct.ThrowIfCancellationRequested();
            checkedCount++;
            var (analysis, homepageBody) = await AnalyzeAsync(http, url, probe, ct);
            if (analysis is null) continue; // healthy — not a lead
            if (await UpsertFindingAsync(http, probe, url, detection, analysis.Value, homepageBody, ct))
                found++;
            // Politeness: a short gap between hosts so a scan is never a burst.
            await Task.Delay(400, ct);
        }

        probe.LastRunAt = DateTimeOffset.UtcNow;
        probe.LastRunChecked = checkedCount;
        probe.LastRunFound = found;
        probe.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _audit.Record("WebScanProbe", probe.Id, "Scanned",
            $"Probe '{probe.Name}' checked {checkedCount} target(s); {found} broken asset(s).", "operator");
        await _db.SaveChangesAsync(ct);

        var message = $"Checked {checkedCount} target(s); found {found} broken/degraded asset(s).";
        if (discoveryNote.Length > 0) message += " " + discoveryNote;
        return (checkedCount, found, message);
    }

    /// <summary>Re-checks one existing finding and refreshes its live status/evidence.</summary>
    public async Task<(bool Ok, string Message)> RecheckAsync(long findingId, CancellationToken ct)
    {
        var finding = await _db.WebAssetFindings.FirstOrDefaultAsync(f => f.Id == findingId, ct);
        if (finding is null) return (false, "Finding not found.");
        var probe = finding.ProbeId is { } pid
            ? await _db.WebScanProbes.FirstOrDefaultAsync(p => p.Id == pid, ct)
            : null;
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var (analysis, _) = await AnalyzeAsync(http, finding.Url, probe, ct);
        finding.LastCheckedAt = DateTimeOffset.UtcNow;
        finding.UpdatedAt = DateTimeOffset.UtcNow;
        if (analysis is null)
        {
            await _db.SaveChangesAsync(ct);
            return (true, "The page now loads without a detected error. Consider dismissing this finding.");
        }
        finding.Severity = analysis.Value.Severity;
        finding.Signal = analysis.Value.Signal;
        finding.Evidence = analysis.Value.Evidence;
        finding.HttpStatus = analysis.Value.Status;
        if (analysis.Value.Software.Length > 0) finding.DetectedSoftware = analysis.Value.Software;
        await _db.SaveChangesAsync(ct);
        return (true, $"Still broken: {analysis.Value.Signal}.");
    }

    /// <summary>
    /// Drafts repair-offer emails for every New finding that has a contact and no draft yet,
    /// in one batched AI call. Suppressed contacts are skipped. Nothing is sent.
    /// </summary>
    public async Task<(int Generated, string Message)> GenerateOutreachBatchAsync(
        string extraInstructions, CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                       ?? new OperatorSettings { Id = 1 };

        var candidates = await _db.WebAssetFindings
            .Where(f => (f.Status == WebAssetStatus.New || f.Status == WebAssetStatus.Reviewing)
                        && f.OutreachBody == "" && f.ContactEmail != "")
            .OrderByDescending(f => f.Severity == WebAssetSeverity.Down)
            .ThenByDescending(f => f.FirstSeenAt)
            .Take(20).ToListAsync(ct);
        if (candidates.Count == 0)
            return (0, "No findings are ready for drafting. A finding needs a contact email and no existing draft.");

        // Honor the suppression list even for drafting: never prepare outreach we must not send.
        var toDraft = new List<WebAssetFinding>();
        foreach (var f in candidates)
        {
            if (settings.SuppressionListEnabled &&
                await _db.SuppressionEntries.AnyAsync(s => s.ContactValue == f.ContactEmail, ct))
                continue;
            toDraft.Add(f);
        }
        if (toDraft.Count == 0) return (0, "Every ready finding's contact is on the suppression list.");

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var items = toDraft.Select(f => new WebOutreachItem
        {
            Id = "a" + f.Id,
            BusinessName = f.BusinessName,
            Host = f.Host,
            Url = f.Url,
            Severity = f.Severity.ToString(),
            Signal = f.Signal,
            Evidence = f.Evidence,
            DetectedSoftware = f.DetectedSoftware
        }).ToList();

        var prompt = WebRescuePrompts.BuildOutreachBatchPrompt(
            items, settings, SkillMatcher.PromptSummary(skills), extraInstructions);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, output, error, model) = await _text.GenerateTextAsync(
            AiFeature.WebAssetOutreach, prompt, settings, timeout, ct);
        if (!ok) return (0, "Repair-offer drafting failed: " + error);

        Dictionary<long, (string Subject, string Body)> emails;
        try { emails = ParseEmails(output); }
        catch (Exception ex) { return (0, "Repair-offer drafting returned invalid JSON: " + ex.Message); }

        var provider = settings.AiFor(AiFeature.WebAssetOutreach).Provider;
        var now = DateTimeOffset.UtcNow;
        var generated = 0;
        foreach (var f in toDraft)
        {
            if (!emails.TryGetValue(f.Id, out var email) || string.IsNullOrWhiteSpace(email.Body)) continue;
            f.OutreachSubject = email.Subject.Trim();
            f.OutreachBody = email.Body.Trim();
            f.OutreachProvider = provider;
            f.OutreachModel = model;
            f.OutreachGeneratedAt = now;
            if (f.Status == WebAssetStatus.New) f.Status = WebAssetStatus.Reviewing;
            f.UpdatedAt = now;
            generated++;
        }
        await _db.SaveChangesAsync(ct);
        _audit.Record("WebAssetFinding", 0, "OutreachBatchDrafted",
            $"Drafted {generated} repair-offer email(s) via {provider}/{model}.", "operator");
        await _db.SaveChangesAsync(ct);
        return (generated, $"Drafted {generated} repair-offer email(s). Review each before sending.");
    }

    /// <summary>
    /// Actually delivers one drafted repair-offer email through the connected Gmail
    /// account. The operator's click is the approval; EmailService re-checks the kill
    /// switch, suppression list, and send caps before anything leaves.
    /// </summary>
    public async Task<(bool Ok, string Message)> SendOutreachEmailAsync(long findingId, CancellationToken ct)
    {
        var finding = await _db.WebAssetFindings.FirstOrDefaultAsync(f => f.Id == findingId, ct);
        if (finding is null) return (false, "Finding not found.");
        if (finding.ContactEmail.Length == 0) return (false, "No contact email on this finding.");
        if (string.IsNullOrWhiteSpace(finding.OutreachBody)) return (false, "No drafted email to send.");

        var (ok, messageId, error) = await _email.SendAsync(
            finding.ContactEmail, finding.OutreachSubject, finding.OutreachBody, ct);
        if (!ok) return (false, error);

        var now = DateTimeOffset.UtcNow;
        finding.Status = WebAssetStatus.Contacted;
        finding.OutreachSentAt = now;
        finding.OutreachMessageId = messageId;
        finding.UpdatedAt = now;
        _audit.Record("WebAssetFinding", finding.Id, "OutreachEmailSent",
            $"Repair offer emailed to {finding.ContactEmail}", "operator", new { messageId });
        await _db.SaveChangesAsync(ct);
        return (true, $"Sent to {finding.ContactEmail}.");
    }

    /// <summary>Re-runs contact discovery for one finding (when the first pass found nothing).</summary>
    public async Task<(bool Ok, string Message)> RefreshContactAsync(long findingId, CancellationToken ct)
    {
        var finding = await _db.WebAssetFindings.FirstOrDefaultAsync(f => f.Id == findingId, ct);
        if (finding is null) return (false, "Finding not found.");
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var (email, source) = await DiscoverContactAsync(http, finding.Host, null, ct);
        if (email.Length == 0) return (false, "No contact email found on the site. Add one manually.");
        finding.ContactEmail = email;
        finding.ContactSource = source;
        finding.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, $"Found contact: {email} ({source}).");
    }

    // ---- verification ----

    private readonly record struct Analysis(
        bool Broken, WebAssetSeverity Severity, string Signal, string Evidence, int Status, string Software);

    /// <summary>
    /// Fetches the homepage and any probe paths, returning the breakage analysis (null when
    /// healthy) plus the homepage body so contact discovery can reuse it.
    /// </summary>
    private async Task<(Analysis? Result, string HomepageBody)> AnalyzeAsync(
        HttpClient http, string url, WebScanProbe? probe, CancellationToken ct)
    {
        var signatures = BuildSignatureList(probe);
        var paths = ProbePaths(probe);
        string homepageBody = "";
        Analysis? worst = null;

        // The homepage first (also captured for contact discovery), then the extra paths.
        foreach (var (target, isHomepage) in EnumerateTargets(url, paths))
        {
            var (status, headers, body, transportError) = await FetchAsync(http, target, ct);
            if (isHomepage) homepageBody = body;

            var analysis = Evaluate(target, status, headers, body, transportError, probe, signatures);
            if (analysis is { } a && (worst is null || a.Severity <= worst.Value.Severity))
                worst = a; // Down (0) < Degraded (1) < Warning (2): lower enum value = worse
        }
        return (worst, homepageBody);
    }

    private static IEnumerable<(string Target, bool IsHomepage)> EnumerateTargets(string url, IReadOnlyList<string> paths)
    {
        yield return (url, true);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri)) yield break;
        var root = $"{baseUri.Scheme}://{baseUri.Authority}";
        foreach (var path in paths)
        {
            var normalized = path.StartsWith('/') ? path : "/" + path;
            if (ProbePathDenyList.Any(deny => normalized.StartsWith(deny, StringComparison.OrdinalIgnoreCase)))
                continue;
            yield return (root + normalized, false);
        }
    }

    private Analysis? Evaluate(string target, int status, string headers, string body, string? transportError,
        WebScanProbe? probe, IReadOnlyList<WebBreakageSignature> signatures)
    {
        var software = DetectSoftware(headers + "\n" + body, probe);

        // A transport failure (DNS/connection/TLS) is a real outage a business would pay to fix.
        if (transportError is not null)
        {
            if (probe?.FlagServerErrors == false) return null;
            return new Analysis(true, WebAssetSeverity.Down, "Site unreachable: " + transportError,
                transportError, 0, software);
        }

        // A visible error string is breakage on any status code.
        var haystack = body.Length > MaxBodyChars ? body[..MaxBodyChars] : body;
        foreach (var sig in signatures)
        {
            var idx = haystack.IndexOf(sig.Text, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var severity = status >= 500 && sig.Severity != WebAssetSeverity.Down ? WebAssetSeverity.Down : sig.Severity;
            return new Analysis(true, severity, $"matched: {sig.Label} (\"{Trim(sig.Text, 60)}\")",
                Excerpt(haystack, idx, sig.Text.Length), status, software);
        }

        // A 5xx with no recognizable body is still an outage.
        if (status >= 500 && probe?.FlagServerErrors != false)
            return new Analysis(true, WebAssetSeverity.Down, $"HTTP {status}",
                Trim(haystack, 400), status, software);

        return null;
    }

    private async Task<(int Status, string Headers, string Body, string? TransportError)> FetchAsync(
        HttpClient http, string target, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,*/*;q=0.8");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var headers = response.Headers.ToString() + response.Content.Headers;
            var raw = await response.Content.ReadAsStringAsync(ct);
            var body = raw.Length > MaxBodyChars ? raw[..MaxBodyChars] : raw;
            return ((int)response.StatusCode, headers, body, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex)
        {
            return (0, "", "", DescribeTransportError(ex));
        }
        catch (TaskCanceledException)
        {
            return (0, "", "", "request timed out");
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Site rescue fetch failed for {Target}", target);
            return (0, "", "", "connection failed");
        }
    }

    private static string DescribeTransportError(HttpRequestException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        if (message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("handshake", StringComparison.OrdinalIgnoreCase))
            return "TLS/SSL failure";
        if (message.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("name or service", StringComparison.OrdinalIgnoreCase))
            return "DNS/host not found";
        if (ex.StatusCode is { } code) return $"HTTP {(int)code}";
        return "connection failed";
    }

    // ---- contact discovery ----

    private async Task<(string Email, string Source)> DiscoverContactAsync(
        HttpClient http, string host, string? homepageBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host)) return ("", "");
        var root = "https://" + host;

        // Reuse the homepage body captured during the scan when we have it.
        if (!string.IsNullOrWhiteSpace(homepageBody))
        {
            var fromHome = ExtractEmail(homepageBody, host);
            if (fromHome.Email.Length > 0) return fromHome;
        }
        else
        {
            var (_, _, body, err) = await FetchAsync(http, root, ct);
            if (err is null)
            {
                var fromHome = ExtractEmail(body, host);
                if (fromHome.Email.Length > 0) return fromHome;
            }
        }

        foreach (var path in new[] { "/contact", "/contact-us", "/about", "/about-us" })
        {
            var (_, _, body, err) = await FetchAsync(http, root + path, ct);
            if (err is not null || body.Length == 0) continue;
            var found = ExtractEmail(body, host);
            if (found.Email.Length > 0) return found;
            await Task.Delay(250, ct);
        }
        return ("", "");
    }

    private static (string Email, string Source) ExtractEmail(string html, string host)
    {
        // Prefer an explicit mailto: link — that is unambiguously a contact address.
        var mailto = Regex.Match(html, @"mailto:([A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})",
            RegexOptions.IgnoreCase);
        if (mailto.Success && IsPlausibleEmail(mailto.Groups[1].Value))
            return (mailto.Groups[1].Value.ToLowerInvariant(), "mailto");

        var domain = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        var candidates = EmailRegex.Matches(html)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(IsPlausibleEmail)
            .Distinct()
            .ToList();
        // An address on the site's own domain is the most likely owner contact.
        var onDomain = candidates.FirstOrDefault(e => e.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase));
        if (onDomain is not null) return (onDomain, "page text (own domain)");
        return candidates.Count > 0 ? (candidates[0], "page text") : ("", "");
    }

    private static bool IsPlausibleEmail(string email)
    {
        var lower = email.ToLowerInvariant();
        if (lower.Contains("example.com") || lower.Contains("sentry") || lower.Contains("wixpress") ||
            lower.Contains("schema.org") || lower.Contains("@2x") || lower.Contains("your-email") ||
            lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".gif") ||
            lower.EndsWith(".webp") || lower.EndsWith(".svg"))
            return false;
        return true;
    }

    // ---- discovery search ----

    private async Task<(List<string> Urls, string Note)> DiscoverTargetsAsync(
        WebScanProbe probe, OperatorSettings settings, CancellationToken ct)
    {
        var queries = SplitLines(probe.DiscoveryQueries);
        if (queries.Count == 0)
            return (new List<string>(), "Discovery skipped: this probe has no discovery queries.");
        var endpoint = string.IsNullOrWhiteSpace(settings.WebScanSearchEndpoint)
            ? "https://html.duckduckgo.com/html/?q={q}" : settings.WebScanSearchEndpoint.Trim();
        if (!endpoint.Contains("{q}")) endpoint += (endpoint.Contains('?') ? "&q=" : "?q=") + "{q}";

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var urls = new List<string>();
        var seenHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var query in queries.Take(5))
        {
            var requestUrl = endpoint.Replace("{q}", Uri.EscapeDataString(query));
            var (_, _, body, err) = await FetchAsync(http, requestUrl, ct);
            if (err is not null || body.Length == 0) continue;
            foreach (var candidate in ExtractSearchResultUrls(body))
            {
                if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) continue;
                var host = uri.Host;
                if (PlatformHosts.Any(p => host.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                                           host.EndsWith("." + p, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (!seenHosts.Add(host)) continue; // one URL per host from discovery
                urls.Add($"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}");
            }
            await Task.Delay(600, ct); // be gentle with the search endpoint
        }
        var note = urls.Count == 0
            ? "Discovery returned no candidate sites (the search endpoint may be rate-limiting; try again later or swap it in Settings)."
            : $"Discovery surfaced {urls.Count} candidate site(s).";
        return (urls, note);
    }

    private static IEnumerable<string> ExtractSearchResultUrls(string html)
    {
        // DuckDuckGo HTML wraps results in a redirect carrying the real URL in uddg=.
        foreach (Match m in Regex.Matches(html, @"uddg=([^&""']+)"))
        {
            var decoded = Uri.UnescapeDataString(m.Groups[1].Value);
            if (decoded.StartsWith("http", StringComparison.OrdinalIgnoreCase)) yield return decoded;
        }
        // Fallback: plain href http(s) links (other keyless HTML search endpoints).
        foreach (Match m in Regex.Matches(html, @"href=""(https?://[^""]+)""", RegexOptions.IgnoreCase))
            yield return m.Groups[1].Value;
    }

    // ---- persistence ----

    private async Task<bool> UpsertFindingAsync(HttpClient http, WebScanProbe probe, string url,
        WebAssetDetection detection, Analysis analysis, string homepageBody, CancellationToken ct)
    {
        var canonical = SourceUrlCanonicalizer.Canonicalize(url) ?? url;
        var host = LeadQualityRules.HostFromUrl(canonical) ?? "";
        var now = DateTimeOffset.UtcNow;

        var existing = await _db.WebAssetFindings.FirstOrDefaultAsync(f => f.Url == canonical, ct);
        if (existing is not null)
        {
            // Refresh a known finding in place; never resurrect one the operator dismissed/won.
            existing.Severity = analysis.Severity;
            existing.Signal = analysis.Signal;
            existing.Evidence = analysis.Evidence;
            existing.HttpStatus = analysis.Status;
            if (analysis.Software.Length > 0) existing.DetectedSoftware = analysis.Software;
            existing.LastCheckedAt = now;
            existing.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return false; // not a newly-found asset
        }

        var (email, source) = await DiscoverContactAsync(http, host, homepageBody, ct);
        var finding = new WebAssetFinding
        {
            ProbeId = probe.Id,
            ProbeName = probe.Name,
            Url = canonical,
            Host = host,
            BusinessName = GuessBusinessName(homepageBody, host),
            Severity = analysis.Severity,
            Detection = detection,
            Status = WebAssetStatus.New,
            HttpStatus = analysis.Status,
            Signal = analysis.Signal,
            Evidence = analysis.Evidence,
            DetectedSoftware = analysis.Software,
            ContactEmail = email,
            ContactSource = source,
            FirstSeenAt = now,
            LastCheckedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WebAssetFindings.Add(finding);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- helpers ----

    private static IReadOnlyList<WebBreakageSignature> BuildSignatureList(WebScanProbe? probe)
    {
        var list = new List<WebBreakageSignature>();
        if (probe is not null)
            foreach (var custom in SplitLines(probe.ErrorSignatures))
                list.Add(new WebBreakageSignature(custom, WebAssetSeverity.Degraded, "custom signature"));
        list.AddRange(WebBreakageSignatures.Defaults);
        return list;
    }

    private static IReadOnlyList<string> ProbePaths(WebScanProbe? probe) =>
        probe is null ? Array.Empty<string>() : SplitLines(probe.PathsToCheck);

    private static string DetectSoftware(string text, WebScanProbe? probe)
    {
        if (probe is not null && !string.IsNullOrWhiteSpace(probe.SoftwarePackage) &&
            text.Contains(probe.SoftwarePackage, StringComparison.OrdinalIgnoreCase))
            return probe.SoftwarePackage.Trim();
        foreach (var (name, fingerprints) in WebBreakageSignatures.SoftwareFingerprints)
            if (fingerprints.Any(f => text.Contains(f, StringComparison.OrdinalIgnoreCase)))
                return name;
        return "";
    }

    private static string GuessBusinessName(string html, string host)
    {
        var title = Regex.Match(html, @"<title[^>]*>(.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (title.Success)
        {
            var text = WebUtility.HtmlDecode(title.Groups[1].Value).Trim();
            // Drop trailing " - Home", " | error" style suffixes to a clean-ish name.
            text = Regex.Replace(text, @"\s*[\|\-–—:]\s*.*$", "").Trim();
            if (text.Length is > 1 and <= 80) return text;
        }
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static IEnumerable<string> ParseTargets(string raw)
    {
        foreach (var line in SplitLines(raw))
        {
            var value = line.Trim().TrimEnd('/');
            if (value.Length == 0) continue;
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                value = "https://" + value;
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                yield return value;
        }
    }

    private static List<string> SplitLines(string raw) =>
        (raw ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();

    private static string Trim(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }

    private static string Excerpt(string body, int index, int matchLength)
    {
        var start = Math.Max(0, index - 80);
        var end = Math.Min(body.Length, index + matchLength + 120);
        return Trim(body[start..end], 300);
    }

    private static Dictionary<long, (string Subject, string Body)> ParseEmails(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("no JSON object found");
        using var doc = JsonDocument.Parse(output[start..(end + 1)]);
        if (!doc.RootElement.TryGetProperty("emails", out var emails) || emails.ValueKind != JsonValueKind.Array)
            throw new JsonException("missing emails array");
        var result = new Dictionary<long, (string, string)>();
        foreach (var item in emails.EnumerateArray())
        {
            var id = GetString(item, "id");
            var digits = new string(id.Where(char.IsDigit).ToArray());
            if (!long.TryParse(digits, out var findingId)) continue;
            var body = GetString(item, "body");
            if (body.Length == 0) continue;
            result[findingId] = (GetString(item, "subject"), body);
        }
        return result;
    }

    private static string GetString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
