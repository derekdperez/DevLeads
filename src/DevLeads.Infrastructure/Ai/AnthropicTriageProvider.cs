using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// Calls Claude via the official Anthropic SDK with a single structured triage request.
/// Returns strict JSON validated against <see cref="AiTriageResult"/>.
/// </summary>
public sealed class AnthropicTriageProvider : IAiTriageProvider
{
    public string Name => "Anthropic";

    /// <summary>True when an API key is available (env <c>ANTHROPIC_API_KEY</c>).</summary>
    public static bool HasApiKey =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));

    public bool IsAvailable(OperatorSettings settings) => HasApiKey;
    public string AvailabilityMessage(OperatorSettings settings) =>
        HasApiKey ? "API key configured." : "ANTHROPIC_API_KEY is not set.";

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct)
    {
        var userPrompt = AiTriagePrompts.BuildUserPrompt(request);
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(AiTriagePrompts.JsonSchema)!;

        // AiModel follows the selected provider; an "provider/model" string belongs to
        // opencode, so fall back to the Anthropic default rather than sending a bad id.
        var model = string.IsNullOrWhiteSpace(settings.AiModel) || settings.AiModel.Contains('/')
            ? OperatorSettings.DefaultAnthropicModel
            : settings.AiModel.Trim();

        var response = new AiTriageResponse
        {
            Provider = Name,
            Model = model,
            RequestJson = JsonSerializer.Serialize(new { system = "DevLeads triage", user = userPrompt })
        };

        if (!HasApiKey)
        {
            response.Succeeded = false;
            response.Retryable = false;
            response.ErrorMessage = "ANTHROPIC_API_KEY is not configured.";
            return response;
        }

        try
        {
            var client = new AnthropicClient();
            var parameters = new MessageCreateParams
            {
                Model = model,
                MaxTokens = 1024,
                System = AiTriagePrompts.SystemPrompt,
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Medium,
                    Format = new JsonOutputFormat { Schema = schema }
                },
                Messages = [new() { Role = Role.User, Content = userPrompt }]
            };

            var message = await client.Messages.Create(parameters, cancellationToken: ct);

            var text = message.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text)
                .FirstOrDefault();

            response.ResponseJson = text;

            if (message.StopReason == "refusal")
            {
                response.Succeeded = false;
                response.Retryable = false;
                response.ErrorMessage = "Model refused the request.";
                return response;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "Empty response from model.";
                return response;
            }

            var result = JsonSerializer.Deserialize<AiTriageResult>(text, ParseOptions);
            if (result is null)
            {
                response.Succeeded = false;
                response.Retryable = true;
                response.ErrorMessage = "Failed to parse structured JSON.";
                return response;
            }

            response.Succeeded = true;
            response.Result = result;
            return response;
        }
        catch (JsonException ex)
        {
            response.Succeeded = false;
            response.Retryable = true; // invalid JSON -> retry once with the same prompt
            response.ErrorMessage = "JSON parse error: " + ex.Message;
            return response;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            response.Succeeded = false;
            // Rate limits / timeouts / 5xx are transient; treat unknown failures as retryable once.
            response.Retryable = true;
            response.ErrorMessage = ex.GetType().Name + ": " + ex.Message;
            return response;
        }
    }
}
