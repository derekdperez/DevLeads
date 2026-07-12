using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// OpenAI-backed provider: runs the same structured triage/shortlist/generation calls
/// through the local `codex` CLI (https://github.com/openai/codex) in non-interactive
/// `exec` mode. Auth and model access come from the operator's codex login, so no API
/// key lives in this app. Unlike OpenCode there is NO model fallback chain: a failed
/// call surfaces its error so the operator's model choice is never silently swapped
/// (post-optimization experiments depend on a stable model).
/// </summary>
public sealed class CodexCliProvider : IAiTriageProvider, IAiBatchShortlistProvider, IAiBatchTriageProvider
{
    private readonly ILogger<CodexCliProvider> _log;

    // CLI availability probe is cached per resolved path (probe spawns a process).
    private static readonly object ProbeLock = new();
    private static string? _probedPath;
    private static bool _probeResult;
    private static string _probeMessage = "";

    public CodexCliProvider(ILogger<CodexCliProvider> log) => _log = log;

    public string Name => "Codex";

    public bool IsAvailable(OperatorSettings settings) => Probe(ResolveCliPath(settings)).Available;

    public string AvailabilityMessage(OperatorSettings settings)
    {
        var (available, message) = Probe(ResolveCliPath(settings));
        return available ? message : $"codex CLI not runnable: {message}";
    }

    private static string ResolveModel(OperatorSettings settings) =>
        string.IsNullOrWhiteSpace(settings.AiModel) ? OperatorSettings.DefaultCodexModel : settings.AiModel.Trim();

    public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = ResolveModel(settings);
        var prompt = AiCliSupport.BuildTriagePrompt(request);

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
            var (exitCode, output, stderr) = await RunCliAsync(cli, model, prompt, timeout, ct);
            response.ResponseJson = AiCliSupport.Truncate(output, 8000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"codex exited {exitCode}: {AiCliSupport.Truncate(stderr, 400)}";
                return response;
            }

