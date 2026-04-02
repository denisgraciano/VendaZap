namespace VendaZap.Domain.Interfaces;

/// <summary>
/// Structured AI response with detected intent and confidence score.
/// </summary>
public record AiConversationResponse(
    string Message,
    string Intent,
    IEnumerable<string> ProductsMentioned,
    string NextAction,
    int Confidence);

/// <summary>
/// A single message from the conversation history passed to the AI as context.
/// </summary>
public record ConversationMessageContext(string Role, string Content, DateTime SentAt);

/// <summary>
/// Sales context supplied to the AI engine so it can generate personalized responses.
/// </summary>
public record AiSalesContext(
    string TenantName,
    string ContactName,
    string? CurrentStage,
    IEnumerable<AiProductInfo> AvailableProducts,
    string? CartSummary,
    string? PaymentInfo,
    string? CustomInstructions = null);

/// <summary>
/// Lightweight product info sent to the AI.
/// </summary>
public record AiProductInfo(Guid Id, string Name, decimal Price, string Description, bool InStock);

/// <summary>
/// Domain contract for AI-powered conversation processing.
/// Implementations must detect intent, return structured responses and a confidence score.
/// </summary>
public interface IConversationAIService
{
    /// <summary>
    /// Process an inbound message and return a structured AI response.
    /// Detects one of the following intents:
    ///   consulta_produto | interesse_compra | finalizar_pedido | suporte | transferir_humano
    /// Returns confidence 0-100; below 70 signals fallback to human.
    /// </summary>
    Task<AiConversationResponse> ProcessMessageAsync(
        Guid tenantId,
        string userMessage,
        IEnumerable<ConversationMessageContext> history,
        AiSalesContext context,
        CancellationToken ct = default);
}
