using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.AI;

public class OpenAiConversationService : IAiConversationService, IConversationAIService
{
    private readonly OpenAIClient _client;
    private readonly ILogger<OpenAiConversationService> _logger;
    private readonly string _model;

    private static readonly HashSet<string> ValidIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "consulta_produto",
        "interesse_compra",
        "finalizar_pedido",
        "suporte",
        "transferir_humano"
    };

    public OpenAiConversationService(IConfiguration config, ILogger<OpenAiConversationService> logger)
    {
        _logger = logger;
        var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured.");
        _model = config["OpenAI:Model"] ?? "gpt-4o";
        _client = new OpenAIClient(apiKey);
    }

    // ── IConversationAIService (Domain) ─────────────────────────────────────

    public async Task<AiConversationResponse> ProcessMessageAsync(
        Guid tenantId,
        string userMessage,
        IEnumerable<ConversationMessageContext> history,
        AiSalesContext context,
        CancellationToken ct = default)
    {
        var systemPrompt = BuildStructuredSalesPrompt(context);
        var chatClient = _client.GetChatClient(_model);

        try
        {
            var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };

            // Load last 20 messages as conversation history
            foreach (var msg in history.TakeLast(20))
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new UserChatMessage(msg.Content));
                else
                    messages.Add(new AssistantChatMessage(msg.Content));
            }

            messages.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken: ct);
            var raw = response.Value.Content[0].Text ?? "{}";

            return ParseStructuredResponse(raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI structured response error for tenant {TenantId}", tenantId);
            return new AiConversationResponse(
                Message: "Desculpe, estou com dificuldades no momento. Pode tentar novamente?",
                Intent: "suporte",
                ProductsMentioned: [],
                NextAction: "retry",
                Confidence: 0);
        }
    }

    // ── IAiConversationService (Application – backward compat) ───────────────

    public async Task<string> GetSalesResponseAsync(
        Guid tenantId, string userMessage, SalesContext context, CancellationToken ct = default)
    {
        var aiContext = new AiSalesContext(
            context.TenantName,
            context.ContactName,
            context.CurrentStage,
            context.AvailableProducts.Select(p => new AiProductInfo(p.Id, p.Name, p.Price, p.Description, p.InStock)),
            context.CartSummary,
            context.PaymentInfo,
            context.CustomInstructions);

        var result = await ProcessMessageAsync(tenantId, userMessage, [], aiContext, ct);
        return result.Message;
    }

    public async Task<string> GetResponseAsync(
        Guid tenantId, string conversationThreadId, string userMessage,
        string systemContext, CancellationToken ct = default)
    {
        var chatClient = _client.GetChatClient(_model);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemContext),
            new UserChatMessage(userMessage)
        };

        var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        return response.Value.Content[0].Text ?? "";
    }

    public Task<string> CreateThreadAsync(CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid().ToString("N"));

    public Task<bool> DeleteThreadAsync(string threadId, CancellationToken ct = default)
        => Task.FromResult(true);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildStructuredSalesPrompt(AiSalesContext context)
    {
        var productsText = context.AvailableProducts.Any()
            ? string.Join("\n", context.AvailableProducts.Select(p =>
                $"- {p.Name} (id:{p.Id}): R$ {p.Price:F2} | {(p.InStock ? "Disponível" : "Indisponível")} | {p.Description}"))
            : "Nenhum produto cadastrado.";

        return $"""
            Você é um assistente de vendas via WhatsApp da loja "{context.TenantName}".
            Seu objetivo é ajudar o cliente a comprar de forma simples, amigável e eficiente.

            REGRAS IMPORTANTES:
            - Responda SEMPRE em português brasileiro
            - Seja amigável, use emojis com moderação
            - Responda de forma CURTA e OBJETIVA (máximo 3 linhas por mensagem)
            - Nunca invente produtos que não estejam na lista abaixo
            - Quando o cliente quiser comprar, colete: quantidade → endereço → confirme pedido
            - Se não souber responder, diga que vai verificar e ofereça chamar um atendente
            - NUNCA discuta política, religião ou assuntos fora do contexto de vendas
            - Se o cliente pedir para falar com humano, use intent "transferir_humano"

            CLIENTE: {context.ContactName}
            ETAPA ATUAL: {context.CurrentStage}

            PRODUTOS DISPONÍVEIS:
            {productsText}

            {(context.CartSummary != null && context.CartSummary != "{}" ? $"CARRINHO ATUAL:\n{context.CartSummary}" : "")}

            PAGAMENTO:
            {context.PaymentInfo}

            {(context.CustomInstructions != null ? $"INSTRUÇÕES ESPECIAIS DO LOJISTA:\n{context.CustomInstructions}" : "")}

            FORMATO DE RESPOSTA (JSON obrigatório):
            {{
              "message": "<resposta em português para o cliente>",
              "intent": "<um de: consulta_produto | interesse_compra | finalizar_pedido | suporte | transferir_humano>",
              "products_mentioned": ["<nome do produto>"],
              "next_action": "<continue | collect_address | confirm_order | transfer_human | close>",
              "confidence": <número inteiro 0-100 indicando sua confiança na resposta>
            }}

            Se sua confiança for menor que 70, prefira intent "transferir_humano".
            Responda APENAS com o JSON, sem markdown ou texto extra.
            """;
    }

    private static AiConversationResponse ParseStructuredResponse(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var message = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "" : "";
            var intentRaw = root.TryGetProperty("intent", out var intentEl) ? intentEl.GetString() ?? "suporte" : "suporte";
            var intent = ValidIntents.Contains(intentRaw) ? intentRaw : "suporte";
            var nextAction = root.TryGetProperty("next_action", out var naEl) ? naEl.GetString() ?? "continue" : "continue";
            var confidence = root.TryGetProperty("confidence", out var confEl) ? confEl.GetInt32() : 50;

            var products = new List<string>();
            if (root.TryGetProperty("products_mentioned", out var prodEl) && prodEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prodEl.EnumerateArray())
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) products.Add(name);
                }
            }

            return new AiConversationResponse(message, intent, products, nextAction, confidence);
        }
        catch
        {
            // If the model didn't return valid JSON, wrap the raw text
            return new AiConversationResponse(
                Message: raw,
                Intent: "suporte",
                ProductsMentioned: [],
                NextAction: "continue",
                Confidence: 50);
        }
    }
}