            var json = AiCliSupport.ExtractJsonObject(output);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true; // one retry with the same prompt often fixes format drift
                response.ErrorMessage = "No JSON object found in codex output.";
                return response;
            }

            var result = JsonSerializer.Deserialize<AiTriageResult>(json, AiCliSupport.ParseOptions);
            if (result is null || !AiCliSupport.IsSchemaValid(result))
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "codex output did not match the strict triage schema.";
                return response;
            }

            AiCliSupport.Normalize(result);
            response.Succeeded = true;
            response.Result = result;
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "codex call timed out.";
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
            _log.LogWarning(ex, "codex triage failed");
            return response;
        }
    }

    public async Task<AiBatchTriageResponse> TriageBatchAsync(
        IReadOnlyList<AiBatchTriageItem> items,
        OperatorSettings settings,
        CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = ResolveModel(settings);

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
            var (exitCode, output, stderr) = await RunCliAsync(
                cli, model, AiCliSupport.BuildBatchTriagePrompt(items), timeout, ct);
            response.ResponseJson = AiCliSupport.Truncate(output, 24000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"codex exited {exitCode}: {AiCliSupport.Truncate(stderr, 400)}";
                return response;
            }

            var json = AiCliSupport.ExtractJsonObject(output);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "No JSON object found in codex batch output.";
                return response;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "codex batch output did not include a results array.";
                return response;
            }

            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString() ?? "" : "";
                if (id.Length == 0) continue;

                var result = el.Deserialize<AiTriageResult>(AiCliSupport.ParseOptions);
                if (result is null || !AiCliSupport.IsSchemaValid(result)) continue;
                AiCliSupport.Normalize(result);
                response.Results[id] = result;
            }

            // Items the model skipped simply fall back to the per-item path — a partial
            // batch is still a success, not a retryable failure.
            response.Succeeded = response.Results.Count > 0;
            if (!response.Succeeded)
            {
                response.Retryable = true;
                response.ErrorMessage = "No valid result objects in codex batch output.";
            }
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "codex batch call timed out.";
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
            _log.LogWarning(ex, "codex batch triage failed");
            return response;
        }
    }

    public async Task<AiShortlistResponse> ShortlistAsync(
        IReadOnlyList<AiShortlistItem> items,
        OperatorSettings settings,
        int maxSelections,
        string campaignObjective,
        CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = ResolveModel(settings);
        var boundedMax = Math.Clamp(maxSelections, 0, items.Count);
        var prompt = AiCliSupport.BuildShortlistPrompt(items, boundedMax, campaignObjective);

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
            var (exitCode, output, stderr) = await RunCliAsync(cli, model, prompt, timeout, ct);
            response.ResponseJson = AiCliSupport.Truncate(output, 8000);

            if (exitCode != 0)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = $"codex shortlist exited {exitCode}: {AiCliSupport.Truncate(stderr, 400)}";
                return response;
            }

            var json = AiCliSupport.ExtractJsonObject(output);
            if (json is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "No JSON object found in codex shortlist output.";
                return response;
            }

            var result = JsonSerializer.Deserialize<AiCliSupport.ShortlistOutput>(json, AiCliSupport.ParseOptions);
            if (result?.Selected is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "codex shortlist output did not include a selected array.";
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
                Reason = selected.TryGetValue(i.Id, out var reason) ? AiCliSupport.Truncate(reason, 300) : ""
            }).ToList();
            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            response.Succeeded = false;
            response.Retryable = true;
            response.ErrorMessage = "codex shortlist call timed out.";
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
            _log.LogWarning(ex, "codex shortlist failed");
            return response;
        }
    }

    /// <summary>
    /// Generic long-form generation, mirroring the OpenCode provider's contract: one
    /// prompt through `codex exec`, raw final-message text back. Callers own the output
    /// contract; failures surface their error instead of falling back to another model.
    /// </summary>
    public async Task<(bool Succeeded, string Text, string Error, string Model)> GenerateTextAsync(
        string prompt, OperatorSettings settings, TimeSpan timeout, CancellationToken ct)
    {
        var cli = ResolveCliPath(settings);
        var model = ResolveModel(settings);

        if (!Probe(cli).Available)
            return (false, "", AvailabilityMessage(settings), model);

        try
        {
            var (exitCode, output, stderr) = await RunCliAsync(cli, model, prompt, timeout, ct);
            if (exitCode != 0)
                return (false, "", $"codex exited {exitCode}: {AiCliSupport.Truncate(stderr, 400)}", model);

            return output.Length == 0
                ? (false, "", "codex produced no output.", model)
                : (true, output, "", model);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, "", "codex call timed out.", model);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "codex text generation failed");
            return (false, "", ex.GetType().Name + ": " + ex.Message, model);
        }
    }

    // ----- CLI plumbing -----

    /// <summary>Resolves the configured CLI path, falling back to the standard install location.</summary>
    public static string ResolveCliPath(OperatorSettings settings)
    {
        var configured = string.IsNullOrWhiteSpace(settings.CodexCliPath) ? "codex" : settings.CodexCliPath.Trim();
        if (configured != "codex" || OnPath("codex")) return configured;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var standard = Path.Combine(home, ".local", "bin", "codex");
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
                    _probeMessage = _probeResult ? $"{version} at {cliPath}" : "non-zero exit from --version";
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

    /// <summary>
    /// One `codex exec` call. Runs in an isolated scratch directory with a read-only
    /// sandbox (codex is a coding agent; the prompts already forbid tool use, and the
    /// sandbox enforces it). The final agent message is read from a --output-last-message
    /// file, so stdout's event stream never pollutes the returned text.
    /// </summary>
    private static async Task<(int ExitCode, string Output, string Stderr)> RunCliAsync(
        string cliPath, string model, string prompt, TimeSpan timeout, CancellationToken ct)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "devleads-codex");
        Directory.CreateDirectory(workDir);
        var lastMessageFile = Path.Combine(workDir, "last-message-" + Guid.NewGuid().ToString("N") + ".txt");

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
        foreach (var a in new[]
                 {
                     "exec",
                     "--skip-git-repo-check", // the scratch dir is not a repository
                     "--ephemeral",           // don't accumulate session files across triage calls
                     "--sandbox", "read-only",
                     "--color", "never",
                     "--output-last-message", lastMessageFile,
                     "--model", model,
                     prompt
                 })
            psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var output = File.Exists(lastMessageFile)
                ? (await File.ReadAllTextAsync(lastMessageFile, CancellationToken.None)).Trim()
                : "";
            if (output.Length == 0) output = AiCliSupport.StripAnsi(stdout).Trim();

            return (process.ExitCode, output, AiCliSupport.StripAnsi(stderr));
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }
        finally
        {
            try { File.Delete(lastMessageFile); } catch { /* best effort */ }
        }
    }
}
