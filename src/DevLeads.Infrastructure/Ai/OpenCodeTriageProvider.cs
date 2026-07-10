using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// Default AI provider: runs the single-pass structured triage through the local
/// `opencode` CLI (https://opencode.ai). The CLI brings its own provider/model
/// configuration, so triage works with whatever the operator has set up in opencode —
/// including its free models — with no API key in this app at all.
/// </summary>
public sealed class OpenCodeTriageProvider : IAiTriageProvider, IAiBatchShortlistProvider, IAiBatchTriageProvider
{
    private readonly ILogger<OpenCodeTriageProvider> _log;

    // CLI availability probe is cached per resolved path (probe spawns a process).
    private static readonly object ProbeLock = new();
    private static string? _probedPath;
    private static bool _probeResult;
    private static string _probeMessage = "";

    public OpenCodeTriageProvider(ILogger<OpenCodeTriageProvider> log) => _log = log;

    public string Name => "OpenCode";

    public bool IsAvailable(OperatorSettings settings) => Probe(ResolveCliPath(settings)).Available;

    public string AvailabilityMessage(OperatorSettings settings)
    {
        var (available, message) = Probe(ResolveCliPath(settings));
        return available ? message : $"opencode CLI not runnable: {message}";
    }

    public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = string.IsNullOrWhiteSpace(settings.AiModel)
            ? OperatorSettings.DefaultOpenCodeModel
            : settings.AiModel.Trim();
        var prompt = BuildPrompt(request);

        var response = new AiTriageResponse
        {
            Provider = Name,
            Model = model,
            RequestJson = JsonSerializer.Serialize(new { cli, model, prompt })
        };

