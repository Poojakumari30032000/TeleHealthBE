namespace Vitality.Services.Email;

public sealed class MailTemplateModel
{
    public string ToEmail { get; init; } = string.Empty;

    public string? ToName { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string Greeting { get; init; } = "Hi there,";

    public string? PreviewText { get; init; }

    public List<string> BodyParagraphs { get; init; } = new();

    public string? HighlightText { get; init; }

    public string? ButtonText { get; init; }

    public string? ButtonUrl { get; init; }

    public string? FooterNote { get; init; }
}
