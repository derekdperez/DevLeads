# Public API Surface

Only public/high-level symbols are listed. Implementation bodies are intentionally omitted.

| File | Line | Kind | Signature |
| --- | --- | --- | --- |
| src/DevLeads.Core/Ai/AiTriagePrompts.cs | 2 | class | public static class AiTriagePrompts |
| src/DevLeads.Core/Ai/AiTriagePrompts.cs | 61 | method | public static string BuildUserPrompt(AiTriageRequest r) |
| src/DevLeads.Core/Ai/AiTriagePrompts.cs | 95 | method | public static string BuildBatchUserPrompt(IReadOnlyList<AiBatchTriageItem> items) |
| src/DevLeads.Core/Ai/ContentPrompts.cs | 6 | class | public static class ContentPrompts |
| src/DevLeads.Core/Ai/ContentPrompts.cs | 10 | method | public static string BuildTopicPrompt(IReadOnlyList<TrendSignal> signals, string operatorSkills, IReadOnlyList<string> existingTopicTitles, int maxTopics) |
| src/DevLeads.Core/Ai/ContentPrompts.cs | 55 | method | public static string BuildDraftPrompt(ContentTopic topic, ContentFormat format, OperatorSettings op, string operatorSkills) |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 4 | class | public sealed class AiTriageRequest |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 8 | property | public string Title { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 9 | property | public string Body { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 10 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 11 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 12 | property | public IReadOnlyList<string> MatchedTerms { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 13 | property | public decimal HeuristicScore { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 14 | property | public string OperatorSkills { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 17 | property | public string CampaignObjective { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 21 | class | public sealed class AiTriageResponse |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 25 | property | public bool Succeeded { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 26 | property | public AiTriageResult? Result { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 27 | property | public string Provider { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 28 | property | public string Model { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 29 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 30 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 31 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 32 | property | public bool Retryable { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 34 | class | public sealed class AiShortlistItem |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 38 | property | public string Id { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 39 | property | public string Title { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 40 | property | public string Snippet { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 41 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 42 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 43 | property | public IReadOnlyList<string> MatchedTerms { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 44 | property | public decimal HeuristicScore { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 46 | class | public sealed class AiShortlistDecision |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 49 | property | public string Id { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 50 | property | public bool ShouldTriage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 51 | property | public string Reason { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 53 | class | public sealed class AiShortlistResponse |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 56 | property | public bool Succeeded { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 57 | property | public IReadOnlyList<AiShortlistDecision> Decisions { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 58 | property | public string Provider { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 59 | property | public string Model { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 60 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 61 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 62 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 63 | property | public bool Retryable { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 65 | interface | public interface IAiBatchShortlistProvider |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 75 | class | public sealed class AiBatchTriageItem |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 79 | property | public string Id { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 80 | property | public AiTriageRequest Request { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 82 | class | public sealed class AiBatchTriageResponse |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 86 | property | public bool Succeeded { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 88 | property | public string Provider { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 89 | property | public string Model { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 90 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 91 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 92 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 93 | property | public bool Retryable { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 95 | interface | public interface IAiBatchTriageProvider |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 107 | interface | public interface IAiTriageProvider |
| src/DevLeads.Core/AiTriageResult.cs | 4 | class | public sealed class AiTriageResult |
| src/DevLeads.Core/AiTriageResult.cs | 12 | property | public bool IsTechnicalProblem { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 14 | property | public bool IsEmergency { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 17 | property | public string PaymentIntent { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 25 | property | public bool AssistanceRequested { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 32 | property | public string? RejectReason { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 35 | property | public string ProblemCategory { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 38 | property | public List<string> DetectedStack { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 41 | property | public string EstimatedCause { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 44 | property | public string FirstDiagnosticStep { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 47 | property | public int? EstimatedFixMinutesMin { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 50 | property | public int? EstimatedFixMinutesMax { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 53 | property | public decimal AiConfidence { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 56 | property | public string OutreachRecommendation { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 4 | class | public sealed class SourceConnectorConfig |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 8 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 9 | property | public int MaxItems { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 10 | property | public IReadOnlyList<string> Terms { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 12 | property | public DateTimeOffset? Since { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 13 | property | public IReadOnlyList<string> SkillTerms { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 17 | class | public sealed class ConnectorHealth |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 21 | property | public bool Healthy { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 22 | property | public string Message { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 23 | property | public int ItemCount { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 24 | property | public DateTimeOffset CheckedAt { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 26 | interface | public interface ISourceConnector |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 2 | class | public class AiTriageRun |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 7 | property | public long OpportunityId { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 8 | property | public string Provider { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 10 | property | public string Model { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 11 | property | public string PromptVersion { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 12 | property | public AiJobStatus Status { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 13 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 15 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 16 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 17 | property | public DateTimeOffset StartedAt { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 19 | property | public DateTimeOffset? CompletedAt { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 20 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 2 | class | public class AuditEvent |
| src/DevLeads.Core/Entities/AuditEvent.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 7 | property | public string EntityType { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 8 | property | public long EntityId { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 9 | property | public string EventType { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 10 | property | public string Actor { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 11 | property | public string Description { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 12 | property | public string MetadataJson { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 13 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 2 | class | public class Campaign |
| src/DevLeads.Core/Entities/Campaign.cs | 10 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 11 | property | public string Key { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 14 | property | public string Name { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 16 | property | public string Emoji { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 17 | property | public string Objective { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 23 | property | public bool Enabled { get; } |
| src/DevLeads.Core/Entities/Campaign.cs | 26 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 2 | class | public class ContentDraft |
| src/DevLeads.Core/Entities/ContentDraft.cs | 9 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 10 | property | public long TopicId { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 11 | property | public ContentFormat Format { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 13 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 14 | property | public string BodyMarkdown { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 15 | property | public int WordCount { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 16 | property | public ContentDraftStatus Status { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 18 | property | public string Provider { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 20 | property | public string Model { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 21 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 23 | property | public DateTimeOffset UpdatedAt { get; } |
| src/DevLeads.Core/Entities/ContentDraft.cs | 24 | property | public ContentTopic? Topic { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 2 | class | public class ContentTopic |
| src/DevLeads.Core/Entities/ContentTopic.cs | 9 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 10 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 12 | property | public string Angle { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 15 | property | public string Rationale { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 18 | property | public double InterestScore { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 21 | property | public string SkillsJson { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 24 | property | public string EvidenceJson { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 27 | property | public string SuggestedFormatsCsv { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 30 | property | public ContentTopicStatus Status { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 32 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 34 | property | public DateTimeOffset UpdatedAt { get; } |
| src/DevLeads.Core/Entities/ContentTopic.cs | 35 | property | public List<ContentDraft> Drafts { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 2 | class | public class OperatorSettings |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 7 | property | public string OperatorName { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 10 | property | public string BusinessName { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 11 | property | public string Location { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 12 | property | public string RemoteAvailability { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 13 | property | public string CoreSkills { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 14 | property | public string SecondarySkills { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 15 | property | public double MinimumFee { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 16 | property | public string PreferredPaymentTerms { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 17 | property | public bool EmergencyAvailability { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 18 | property | public string AiProvider { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 22 | property | public string AiModel { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 25 | property | public string OpenCodeCliPath { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 32 | property | public string PromptVersion { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 33 | property | public int MaxAiCallsPerHour { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 34 | property | public double MaxAiSpendPerDay { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 35 | property | public double MinPreFilterScoreForAi { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 36 | property | public double MinAiConfidenceForDraft { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 37 | property | public double ManualReviewConfidenceThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 38 | property | public int AiRetryCount { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 39 | property | public int AiTimeoutSeconds { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 40 | property | public OutreachMode DefaultOutreachMode { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 43 | property | public bool GlobalAutoModeEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 44 | property | public bool GlobalKillSwitch { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 45 | property | public int MaxSendsPerHour { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 46 | property | public int MaxSendsPerDay { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 47 | property | public double RequireApprovalAboveRisk { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 48 | property | public double RequireApprovalBelowConfidence { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 49 | property | public bool SuppressionListEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 50 | property | public bool AuditLoggingEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 51 | property | public double DraftScoreThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 54 | property | public double AlertScoreThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 55 | property | public long? SelectedCampaignId { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 58 | property | public bool DiscoveryEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 61 | property | public bool ContentDiscoveryEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 64 | property | public int StaleItemMaxAgeHours { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 65 | property | public int FollowUpDefaultHours { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 2 | class | public class Opportunity |
| src/DevLeads.Core/Entities/Opportunity.cs | 8 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 9 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 11 | property | public string Summary { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 12 | property | public long? CampaignId { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 15 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 16 | property | public string SourceUrl { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 17 | property | public string? AuthorName { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 18 | property | public string? AuthorProfileUrl { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 19 | property | public OpportunityStatus Status { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 21 | property | public Priority Priority { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 22 | property | public double Score { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 25 | property | public double UrgencyScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 26 | property | public double StackFitScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 27 | property | public double BusinessValueScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 28 | property | public double ReachabilityScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 29 | property | public double CompetitionScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 30 | property | public double TrustScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 31 | property | public string ProblemType { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 34 | property | public string PaymentIntent { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 36 | property | public bool? AssistanceRequested { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 39 | property | public string DetectedStackJson { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 40 | property | public string SuggestedFirstStep { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 41 | property | public string EstimatedCause { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 42 | property | public double? EstimatedFeeMin { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 44 | property | public double? EstimatedFeeMax { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 45 | property | public bool FeeIsEstimate { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 51 | property | public int? EstimatedFixMinutesMin { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 52 | property | public int? EstimatedFixMinutesMax { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 53 | property | public double AiConfidence { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 55 | property | public OutreachRecommendation OutreachRecommendation { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 56 | property | public string? RejectionReason { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 57 | property | public AiJobStatus AiJobStatus { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 59 | property | public double HeuristicScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 62 | property | public string MatchedTermsJson { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 63 | property | public string? PreFilterRejectReason { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 64 | property | public bool AutoEligible { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 67 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 69 | property | public DateTimeOffset FirstSeenAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 70 | property | public DateTimeOffset LastSeenAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 71 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 72 | property | public DateTimeOffset UpdatedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 73 | property | public DateTimeOffset? NextFollowUpAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 76 | property | public string? WorkNotes { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 77 | property | public List<AiTriageRun> TriageRuns { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 80 | property | public List<OutreachAttempt> OutreachAttempts { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 81 | property | public List<Quote> Quotes { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 82 | property | public List<WorkSession> WorkSessions { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 2 | class | public class OutreachAttempt |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 7 | property | public long OpportunityId { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 8 | property | public OutreachChannel Channel { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 10 | property | public OutreachMode Mode { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 11 | property | public string? Subject { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 13 | property | public string Body { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 14 | property | public string TemplateKey { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 15 | property | public OutreachStatus Status { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 17 | property | public bool RequiresApproval { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 18 | property | public DateTimeOffset? ApprovedAt { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 20 | property | public DateTimeOffset? SentAt { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 21 | property | public DateTimeOffset? ResponseReceivedAt { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 22 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 23 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 24 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 2 | class | public class QueryPack |
| src/DevLeads.Core/Entities/QueryPack.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 7 | property | public string Name { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 8 | property | public string Description { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 9 | property | public string Terms { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 12 | property | public bool IsHighPriority { get; } |
| src/DevLeads.Core/Entities/QueryPack.cs | 15 | property | public bool IsNegative { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 2 | class | public class Quote |
| src/DevLeads.Core/Entities/Quote.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 7 | property | public long OpportunityId { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 8 | property | public double Amount { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 10 | property | public bool PaymentDueUponCompletion { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 11 | property | public double? DiagnosticFee { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 12 | property | public string Scope { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 14 | property | public string Exclusions { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 15 | property | public string? PaymentUrl { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 16 | property | public QuoteStatus Status { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 18 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 20 | property | public DateTimeOffset? SentAt { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 21 | property | public DateTimeOffset? PaidAt { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 22 | property | public DateTimeOffset? DueAt { get; } |
| src/DevLeads.Core/Entities/Quote.cs | 23 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 2 | class | public class RawSourceItem |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 9 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 10 | property | public long? OpportunityId { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 11 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 13 | property | public string ExternalId { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 14 | property | public string Url { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 15 | property | public string? AuthorName { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 16 | property | public string? AuthorProfileUrl { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 17 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 18 | property | public string BodyText { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 19 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 21 | property | public DateTimeOffset FetchedAt { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 22 | property | public string RawJson { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 24 | property | public string ContentHash { get; } |
| src/DevLeads.Core/Entities/RawSourceItem.cs | 27 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 2 | class | public class Skill |
| src/DevLeads.Core/Entities/Skill.cs | 10 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 11 | property | public string Name { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 14 | property | public string Category { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 17 | property | public int Weight { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 20 | property | public bool Enabled { get; } |
| src/DevLeads.Core/Entities/Skill.cs | 23 | property | public string Aliases { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 2 | class | public class SourceConfig |
| src/DevLeads.Core/Entities/SourceConfig.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 7 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 8 | property | public string DisplayName { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 9 | property | public bool Enabled { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 10 | property | public long? CampaignId { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 13 | property | public int PollIntervalMinutes { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 15 | property | public int MaxItemsPerRun { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 16 | property | public string QueryPacksCsv { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 19 | property | public string ParametersJson { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 22 | property | public double MinPreFilterScore { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 24 | property | public double MinOpportunityScore { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 25 | property | public double DraftThreshold { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 26 | property | public double AlertThreshold { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 27 | property | public bool AutoModeEligible { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 29 | property | public bool LastRunHealthy { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 32 | property | public string? LastRunMessage { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 33 | property | public int LastRunItemCount { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 34 | property | public DateTimeOffset? LastRunAt { get; } |
| src/DevLeads.Core/Entities/SourceConfig.cs | 35 | property | public DateTimeOffset? NextRunAt { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 2 | class | public class SuppressionEntry |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 7 | property | public string ContactValue { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 8 | property | public SuppressionContactType ContactType { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 9 | property | public string Reason { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 10 | property | public string Source { get; } |
| src/DevLeads.Core/Entities/SuppressionEntry.cs | 11 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 2 | class | public class TrendSignal |
| src/DevLeads.Core/Entities/TrendSignal.cs | 9 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 10 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 13 | property | public string ExternalId { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 14 | property | public string Url { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 16 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 17 | property | public string Snippet { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 18 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 20 | property | public DateTimeOffset FetchedAt { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 21 | property | public double Engagement { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 24 | property | public string MatchedSkillsJson { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 27 | property | public double Hotness { get; } |
| src/DevLeads.Core/Entities/TrendSignal.cs | 30 | property | public bool UsedInTopic { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 2 | class | public class TrendSource |
| src/DevLeads.Core/Entities/TrendSource.cs | 10 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 11 | property | public string SeedKey { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 14 | property | public string Kind { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 17 | property | public string DisplayName { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 19 | property | public string ParametersJson { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 20 | property | public bool Enabled { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 21 | property | public int PollIntervalMinutes { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 24 | property | public int MaxItemsPerRun { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 25 | property | public bool RequireSkillMatch { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 31 | property | public bool LastRunHealthy { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 34 | property | public string? LastRunMessage { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 35 | property | public int LastRunItemCount { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 36 | property | public DateTimeOffset? LastRunAt { get; } |
| src/DevLeads.Core/Entities/TrendSource.cs | 37 | property | public DateTimeOffset? NextRunAt { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 2 | class | public class WorkSession |
| src/DevLeads.Core/Entities/WorkSession.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 7 | property | public long OpportunityId { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 8 | property | public DateTimeOffset StartedAt { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 10 | property | public DateTimeOffset? EndedAt { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 11 | property | public WorkSessionStatus Status { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 12 | property | public string AccessChecklistJson { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 15 | property | public string Notes { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 16 | property | public string FixSummary { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 17 | property | public string ClientConfirmation { get; } |
| src/DevLeads.Core/Entities/WorkSession.cs | 18 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Enums.cs | 2 | enum | public enum OpportunityStatus |
| src/DevLeads.Core/Enums.cs | 32 | enum | public enum Priority |
| src/DevLeads.Core/Enums.cs | 42 | enum | public enum AiJobStatus |
| src/DevLeads.Core/Enums.cs | 54 | enum | public enum OutreachRecommendation |
| src/DevLeads.Core/Enums.cs | 64 | enum | public enum OutreachMode |
| src/DevLeads.Core/Enums.cs | 74 | enum | public enum OutreachStatus |
| src/DevLeads.Core/Enums.cs | 86 | enum | public enum OutreachChannel |
| src/DevLeads.Core/Enums.cs | 96 | enum | public enum QuoteStatus |
| src/DevLeads.Core/Enums.cs | 111 | enum | public enum WorkSessionStatus |
| src/DevLeads.Core/Enums.cs | 122 | enum | public enum ContentTopicStatus |
| src/DevLeads.Core/Enums.cs | 130 | enum | public enum ContentDraftStatus |
| src/DevLeads.Core/Enums.cs | 139 | enum | public enum ContentFormat |
| src/DevLeads.Core/Enums.cs | 149 | enum | public enum SuppressionContactType |
| src/DevLeads.Core/HeuristicPreFilter.cs | 6 | class | public sealed class HeuristicPreFilter |
| src/DevLeads.Core/HeuristicPreFilter.cs | 90 | method | public static bool HasPayLanguage(string text) |
| src/DevLeads.Core/HeuristicPreFilter.cs | 97 | method | public PreFilterResult Analyze(RawSourceItem item, IReadOnlyCollection<string>? packNames = null) |
| src/DevLeads.Core/LeadQualityRules.cs | 4 | class | public static class LeadQualityRules |
| src/DevLeads.Core/LeadQualityRules.cs | 116 | method | public static bool IsPromotionalAnnouncement(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 126 | method | public static bool IsReplyFeedItem(string title) |
| src/DevLeads.Core/LeadQualityRules.cs | 158 | method | public static bool IsAlreadyClaimed(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 170 | method | public static int CompetingResponseCount(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 185 | method | public static bool IsAiAgentTaskPost(string title, string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 191 | method | public static bool HasThirdPartyPayOffer(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 195 | method | public static bool IsVendorControlledSupportRequest(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 198 | method | public static bool IsNonHirableVendorSupportRequest(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 201 | method | public static bool IsResolvedOrClosedRequest(string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 204 | method | public static bool IsConcretePaidSource(string sourceKey) |
| src/DevLeads.Core/LeadQualityRules.cs | 213 | method | public static bool IsDashboardWorthyLead(string sourceKey, string paymentIntent, bool? assistanceRequested, bool feeIsEstimate, string text) |
| src/DevLeads.Core/LeadQualityRules.cs | 234 | method | public static string NormalizeDuplicateTitle(string title) |
| src/DevLeads.Core/LeadQualityRules.cs | 240 | method | public static string? HostFromUrl(string? url) |
| src/DevLeads.Core/LeadQualityRules.cs | 246 | method | public static bool SharesDuplicateClue(string leftText, string rightText) |
| src/DevLeads.Core/OfferedCompensation.cs | 4 | class | public static class OfferedCompensation |
| src/DevLeads.Core/PreFilterResult.cs | 2 | class | public sealed class PreFilterResult |
| src/DevLeads.Core/PreFilterResult.cs | 6 | property | public bool ShouldAnalyzeWithAi { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 7 | property | public int KeywordHitCount { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 8 | property | public int HighPriorityHitCount { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 9 | property | public int NegativeHitCount { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 10 | property | public int PayIntentHitCount { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 12 | property | public decimal HeuristicScore { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 13 | property | public List<string> MatchedTerms { get; } |
| src/DevLeads.Core/PreFilterResult.cs | 14 | property | public string? RejectReason { get; } |
| src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs | 2 | record | public sealed record QueryPackSeed (string Name, string Description, bool IsHighPriority, bool IsNegative, string[] Terms) |
| src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs | 2 | method | public sealed record QueryPackSeed(string Name, string Description, bool IsHighPriority, bool IsNegative, string[] Terms) |
| src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs | 5 | class | public static class DefaultQueryPacks |
| src/DevLeads.Core/QueryPacks/IQueryPackProvider.cs | 2 | interface | public interface IQueryPackProvider |
| src/DevLeads.Core/RedFlagDetector.cs | 2 | record | public sealed record RedFlagResult (bool IsRedFlagged, IReadOnlyList<string> Reasons) |
| src/DevLeads.Core/RedFlagDetector.cs | 2 | method | public sealed record RedFlagResult(bool IsRedFlagged, IReadOnlyList<string> Reasons) |
| src/DevLeads.Core/RedFlagDetector.cs | 8 | class | public static class RedFlagDetector |
| src/DevLeads.Core/RedFlagDetector.cs | 53 | method | public static RedFlagResult Scan(string title, string body) |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 4 | class | public sealed class ScoreBreakdown |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 8 | property | public double UrgencyScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 9 | property | public double StackFitScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 10 | property | public double BusinessValueScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 11 | property | public double ReachabilityScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 12 | property | public double CompetitionScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 13 | property | public double TrustScore { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 14 | property | public double Total { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 15 | property | public Priority Priority { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 17 | class | public sealed class ScoringInput |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 21 | property | public AiTriageResult? Ai { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 22 | property | public PreFilterResult? PreFilter { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 23 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 24 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 25 | property | public bool RedFlagged { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 26 | property | public bool HasContact { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 27 | property | public IReadOnlyList<SkillMatch>? SkillMatches { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 33 | property | public double? OfferedAmount { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 36 | property | public bool ClaimedByOthers { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 39 | property | public int CompetingResponses { get; } |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 43 | class | public static class OpportunityScorer |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 61 | method | public static ScoreBreakdown Score(ScoringInput input, DateTimeOffset now) |
| src/DevLeads.Core/Scoring/OpportunityScorer.cs | 125 | method | public static Priority ToPriority(double total) |
| src/DevLeads.Core/Skills/DefaultSkills.cs | 4 | class | public static class DefaultSkills |
| src/DevLeads.Core/Skills/DefaultSkills.cs | 14 | property | public static IReadOnlyList<Skill> All { get; } |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 4 | record | public sealed record SkillMatch (string Name, int Weight) |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 4 | method | public sealed record SkillMatch(string Name, int Weight) |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 7 | class | public static class SkillMatcher |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 11 | method | public static List<SkillMatch> Match(string text, IEnumerable<Skill> skills) |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 26 | method | public static double FitScore(IReadOnlyList<SkillMatch> matches) |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 39 | method | public static string PromptSummary(IEnumerable<Skill> skills, int maxItems = 40) |
| src/DevLeads.Core/Skills/SkillMatcher.cs | 49 | method | public static List<string> SearchTerms(IEnumerable<Skill> skills, int max = 12) |
| src/DevLeads.Core/SourceUrlCanonicalizer.cs | 2 | class | public static class SourceUrlCanonicalizer |
| src/DevLeads.Core/SourceUrlCanonicalizer.cs | 10 | method | public static string? Canonicalize(string? sourceUrl) |
| src/DevLeads.Core/Templates/EmergencyChecklists.cs | 2 | record | public sealed record EmergencyChecklist (string Key, string Name, string[] Items) |
| src/DevLeads.Core/Templates/EmergencyChecklists.cs | 2 | method | public sealed record EmergencyChecklist(string Key, string Name, string[] Items) |
| src/DevLeads.Core/Templates/EmergencyChecklists.cs | 4 | class | public static class EmergencyChecklists |
| src/DevLeads.Core/Templates/EmergencyChecklists.cs | 29 | method | public static EmergencyChecklist SuggestFor(string problemCategory) |
| src/DevLeads.Core/Templates/PricingTiers.cs | 2 | record | public sealed record PricingTier (string Name, string UseCase, double SuggestedMin, double SuggestedMax) |
| src/DevLeads.Core/Templates/PricingTiers.cs | 2 | method | public sealed record PricingTier(string Name, string UseCase, double SuggestedMin, double SuggestedMax) |
| src/DevLeads.Core/Templates/PricingTiers.cs | 4 | class | public static class PricingTiers |
| src/DevLeads.Core/Templates/ResponseTemplates.cs | 2 | record | public sealed record ResponseTemplate (string Key, string Name, string Channel, string Body) |
| src/DevLeads.Core/Templates/ResponseTemplates.cs | 2 | method | public sealed record ResponseTemplate(string Key, string Name, string Channel, string Body) |
| src/DevLeads.Core/Templates/ResponseTemplates.cs | 4 | class | public static class ResponseTemplates |
| src/DevLeads.Core/Templates/ResponseTemplates.cs | 54 | method | public static ResponseTemplate Get(string key) |
| src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs | 6 | class | public sealed class AiTriageRouter |
| src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs | 30 | method | public IAiTriageProvider Resolve(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs | 52 | method | public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs | 76 | method | public async Task<AiBatchTriageResponse> TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs | 107 | method | public async Task<AiShortlistResponse> ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs | 9 | class | public sealed class AnthropicTriageProvider : IAiTriageProvider |
| src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs | 21 | method | public bool IsAvailable(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs | 23 | method | public string AvailabilityMessage(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs | 30 | method | public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs | 7 | class | public sealed class HeuristicTriageProvider : IAiTriageProvider |
| src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs | 15 | method | public bool IsAvailable(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs | 17 | method | public string AvailabilityMessage(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs | 100 | method | public Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 11 | class | public sealed class OpenCodeTriageProvider : IAiTriageProvider, IAiBatchShortlistProvider, IAiBatchTriageProvider |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 31 | method | public bool IsAvailable(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 33 | method | public string AvailabilityMessage(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 39 | method | public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 127 | method | public async Task<AiBatchTriageResponse> TriageBatchAsync(IReadOnlyList<AiBatchTriageItem> items, OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 242 | method | public async Task<AiShortlistResponse> ShortlistAsync(IReadOnlyList<AiShortlistItem> items, OperatorSettings settings, int maxSelections, string campaignObjective, CancellationToken ct) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 433 | class | private sealed class ShortlistOutput |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 436 | property | public List<ShortlistSelection> Selected { get; } |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 438 | class | private sealed class ShortlistSelection |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 441 | property | public string Id { get; } |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 442 | property | public string? Reason { get; } |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 444 | method | public static string ResolveCliPath(OperatorSettings settings) |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 497 | method | public static void ResetProbe() |
| src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs | 551 | method | public static string? ExtractJsonObject(string text) |
| src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs | 6 | class | public static class ConnectorSupport |
| src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs | 11 | method | public static string ContentHash(string sourceKey, string externalId, string title) |
| src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs | 19 | method | public static RawSourceItem NewItem(string sourceKey, string externalId, string title, string body, string url, string? author, string? authorUrl, DateTimeOffset postedAt, string rawJson) |
| src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs | 7 | class | public sealed class GitHubSearchConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs | 28 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs | 155 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs | 7 | class | public sealed class HackerNewsConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs | 22 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs | 79 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/OpireConnector.cs | 7 | class | public sealed class OpireConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/OpireConnector.cs | 26 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/OpireConnector.cs | 119 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RedditConnector.cs | 7 | class | public sealed class RedditConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/RedditConnector.cs | 26 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RedditConnector.cs | 148 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs | 7 | class | public sealed class RemotiveConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs | 41 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs | 103 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RssConnector.cs | 8 | class | public sealed class RssConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/RssConnector.cs | 38 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/RssConnector.cs | 167 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs | 7 | class | public sealed class StackExchangeConnector : ISourceConnector |
| src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs | 22 | method | public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct) |
| src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs | 100 | method | public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Data/DatabaseSeeder.cs | 8 | class | public static class DatabaseSeeder |
| src/DevLeads.Infrastructure/Data/DatabaseSeeder.cs | 16 | method | public static async Task InitializeAsync(DevLeadsDbContext db, CancellationToken ct = default) |
| src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs | 7 | class | public class DevLeadsDbContext : DbContext |
| src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs | 30 | class | private sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long> |
| src/DevLeads.Infrastructure/DependencyInjection.cs | 16 | class | public static class DependencyInjection |
| src/DevLeads.Infrastructure/DependencyInjection.cs | 19 | method | public static IServiceCollection AddDevLeads(this IServiceCollection services, string connectionString) |
| src/DevLeads.Infrastructure/DependencyInjection.cs | 83 | method | public static async Task InitializeDevLeadsAsync(this IServiceProvider services) |
| src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs | 5 | class | public sealed class DbQueryPackProvider : IQueryPackProvider |
| src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs | 18 | method | public IReadOnlyList<string> GetTerms(string packName) |
| src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs | 24 | method | public IReadOnlyList<string> GetHighPriorityTerms() |
| src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs | 28 | method | public IReadOnlyList<string> GetHighPriorityTerms(IReadOnlyCollection<string> packNames) |
| src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs | 34 | method | public IReadOnlyList<string> GetNegativeTerms() |
| src/DevLeads.Infrastructure/Services/AuditService.cs | 6 | class | public sealed class AuditService |
| src/DevLeads.Infrastructure/Services/AuditService.cs | 12 | method | public void Record(string entityType, long entityId, string eventType, string description, string actor = "system", object? metadata = null) |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 12 | class | public sealed class ContentStudioService |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 198 | class | private sealed class TopicOutput |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 201 | property | public List<TopicSuggestion> Topics { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 203 | class | private sealed class TopicSuggestion |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 206 | property | public string Title { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 207 | property | public string? Angle { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 208 | property | public string? Rationale { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 209 | property | public double InterestScore { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 210 | property | public List<string>? Skills { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 211 | property | public List<string>? Formats { get; } |
| src/DevLeads.Infrastructure/Services/ContentStudioService.cs | 212 | property | public List<int>? Evidence { get; } |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 2 | class | public sealed class DiscoveryActivityTracker |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 10 | record | public sealed record ActivityEvent (DateTimeOffset At, string Kind, string SourceKey, string Message) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 10 | method | public sealed record ActivityEvent(DateTimeOffset At, string Kind, string SourceKey, string Message) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 11 | record | public sealed record RunningSource (string SourceKey, string DisplayName, DateTimeOffset StartedAt) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 11 | method | public sealed record RunningSource(string SourceKey, string DisplayName, DateTimeOffset StartedAt) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 17 | method | public void RunStarted(string sourceKey, string displayName) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 26 | method | public void RunCompleted(string sourceKey, bool healthy, string message) |
| src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs | 35 | method | public void LeadCreated(string sourceKey, string title, double score) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 14 | class | public sealed class LeadIngestionService |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 37 | method | public async Task<Opportunity?> IngestAsync(RawSourceItem item, SourceConfig source, CancellationToken ct, AiTriageResponse? precomputedTriage = null) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 156 | method | public async Task<bool> RecordRawOnlyAsync(RawSourceItem item, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 186 | method | public async Task<Opportunity> CreateManualAsync(string title, string body, string sourceUrl, string? author, string? authorUrl, CancellationToken ct, long? campaignId = null) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 236 | method | public async Task RerunAsync(long opportunityId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 553 | method | public static OutreachRecommendation MapRecommendation(string value) |
| src/DevLeads.Infrastructure/Services/LeadIngestionService.cs | 605 | method | public async Task<bool> IsOverAiBudgetAsync(OperatorSettings settings, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/MaintenanceService.cs | 7 | class | public sealed class MaintenanceService |
| src/DevLeads.Infrastructure/Services/MaintenanceService.cs | 19 | method | public async Task<int> ArchiveStaleLeadsAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/MaintenanceService.cs | 44 | method | public async Task<int> RejectNonHirableVendorSupportAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/MaintenanceService.cs | 90 | method | public async Task<int> FlagOverdueQuotesAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/MaintenanceService.cs | 100 | method | public async Task<int> DueFollowUpCountAsync(CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 8 | class | public sealed class OutreachService |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 23 | method | public async Task<OutreachAttempt> GenerateDraftAsync(long opportunityId, string templateKey, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 55 | method | public async Task ApproveAsync(long attemptId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 97 | method | public async Task CancelAsync(long attemptId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 105 | method | public async Task MarkRespondedAsync(long opportunityId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 116 | method | public Task<bool> IsSuppressedAsync(string contact, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/OutreachService.cs | 119 | method | public async Task AddSuppressionAsync(string contact, SuppressionContactType type, string reason, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/QuoteService.cs | 8 | class | public sealed class QuoteService |
| src/DevLeads.Infrastructure/Services/QuoteService.cs | 20 | method | public async Task<Quote> GenerateAsync(long opportunityId, double? amount, bool dueOnCompletion, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/QuoteService.cs | 49 | method | public async Task SendAsync(long quoteId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/QuoteService.cs | 59 | method | public async Task MarkPaidAsync(long quoteId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/QuoteService.cs | 69 | method | public async Task MarkOverdueAsync(long quoteId, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 13 | class | public sealed class SourceRunner |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 39 | method | public async Task<int> RunAsync(SourceConfig source, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 389 | method | public async Task<ConnectorHealth> CheckHealthAsync(string sourceKey, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 412 | class | private sealed class ShortlistGate |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 431 | property | public bool Enabled { get; } |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 434 | property | public IReadOnlySet<string> SelectedIds { get; } |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 435 | property | public int CandidateCount { get; } |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 437 | property | public string Provider { get; } |
| src/DevLeads.Infrastructure/Services/SourceRunner.cs | 438 | method | public bool ShouldRecordRawOnly(RawSourceItem item) |
| src/DevLeads.Infrastructure/Services/TrendScanService.cs | 10 | class | public sealed class TrendScanService |
| src/DevLeads.Infrastructure/Services/TrendScanService.cs | 31 | method | public async Task<int> RunDueAsync(bool force, CancellationToken ct) |
| src/DevLeads.Infrastructure/Services/TrendScanService.cs | 49 | method | public async Task<int> RunSourceAsync(TrendSource source, CancellationToken ct) |
| src/DevLeads.Infrastructure/Workers/ContentTrendWorker.cs | 10 | class | public sealed class ContentTrendWorker : BackgroundService |
| src/DevLeads.Infrastructure/Workers/DiscoveryWorker.cs | 9 | class | public sealed class DiscoveryWorker : BackgroundService |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 7 | class | public static class ApiEndpoints |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 11 | method | public static void MapDevLeadsApi(this WebApplication app) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 168 | record | public record ManualLeadDto (string Title, string Body, string? SourceUrl, string? Author, string? AuthorUrl, long? CampaignId = null) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 168 | method | public record ManualLeadDto(string Title, string Body, string? SourceUrl, string? Author, string? AuthorUrl, long? CampaignId = null) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 170 | record | public record DraftDto (string TemplateKey) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 170 | method | public record DraftDto(string TemplateKey) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 171 | record | public record QuoteDto (double? Amount, bool DueOnCompletion) |
| src/DevLeads.Web/Api/ApiEndpoints.cs | 171 | method | public record QuoteDto(double? Amount, bool DueOnCompletion) |
| src/DevLeads.Web/AppRestartService.cs | 4 | class | public sealed class AppRestartService |
| src/DevLeads.Web/AppRestartService.cs | 24 | method | public string? Restart() |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 6 | class | public static class UiHelpers |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 10 | method | public static string PriorityClass(Priority p) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 18 | method | public static string StatusClass(OpportunityStatus s) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 31 | method | public static string AiStatusClass(AiJobStatus s) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 40 | method | public static string Spaced(Enum e) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 46 | method | public static string Age(DateTimeOffset from) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 55 | method | public static string AgeClass(DateTimeOffset from) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 62 | method | public static bool? CompensationOffered(Opportunity o) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 82 | method | public static string Fee(double? min, double? max) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 85 | method | public static string Fee(Opportunity o) |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | 99 | method | public static List<string> ParseStringList(string json) |
