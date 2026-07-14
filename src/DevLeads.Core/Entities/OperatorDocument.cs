namespace DevLeads.Core.Entities;

/// <summary>
/// A document the operator uploads once and the app hands out wherever it's needed —
/// the resume today; portfolios, cover letters, or case studies as features grow.
/// One current document per <see cref="Kind"/>; uploading again replaces it.
/// Served for download at /api/documents/{kind}.
/// </summary>
public class OperatorDocument
{
    public const string ResumeKind = "resume";

    public long Id { get; set; }

    /// <summary>"resume" (see <see cref="ResumeKind"/>); future kinds slot in beside it.</summary>
    public string Kind { get; set; } = ResumeKind;

    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    public byte[] Data { get; set; } = Array.Empty<byte>();

    public DateTimeOffset UploadedAt { get; set; }
}
