namespace VendaZap.Domain.Interfaces;

public record WhatsAppButton(string Id, string Title);

public record WhatsAppListRow(string Id, string Title, string? Description = null);

public record WhatsAppListSection(string Title, IEnumerable<WhatsAppListRow> Rows);

public interface IWhatsAppClient
{
    Task<string?> SendTextAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string message, CancellationToken ct = default);

    Task<string?> SendImageAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string imageUrl, string? caption = null, CancellationToken ct = default);

    Task<string?> SendInteractiveButtonsAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string bodyText, IEnumerable<WhatsAppButton> buttons, CancellationToken ct = default);

    Task<string?> SendInteractiveListAsync(
        string phoneNumberId, string accessToken, string toPhone,
        string bodyText, string buttonText, IEnumerable<WhatsAppListSection> sections,
        CancellationToken ct = default);
}
