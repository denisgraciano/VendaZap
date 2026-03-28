# VendaZap Backend

Sistema de automação de vendas via WhatsApp para pequenas empresas de e-commerce.

## 🧱 Arquitetura

```
VendaZap/
├── src/
│   ├── VendaZap.Domain/          # Entidades, Value Objects, Enums, Interfaces
│   ├── VendaZap.Application/     # Use Cases (CQRS via MediatR), Behaviors, DTOs
│   ├── VendaZap.Infrastructure/  # EF Core, PostgreSQL, Redis, RabbitMQ, OpenAI, WhatsApp API
│   └── VendaZap.API/             # Controllers REST, SignalR Hub, Webhook WhatsApp
└── docker-compose.yml
```

## 🚀 Quick Start (Desenvolvimento)

### Pré-requisitos
- .NET 8 SDK
- Docker Desktop

### 1. Subir infraestrutura local
```bash
docker-compose up -d postgres redis rabbitmq seq
```

### 2. Configurar variáveis
Edite `src/VendaZap.API/appsettings.Development.json`:
```json
{
  "OpenAI": { "ApiKey": "sk-..." },
  "WhatsApp": { "VerifyToken": "seu-token-secreto" },
  "Jwt": { "Secret": "sua-chave-jwt-muito-longa-e-segura" }
}
```

### 3. Rodar migrations
```bash
cd src/VendaZap.API
dotnet ef database update --project ../VendaZap.Infrastructure
```

### 4. Rodar a API
```bash
dotnet run --project src/VendaZap.API
```

Acesse: http://localhost:5000/docs (Swagger)

## 🐳 Docker Completo
```bash
OPENAI_API_KEY=sk-... docker-compose up --build
```

## 📡 Endpoints Principais

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | /api/v1/auth/register | Cadastrar nova empresa |
| POST | /api/v1/auth/login | Autenticar usuário |
| GET | /api/v1/products | Listar produtos |
| POST | /api/v1/products | Cadastrar produto |
| GET | /api/v1/conversations | Listar conversas |
| POST | /api/v1/conversations/{id}/messages | Enviar mensagem manual |
| POST | /api/v1/conversations/{id}/transfer-to-human | Transferir para humano |
| GET | /api/v1/orders | Listar pedidos |
| PATCH | /api/v1/orders/{id}/status | Atualizar status do pedido |
| GET | /api/v1/dashboard | Métricas do dashboard |
| GET/POST | /api/v1/webhooks/whatsapp/{slug} | Webhook WhatsApp |

## 🔗 Configurar Webhook WhatsApp

1. No Meta Business Suite → WhatsApp → Configuração → Webhooks
2. URL do Callback: `https://api.seudominio.com.br/api/v1/webhooks/whatsapp/{slug-do-tenant}`
3. Token de verificação: valor de `WhatsApp:VerifyToken` no appsettings
4. Assinar eventos: `messages`, `message_deliveries`, `message_reads`

## 🔔 SignalR (Real-time)

Conectar o frontend ao hub:
```javascript
const connection = new HubConnectionBuilder()
  .withUrl("https://api/hubs/notifications?access_token=SEU_JWT")
  .build();

connection.on("NewMessage", (data) => { /* atualizar conversa */ });
connection.on("HumanTakeover", (data) => { /* notificar agente */ });
connection.on("NewOrder", (data) => { /* notificar novo pedido */ });

await connection.start();
await connection.invoke("JoinTenantGroup", tenantId);
```

## 🛠 Tecnologias

- **Runtime**: .NET 8 / ASP.NET Core
- **ORM**: Entity Framework Core + Npgsql (PostgreSQL)
- **CQRS**: MediatR
- **Validação**: FluentValidation
- **Mensageria**: MassTransit + RabbitMQ
- **Cache**: Redis (StackExchange.Redis)
- **AI**: OpenAI GPT-4o-mini
- **WhatsApp**: Meta Business API (Graph API v18)
- **Real-time**: SignalR
- **Auth**: JWT Bearer
- **Logs**: Serilog + Seq
- **Resiliência**: Polly

## 📊 Ferramentas de Monitoramento

| Serviço | URL | Credenciais |
|---------|-----|-------------|
| Swagger | http://localhost:5000/docs | - |
| Seq Logs | http://localhost:8088 | - |
| RabbitMQ | http://localhost:15672 | vendazap / vendazap123 |
| pgAdmin | http://localhost:5050 | admin@vendazap.com.br / admin123 |

## 🗄️ Migrations

```bash
# Criar nova migration
dotnet ef migrations add NomeDaMigration \
  --project src/VendaZap.Infrastructure \
  --startup-project src/VendaZap.API

# Aplicar migrations
dotnet ef database update \
  --project src/VendaZap.Infrastructure \
  --startup-project src/VendaZap.API
```
