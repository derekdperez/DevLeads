namespace DevLeads.Core.Entities;

/// <summary>
/// A generated piece of publishable content (blog post, article, white paper, research
/// paper, or LinkedIn post) for the operator to edit and post on their own channels.
/// </summary>
public class ContentDraft
{
    public long Id { get; set; }
    public long TopicId { get; set; }

    public ContentFormat Format { get; set; } = ContentFormat.BlogPost;
    public string Title { get; set; } = "";
    public string BodyMarkdown { get; set; } = "";
    public int WordCount { get; set; }

    public ContentDraftStatus Status { get; set; } = ContentDraftStatus.Draft;

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ContentTopic? Topic { get; set; }
}
