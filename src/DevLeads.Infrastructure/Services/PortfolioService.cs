using System.Diagnostics;
using System.Net;
using System.Text;
using Markdig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Generates the operator's public portfolio site — plain static HTML from operator
/// settings, skills, published case studies, and published content-studio drafts — and
/// deploys it to a GitHub Pages repo with ordinary git. Output renders to a directory
/// next to the SQLite database (never wwwroot: the build owns that folder), and deploy
/// force-pushes because the database is the source of truth, not the site repo.
/// </summary>
public sealed class PortfolioService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<PortfolioService> _log;

    public PortfolioService(DevLeadsDbContext db, AuditService audit, ILogger<PortfolioService> log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    public string ResolveOutputDir(OperatorSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PortfolioOutputDir))
            return settings.PortfolioOutputDir.Trim();
        var dataSource = _db.Database.GetDbConnection().DataSource;
        var appData = string.IsNullOrWhiteSpace(dataSource)
            ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data")
            : Path.GetDirectoryName(Path.GetFullPath(dataSource))!;
        return Path.Combine(appData, "portfolio-site");
    }

    /// <summary>Renders the whole static site. Returns the output dir and page count.</summary>
    public async Task<(bool Ok, string OutputDir, int Pages, string Message)> GenerateAsync(CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct)
                       ?? new OperatorSettings { Id = 1 };
        var skills = await _db.Skills.AsNoTracking()
            .Where(s => s.Enabled).OrderByDescending(s => s.Weight).ThenBy(s => s.Name).ToListAsync(ct);
        var studies = await _db.CaseStudies.AsNoTracking()
            .Where(c => c.Status == CaseStudyStatus.Published)
            .OrderByDescending(c => c.UpdatedAt).ToListAsync(ct);
        var posts = await _db.ContentDrafts.AsNoTracking()
            .Where(d => d.Status == ContentDraftStatus.Published && d.Format != ContentFormat.LinkedInPost)
            .OrderByDescending(d => d.UpdatedAt).ToListAsync(ct);
        var platforms = await _db.PlatformProfiles.AsNoTracking()
            .Where(p => p.ProfileUrl != "").OrderBy(p => p.Name).ToListAsync(ct);
        var resume = await _db.OperatorDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Kind == "resume", ct);

        var dir = ResolveOutputDir(settings);
        try
        {
            Directory.CreateDirectory(dir);
            ClearExceptGit(dir);

            var pages = 0;
            var name = WebUtility.HtmlEncode(settings.OperatorName);
            var resumeFile = resume is null ? null : "resume" + Path.GetExtension(resume.FileName);

            await File.WriteAllTextAsync(Path.Combine(dir, "styles.css"), StyleSheet, ct);
            await File.WriteAllTextAsync(Path.Combine(dir, ".nojekyll"), "", ct);
            if (!string.IsNullOrWhiteSpace(settings.PortfolioCname))
                await File.WriteAllTextAsync(Path.Combine(dir, "CNAME"), settings.PortfolioCname.Trim(), ct);

            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"),
                Layout(name, RenderIndex(settings, skills, studies, posts, platforms, resumeFile), ""), ct);
            pages++;

            if (studies.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(dir, "case-studies"));
                foreach (var study in studies)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(dir, "case-studies", study.Slug + ".html"),
                        Layout($"{WebUtility.HtmlEncode(study.Title)} · {name}", RenderCaseStudy(study), "../"), ct);
                    pages++;
                }
            }

            if (posts.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(dir, "blog"));
                foreach (var post in posts)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(dir, "blog", PostSlug(post) + ".html"),
                        Layout($"{WebUtility.HtmlEncode(post.Title)} · {name}", RenderPost(post), "../"), ct);
                    pages++;
                }
            }

            if (resume is not null && resumeFile is not null)
                await File.WriteAllBytesAsync(Path.Combine(dir, resumeFile), resume.Data, ct);

            _audit.Record("Portfolio", 0, "Generated", $"Portfolio rendered: {pages} page(s) to {dir}", "operator");
            await _db.SaveChangesAsync(ct);
            return (true, dir, pages, $"Rendered {pages} page(s), {studies.Count} case stud(ies), {posts.Count} post(s).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Portfolio generation failed");
            return (false, dir, 0, "Generation failed: " + ex.Message);
        }
    }

    /// <summary>Commits and force-pushes the rendered site to the configured repo/branch.</summary>
    public async Task<(bool Ok, string Message)> DeployAsync(CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.FirstOrDefaultAsync(ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.PortfolioRepoUrl))
            return (false, "Set the portfolio repo URL first.");

        var dir = ResolveOutputDir(settings);
        if (!File.Exists(Path.Combine(dir, "index.html")))
            return (false, "Generate the site before deploying.");

        var repo = settings.PortfolioRepoUrl.Trim();
        var branch = string.IsNullOrWhiteSpace(settings.PortfolioBranch) ? "main" : settings.PortfolioBranch.Trim();
        var email = string.IsNullOrWhiteSpace(settings.ContactEmail) ? "operator@localhost" : settings.ContactEmail;

        try
        {
            if (!Directory.Exists(Path.Combine(dir, ".git")))
            {
                await GitAsync(dir, $"init -b {branch}", ct);
                await GitAsync(dir, $"remote add origin {repo}", ct);
            }
            else
            {
                // Keep the remote in sync with the setting.
                await GitAsync(dir, "remote remove origin", ct, allowFailure: true);
                await GitAsync(dir, $"remote add origin {repo}", ct);
            }
            await GitAsync(dir, "add -A", ct);
            var commit = await GitAsync(dir,
                $"-c user.name=\"{settings.OperatorName}\" -c user.email=\"{email}\" commit -m \"Portfolio deploy {DateTimeOffset.UtcNow:u}\"",
                ct, allowFailure: true);
            if (!commit.Ok && !commit.Output.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase))
                return (false, "git commit failed: " + commit.Output);
            var push = await GitAsync(dir, $"push -f origin {branch}", ct);
            if (!push.Ok) return (false, "git push failed: " + push.Output);

            settings.LastPortfolioDeployAt = DateTimeOffset.UtcNow;
            settings.LastPortfolioDeployStatus = $"Deployed to {repo} ({branch})";
            _audit.Record("Portfolio", 0, "Deployed", settings.LastPortfolioDeployStatus, "operator");
            await _db.SaveChangesAsync(ct);
            return (true, settings.LastPortfolioDeployStatus + ". GitHub Pages usually updates within a minute.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Portfolio deploy failed");
            if (settings is not null)
            {
                settings.LastPortfolioDeployStatus = "Deploy failed: " + ex.Message;
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            return (false, "Deploy failed: " + ex.Message);
        }
    }

    private async Task<(bool Ok, string Output)> GitAsync(
        string workDir, string args, CancellationToken ct, bool allowFailure = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = (stdout + "\n" + stderr).Trim();
        if (process.ExitCode != 0 && !allowFailure)
            throw new InvalidOperationException($"git {args.Split(' ')[0]}: {output}");
        return (process.ExitCode == 0, output);
    }

    private static void ClearExceptGit(string dir)
    {
        foreach (var file in Directory.GetFiles(dir)) File.Delete(file);
        foreach (var sub in Directory.GetDirectories(dir))
            if (!Path.GetFileName(sub).Equals(".git", StringComparison.OrdinalIgnoreCase))
                Directory.Delete(sub, recursive: true);
    }

    // ---- Rendering ----

    private static string RenderIndex(OperatorSettings s, List<Skill> skills, List<CaseStudy> studies,
        List<ContentDraft> posts, List<PlatformProfile> platforms, string? resumeFile)
    {
        var sb = new StringBuilder();
        var name = WebUtility.HtmlEncode(s.OperatorName);
        sb.Append($"<header><h1>{name}</h1>");
        if (!string.IsNullOrWhiteSpace(s.Headline))
            sb.Append($"<p class=\"headline\">{WebUtility.HtmlEncode(s.Headline)}</p>");
        sb.Append("<p class=\"meta\">")
          .Append(WebUtility.HtmlEncode(s.Location))
          .Append(" · ").Append(WebUtility.HtmlEncode(s.RemoteAvailability)).Append(" remote")
          .Append("</p>");
        sb.Append("<p class=\"actions\">");
        sb.Append($"<a class=\"btn\" href=\"mailto:{WebUtility.HtmlEncode(s.ContactEmail)}\">Email me</a>");
        if (!string.IsNullOrWhiteSpace(s.BookingLink))
            sb.Append($"<a class=\"btn primary\" href=\"{WebUtility.HtmlEncode(s.BookingLink.Trim())}\">Book a call</a>");
        if (resumeFile is not null)
            sb.Append($"<a class=\"btn\" href=\"{resumeFile}\">Resume</a>");
        sb.Append("</p></header>");

        if (!string.IsNullOrWhiteSpace(s.Bio))
            sb.Append($"<section><h2>About</h2><p>{WebUtility.HtmlEncode(s.Bio).Replace("\n", "<br>")}</p></section>");
        if (!string.IsNullOrWhiteSpace(s.ServicesBlurb))
            sb.Append($"<section><h2>What I do</h2><p>{WebUtility.HtmlEncode(s.ServicesBlurb).Replace("\n", "<br>")}</p></section>");

        if (skills.Count > 0)
        {
            sb.Append("<section><h2>Skills</h2>");
            foreach (var group in skills.GroupBy(k => k.Category).OrderBy(g => g.Min(k => -k.Weight)))
            {
                sb.Append($"<h3>{WebUtility.HtmlEncode(group.Key)}</h3><p class=\"tags\">");
                foreach (var skill in group)
                    sb.Append($"<span class=\"tag\">{WebUtility.HtmlEncode(skill.Name)}</span>");
                sb.Append("</p>");
            }
            sb.Append("</section>");
        }

        if (studies.Count > 0)
        {
            sb.Append("<section><h2>Case studies</h2><ul class=\"cards\">");
            foreach (var study in studies)
            {
                sb.Append("<li><a href=\"case-studies/").Append(study.Slug).Append(".html\">")
                  .Append(WebUtility.HtmlEncode(study.Title)).Append("</a>")
                  .Append($"<p>{WebUtility.HtmlEncode(Teaser(study.ProblemSummary))}</p></li>");
            }
            sb.Append("</ul></section>");
        }

        if (posts.Count > 0)
        {
            sb.Append("<section><h2>Writing</h2><ul class=\"cards\">");
            foreach (var post in posts)
                sb.Append("<li><a href=\"blog/").Append(PostSlug(post)).Append(".html\">")
                  .Append(WebUtility.HtmlEncode(post.Title)).Append("</a></li>");
            sb.Append("</ul></section>");
        }

        if (platforms.Count > 0)
        {
            sb.Append("<section><h2>Elsewhere</h2><p class=\"tags\">");
            foreach (var platform in platforms)
                sb.Append($"<a class=\"tag\" href=\"{WebUtility.HtmlEncode(platform.ProfileUrl)}\">{WebUtility.HtmlEncode(platform.Name)}</a>");
            sb.Append("</p></section>");
        }
        return sb.ToString();
    }

    private static string RenderCaseStudy(CaseStudy study)
    {
        var sb = new StringBuilder();
        sb.Append($"<article><h1>{WebUtility.HtmlEncode(study.Title)}</h1>");
        if (!string.IsNullOrWhiteSpace(study.Technologies))
        {
            sb.Append("<p class=\"tags\">");
            foreach (var tech in study.Technologies.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                sb.Append($"<span class=\"tag\">{WebUtility.HtmlEncode(tech)}</span>");
            sb.Append("</p>");
        }
        sb.Append($"<h2>The problem</h2><p>{WebUtility.HtmlEncode(study.ProblemSummary)}</p>");
        sb.Append($"<h2>What I did</h2><p>{WebUtility.HtmlEncode(study.SolutionSummary)}</p>");
        if (!string.IsNullOrWhiteSpace(study.OutcomeSummary))
            sb.Append($"<h2>Outcome</h2><p>{WebUtility.HtmlEncode(study.OutcomeSummary)}</p>");
        // The quote renders only with explicit consent; attribution only when not anonymized.
        if (study.ClientConsent && !string.IsNullOrWhiteSpace(study.TestimonialQuote))
        {
            sb.Append($"<blockquote>{WebUtility.HtmlEncode(study.TestimonialQuote)}");
            if (!study.Anonymized && !string.IsNullOrWhiteSpace(study.TestimonialAttribution))
                sb.Append($"<footer>— {WebUtility.HtmlEncode(study.TestimonialAttribution)}</footer>");
            sb.Append("</blockquote>");
        }
        sb.Append("</article><p><a href=\"../index.html\">← All case studies</a></p>");
        return sb.ToString();
    }

    private static string RenderPost(ContentDraft post)
    {
        var html = Markdown.ToHtml(post.BodyMarkdown ?? "");
        return $"<article><h1>{WebUtility.HtmlEncode(post.Title)}</h1>{html}</article>" +
               "<p><a href=\"../index.html\">← Home</a></p>";
    }

    private static string Layout(string title, string body, string rootPrefix) => $"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{title}</title>
        <link rel="stylesheet" href="{rootPrefix}styles.css">
        </head>
        <body>
        <main>
        {body}
        </main>
        </body>
        </html>
        """;

    private static string PostSlug(ContentDraft post)
    {
        var slug = System.Text.RegularExpressions.Regex
            .Replace(post.Title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 60) slug = slug[..60].Trim('-');
        return slug.Length == 0 ? "post-" + post.Id : slug + "-" + post.Id;
    }

    private static string Teaser(string text) =>
        text.Length <= 160 ? text : text[..160].TrimEnd() + "…";

    private const string StyleSheet = """
        :root { --ink:#1a1f26; --muted:#5b6470; --line:#e3e7ec; --accent:#0b5fff; --bg:#ffffff; }
        @media (prefers-color-scheme: dark) {
          :root { --ink:#e8ecf1; --muted:#9aa4b1; --line:#2a323c; --accent:#5b93ff; --bg:#12161b; }
        }
        * { box-sizing: border-box; }
        body { margin:0; background:var(--bg); color:var(--ink);
               font:17px/1.6 system-ui, -apple-system, "Segoe UI", sans-serif; }
        main { max-width: 760px; margin: 0 auto; padding: 3rem 1.2rem 4rem; }
        h1 { font-size: 2rem; margin:.2rem 0; }
        h2 { font-size: 1.25rem; margin: 2rem 0 .5rem; border-bottom: 1px solid var(--line); padding-bottom:.3rem; }
        h3 { font-size: 1rem; margin: 1rem 0 .3rem; color: var(--muted); }
        a { color: var(--accent); text-decoration: none; }
        a:hover { text-decoration: underline; }
        .headline { font-size: 1.15rem; color: var(--muted); margin:.2rem 0; }
        .meta { color: var(--muted); font-size:.95rem; }
        .actions { display:flex; gap:.6rem; flex-wrap:wrap; margin-top: 1rem; }
        .btn { display:inline-block; padding:.45rem .9rem; border:1px solid var(--line);
               border-radius:8px; color:var(--ink); }
        .btn.primary { background:var(--accent); border-color:var(--accent); color:#fff; }
        .btn:hover { text-decoration:none; border-color:var(--accent); }
        .tags { display:flex; gap:.4rem; flex-wrap:wrap; }
        .tag { display:inline-block; padding:.15rem .55rem; border:1px solid var(--line);
               border-radius:999px; font-size:.85rem; color:var(--muted); }
        ul.cards { list-style:none; padding:0; display:grid; gap:.8rem; }
        ul.cards li { border:1px solid var(--line); border-radius:10px; padding:.8rem 1rem; }
        ul.cards p { margin:.3rem 0 0; color:var(--muted); font-size:.95rem; }
        blockquote { margin:1.5rem 0; padding:.8rem 1.2rem; border-left:3px solid var(--accent);
                     background:color-mix(in srgb, var(--accent) 6%, transparent); font-style:italic; }
        blockquote footer { margin-top:.5rem; font-style:normal; color:var(--muted); }
        pre { background:color-mix(in srgb, var(--ink) 6%, transparent); padding:.8rem 1rem;
              border-radius:8px; overflow-x:auto; font-size:.9rem; }
        img { max-width:100%; }
        """;
}
