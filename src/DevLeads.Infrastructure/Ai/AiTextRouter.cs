using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// Routes the app's long-form / free-text generation calls (outreach replies, content
/// drafts, the operator's own posts, thread summaries, optimization rewrites) to the
/// right CLI provider for each <see cref="AiFeature"/>. The two CLI-backed engines that
/// speak plain text are OpenCode (default) and Codex (OpenAI). Codex is used only when a
/// feature explicitly selects it; Heuristic reports a clear error (the operator opted
/// offline); every other provider (including Anthropic, which has no text path here)
/// falls back to OpenCode, matching the app's original always-OpenCode text behavior.
/// </summary>
public sealed class AiTextRouter
{
    private readonly OpenCodeTriageProvider _openCode;
    private readonly CodexCliProvider _codex;
    private readonly ILogger<AiTextRouter> _log;

    public AiTextRouter(OpenCodeTriageProvider openCode, CodexCliProvider codex, ILogger<AiTextRouter> log)
    {
        _openCode = openCode;
        _codex = codex;
        _log = log;
    }

    /// <summary>
    /// Runs <paramref name="prompt"/> through the provider/model configured for
    /// <paramref name="feature"/>. The returned tuple mirrors the CLI providers'
    /// GenerateTextAsync contract; callers own the output format. No cross-provider
    /// fallback — a failed Codex call reports its error so a pinned model is never
    /// silently swapped.
    /// </summary>
    public Task<(bool Succeeded, string Text, string Error, string Model)> GenerateTextAsync(
        AiFeature feature, string prompt, OperatorSettings settings, TimeSpan timeout, CancellationToken ct)
    {
        var featureSettings = settings.WithAiFor(feature);
        var provider = featureSettings.AiProvider;

        if (provider.Equals("Codex", StringComparison.OrdinalIgnoreCase))
            return _codex.GenerateTextAsync(prompt, featureSettings, timeout, ct);

        // Heuristic is the deliberate "offline, don't spend on AI" choice — surface it
        // rather than quietly using a CLI model the operator opted out of.
        if (provider.Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("AI feature {Feature} is set to Heuristic, which cannot generate text.", feature);
            return Task.FromResult((false, "",
                "This feature is set to Heuristic — choose OpenCode or Codex for it in Settings.",
                featureSettings.AiModel));
        }

        // OpenCode is the local text engine and the default for any non-Codex provider.
        // An opencode model id is "provider/model"; an inherited Anthropic id (or a blank)
        // is meaningless to the CLI, so fall back to OpenCode's own default in that case.
        // featureSettings is already a clone, so mutating it here is safe.
        if (string.IsNullOrWhiteSpace(featureSettings.AiModel) || !featureSettings.AiModel.Contains('/'))
            featureSettings.AiModel = OperatorSettings.DefaultOpenCodeModel;
        return _openCode.GenerateTextAsync(prompt, featureSettings, timeout, ct);
    }

    /// <summary>The provider name a feature will actually use — for pre-flight UI/guards.</summary>
    public string ProviderFor(OperatorSettings settings, AiFeature feature) => settings.AiFor(feature).Provider;
}