        if (!Probe(cli).Available)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = AvailabilityMessage(settings);
            return response;
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds, 30, 300));
            var (exitCode, stdout, stderr) = await RunCliAsync(cli,
                new[] { "run", "-m", model, prompt }, timeout, ct);

            var cleaned = StripAnsi(stdout);
            response.ResponseJson = Truncate(cleaned, 8000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"opencode exited {exitCode}: {Truncate(StripAnsi(stderr), 400)}";
                return response;
            }

            var json = ExtractJsonObject(cleaned);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true; // one retry with the same prompt often fixes format drift
                response.ErrorMessage = "No JSON object found in opencode output.";
                return response;
            }

            var result = JsonSerializer.Deserialize<AiTriageResult>(json, ParseOptions);
            if (result is null || !IsSchemaValid(result))
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "opencode output did not match the strict triage schema.";
                return response;
            }

            Normalize(result);
            response.Succeeded = true;
            response.Result = result;
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "opencode call timed out.";
            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "JSON parse error: " + ex.Message;
            return response;
        }
        catch (Exception ex)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = ex.GetType().Name + ": " + ex.Message;
            _log.LogWarning(ex, "opencode triage failed");
            return response;
        }
    }

    public async Task<AiBatchTriageResponse> TriageBatchAsync(
        IReadOnlyList<AiBatchTriageItem> items,
        OperatorSettings settings,
        CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = string.IsNullOrWhiteSpace(settings.AiModel)
            ? OperatorSettings.DefaultOpenCodeModel
            : settings.AiModel.Trim();
        var prompt =
            AiTriagePrompts.SystemPrompt +
            "\n\nEach result object must match this schema exactly (plus the extra \"id\" field):\n" + AiTriagePrompts.JsonSchema +
            "\n\nDo not use tools, do not read or write files — respond with the JSON object only, no markdown fences.\n\n" +
            AiTriagePrompts.BuildBatchUserPrompt(items);

        var response = new AiBatchTriageResponse
        {
            Provider = Name,
            Model = model,
            RequestJson = JsonSerializer.Serialize(new { cli, model, batch = items.Count })
        };

        if (items.Count == 0) { response.Succeeded = true; return response; }

        if (!Probe(cli).Available)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = AvailabilityMessage(settings);
            return response;
        }

        try
        {
            // Batches produce more output than a single triage — allow the upper bound.
            var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 60, 600));
            var (exitCode, stdout, stderr) = await RunCliAsync(cli,
                new[] { "run", "-m", model, prompt }, timeout, ct);

            var cleaned = StripAnsi(stdout);
            response.ResponseJson = Truncate(cleaned, 24000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"opencode exited {exitCode}: {Truncate(StripAnsi(stderr), 400)}";
                return response;
            }

            var json = ExtractJsonObject(cleaned);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "No JSON object found in opencode batch output.";
                return response;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "opencode batch output did not include a results array.";
                return response;
            }

            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString() ?? "" : "";
                if (id.Length == 0) continue;

                var result = el.Deserialize<AiTriageResult>(ParseOptions);
                if (result is null || !IsSchemaValid(result)) continue;
                Normalize(result);
                response.Results[id] = result;
            }

            // Items the model skipped simply fall back to the per-item path — a partial
            // batch is still a success, not a retryable failure.
            response.Succeeded = response.Results.Count > 0;
            if (!response.Succeeded)
            {
                response.Retryable = true;
                response.ErrorMessage = "No valid result objects in opencode batch output.";
            }
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "opencode batch call timed out.";
            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "JSON parse error: " + ex.Message;
            return response;
        }
        catch (Exception ex)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = ex.GetType().Name + ": " + ex.Message;
            _log.LogWarning(ex, "opencode batch triage failed");
            return response;
        }
    }

    public async Task<AiShortlistResponse> ShortlistAsync(
        IReadOnlyList<AiShortlistItem> items,
        OperatorSettings settings,
        int maxSelections,
        CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = string.IsNullOrWhiteSpace(settings.AiModel)
            ? OperatorSettings.DefaultOpenCodeModel
            : settings.AiModel.Trim();
        var boundedMax = Math.Clamp(maxSelections, 0, items.Count);
        var prompt = BuildShortlistPrompt(items, boundedMax);

        var response = new AiShortlistResponse
        {
            Provider = Name,
            Model = model,
            RequestJson = JsonSerializer.Serialize(new { cli, model, maxSelections = boundedMax, prompt })
        };

        if (items.Count == 0 || boundedMax == 0)
        {
            response.Succeeded = true;
            response.Decisions = items.Select(i => new AiShortlistDecision { Id = i.Id, ShouldTriage = false }).ToList();
            return response;
        }

        if (!Probe(cli).Available)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = AvailabilityMessage(settings);
            return response;
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds, 30, 180));
            var (exitCode, stdout, stderr) = await RunCliAsync(cli,
                new[] { "run", "-m", model, prompt }, timeout, ct);

            var cleaned = StripAnsi(stdout);
            response.ResponseJson = Truncate(cleaned, 8000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"opencode shortlist exited {exitCode}: {Truncate(StripAnsi(stderr), 400)}";
                return response;
            }

            var json = ExtractJsonObject(cleaned);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "No JSON object found in opencode shortlist output.";
                return response;
            }

            var result = JsonSerializer.Deserialize<ShortlistOutput>(json, ParseOptions);
            if (result?.Selected is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "opencode shortlist output did not include a selected array.";
                return response;
            }

            var selected = result.Selected
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .Take(boundedMax)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Reason ?? "", StringComparer.OrdinalIgnoreCase);

            response.Succeeded = true;
            response.Decisions = items.Select(i => new AiShortlistDecision
            {
                Id = i.Id,
                ShouldTriage = selected.ContainsKey(i.Id),
                Reason = selected.TryGetValue(i.Id, out var reason) ? Truncate(reason, 300) : ""
            }).ToList();
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "opencode shortlist call timed out.";
            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "JSON parse error: " + ex.Message;
            return response;
        }
        catch (Exception ex)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = ex.GetType().Name + ": " + ex.Message;
            _log.LogWarning(ex, "opencode shortlist failed");
            return response;
        }
    }

    // ----- prompt -----

    private static string BuildPrompt(AiTriageRequest request) =>
        AiTriagePrompts.SystemPrompt +
        "\n\nThe JSON object must match this schema exactly:\n" + AiTriagePrompts.JsonSchema +
        "\n\nDo not use tools, do not read or write files — respond with the JSON object only, no markdown fences.\n\n" +
        AiTriagePrompts.BuildUserPrompt(request);

    private static string BuildShortlistPrompt(IReadOnlyList<AiShortlistItem> items, int maxSelections)
    {
        var payload = items.Select(i => new
        {
            i.Id,
            i.SourceKey,
            i.Title,
            Snippet = Truncate(i.Snippet, 700),
            i.PostedAt,
            i.HeuristicScore,
            MatchedTerms = i.MatchedTerms.Take(16)
        });

        return
            "You are screening public posts for profitable urgent software-support opportunities.\n" +
            "Pick only candidates worth a full expensive triage call. Favor posts where the author owns the broken business, asks for hands-on help, names a budget/pay intent, or describes customer/revenue impact.\n" +
            "Reject generic advice requests, vendor-only support/account issues, learning/homework posts, news, and low-value discussion.\n" +
            $"Return at most {maxSelections} items.\n" +
            "Respond with JSON only, exactly like: {\"selected\":[{\"id\":\"i0\",\"reason\":\"short reason\"}]}\n" +
            "Do not use tools, do not read or write files, and do not include markdown fences.\n\n" +
            "Candidates:\n" + JsonSerializer.Serialize(payload);
    }

    private sealed class ShortlistOutput
    {
        public List<ShortlistSelection> Selected { get; set; } = new();
    }

    private sealed class ShortlistSelection
    {
        public string Id { get; set; } = "";
        public string? Reason { get; set; }
    }

    // ----- CLI plumbing -----

    /// <summary>Resolves the configured CLI path, falling back to the standard install location.</summary>
    public static string ResolveCliPath(OperatorSettings settings)
    {
        var configured = string.IsNullOrWhiteSpace(settings.OpenCodeCliPath) ? "opencode" : settings.OpenCodeCliPath.Trim();
        if (configured != "opencode" || OnPath("opencode")) return configured;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var standard = Path.Combine(home, ".opencode", "bin", "opencode");
        return File.Exists(standard) ? standard : configured;
    }

    private static bool OnPath(string command)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        return paths.Any(p => !string.IsNullOrWhiteSpace(p) && File.Exists(Path.Combine(p, command)));
    }

    private static (bool Available, string Message) Probe(string cliPath)
    {
        lock (ProbeLock)
        {
            if (_probedPath == cliPath) return (_probeResult, _probeMessage);
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = cliPath,
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                if (process is null) { _probeResult = false; _probeMessage = "process failed to start"; }
                else
                {
                    var version = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(10_000);
                    _probeResult = process.HasExited && process.ExitCode == 0;
                    _probeMessage = _probeResult ? $"opencode {version} at {cliPath}" : "non-zero exit from --version";
                }
            }
            catch (Exception ex)
            {
                _probeResult = false;
                _probeMessage = ex.Message;
            }
            _probedPath = cliPath;
            return (_probeResult, _probeMessage);
        }
    }

    /// <summary>Clears the cached probe so a changed CLI path takes effect immediately.</summary>
    public static void ResetProbe()
    {
        lock (ProbeLock) { _probedPath = null; }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(
        string cliPath, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct)
    {
        // Run in an isolated scratch directory: opencode is a coding agent, and running it
        // inside a repository would make it scan project context we don't want in triage.
        var workDir = Path.Combine(Path.GetTempPath(), "devleads-opencode");
        Directory.CreateDirectory(workDir);

        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    // ----- output parsing -----

    private static readonly Regex AnsiPattern = new(@"\x1b\[[0-9;?]*[A-Za-z]|\x1b\][^\x07]*\x07", RegexOptions.Compiled);

    private static string StripAnsi(string text) => AnsiPattern.Replace(text, "");

    /// <summary>Extracts the first balanced JSON object from arbitrary CLI output.</summary>
    public static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        while (start >= 0)
        {
            var depth = 0;
            var inString = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
            start = text.IndexOf('{', start + 1);
        }
        return null;
    }

    private static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    private static bool IsSchemaValid(AiTriageResult r) =>
        !string.IsNullOrWhiteSpace(r.ProblemCategory) &&
        !string.IsNullOrWhiteSpace(r.OutreachRecommendation);

    /// <summary>Coerces near-miss enum values back onto the strict schema instead of failing the call.</summary>
    private static void Normalize(AiTriageResult r)
    {
        if (!AiTriageResult.ProblemCategories.Contains(r.ProblemCategory, StringComparer.OrdinalIgnoreCase))
            r.ProblemCategory = r.IsEmergency ? "Production Outage" : "Non-Urgent Help Request";
        else
            r.ProblemCategory = AiTriageResult.ProblemCategories.First(c =>
                c.Equals(r.ProblemCategory, StringComparison.OrdinalIgnoreCase));

        if (!AiTriageResult.OutreachRecommendations.Contains(r.OutreachRecommendation, StringComparer.OrdinalIgnoreCase))
            r.OutreachRecommendation = "Manual Review";
        else
            r.OutreachRecommendation = AiTriageResult.OutreachRecommendations.First(c =>
                c.Equals(r.OutreachRecommendation, StringComparison.OrdinalIgnoreCase));

        // Unknown/missing payment intent stays "" (neutral) — only a deliberate value
        // should influence scoring.
        r.PaymentIntent = AiTriageResult.PaymentIntents.FirstOrDefault(v =>
            v.Equals(r.PaymentIntent, StringComparison.OrdinalIgnoreCase)) ?? "";

        r.AiConfidence = Math.Clamp(r.AiConfidence, 0m, 1m);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
