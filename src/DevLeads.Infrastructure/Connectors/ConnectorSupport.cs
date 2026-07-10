using System.Security.Cryptography;
using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>Shared helpers for connectors: content hashing and parameter parsing.</summary>
public static class ConnectorSupport
{
    public const string HttpClientName = "connector";

    /// <summary>Stable hash used for duplicate detection across fetches.</summary>
    public static string ContentHash(string sourceKey, string externalId, string title)
    {
        var raw = $"{sourceKey}|{externalId}|{title.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    public static RawSourceItem NewItem(string sourceKey, string externalId, string title, string body,
        string url, string? author, string? authorUrl, DateTimeOffset postedAt, string rawJson)
        => new()
        {
            SourceKey = sourceKey,
            ExternalId = externalId,
            Title = title,
            BodyText = body,
            Url = url,
            AuthorName = author,
            AuthorProfileUrl = authorUrl,
            PostedAt = postedAt,
            FetchedAt = DateTimeOffset.UtcNow,
            RawJson = rawJson,
            ContentHash = ContentHash(sourceKey, externalId, title)
        };
}
