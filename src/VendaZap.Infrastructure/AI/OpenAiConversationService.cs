using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using VendaZap.Application.Common.Interfaces;

namespace VendaZap.Infrastructure.AI;

public class OpenAiConversationService : IAiConversationService
{
    private readonly OpenAIClient _client;
    private readonly ILogger<OpenAiConversationService> _logger;
    private readonly string _model;

    public OpenAiConversationService(IConfiguration config, ILogger<OpenAiConversationService> logger)
    {
        _logger = logger;
        var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured.");
        _model = config["OpenAI:Model"] ?? "gpt-4o-mini";
        _client = new OpenAIClient(apiKey);
    }

    public async Task<string> GetSalesResponseAsync(Guid tenantId, string userMessage, SalesContext context, CancellationToken ct = default)
    {
        var systemPrompt = BuildSalesSystemPrompt(context);
        var chatClient = _client.GetChatClient(_model);

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            return response.Value.Content[0].Text ?? "Olá! Como posso te ajudar?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API error for tenant {TenantId}", tenantId);
            return "Desculpe, estou com dificuldades no momento. Pode tentar novamente?";
        }
    }

    public async Task<string> GetResponseAsync(
        Guid tenantId, string conversationThreadId, string userMessage,
        string systemContext, CancellationToken ct = default)
    {
        // For stateless completion (simpler flow)
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
    {
        // Using a simple UUID as thread ID for conversation tracking
        // In a full implementation, use OpenAI Assistants API threads
        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public Task<bool> DeleteThreadAsync(string threadId, CancellationToken ct = default)
        => Task.FromResult(true);

    private static string BuildSalesSystemPrompt(SalesContext context)
    {
        var productsText = context.AvailableProducts.Any()
            ? string.Join("\n", context.AvailableProducts.Select(p =>
                $"- {p.Name}: R$ {p.Price:F2} | {(p.InStock ? "Disponível" : "Indisponível")} | {p.Description}"))
            : "Nenhum produto cadastrado.";

        return $"""
            Você é um assistente de vendas via WhatsApp da loja "{context.TenantName}".
            Seu objetivo é ajudar o cliente a comprar de forma simples, amigável e eficiente.
            
            REGRAS IMPORTANTES:
            - Responda SEMPRE em português brasileiro
            - Seja amigável, use emojis com moderação
            - Responda de forma CURTA e OBJETIVA (máximo 3 linhas por mensagem)
            - Nunca invente produtos que não estejam na lista
            - Quando o cliente quiser comprar, colete: quantidade → endereço → confirme pedido
            - Se não souber responder, diga que vai verificar e ofereça chamar um atendente
            - NUNCA discuta política, religião ou assuntos fora do contexto de vendas
            - Se o cliente pedir para falar com humano, diga "Vou chamar um atendente para você."
            
            CLIENTE: {context.ContactName}
            ETAPA ATUAL: {context.CurrentStage}
            
            PRODUTOS DISPONÍVEIS:
            {productsText}
            
            {(context.CartSummary != "{}" ? $"CARRINHO ATUAL:\n{context.CartSummary}" : "")}
            
            PAGAMENTO:
            {context.PaymentInfo}
            
            {(context.CustomInstructions != null ? $"INSTRUÇÕES ESPECIAIS DO LOJISTA:\n{context.CustomInstructions}" : "")}
            """;
    }
}
