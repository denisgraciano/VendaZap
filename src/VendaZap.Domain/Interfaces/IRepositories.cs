using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;

namespace VendaZap.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetWithUsersAsync(Guid id, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid tenantId, CancellationToken ct = default);
}

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByTenantAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default);
    Task<IEnumerable<Product>> SearchAsync(Guid tenantId, string query, CancellationToken ct = default);
}

public interface IContactRepository : IRepository<Contact>
{
    Task<Contact?> GetByPhoneAsync(string phone, Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Contact>> GetByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Contact?> GetOrCreateAsync(Guid tenantId, string phone, string? name, CancellationToken ct = default);
}

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetActiveByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct = default);
    Task<(Conversation conversation, bool isNew)> GetOrCreateByPhoneAsync(Guid tenantId, string customerPhone, string? customerName, CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetByTenantAsync(Guid tenantId, ConversationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken ct = default);
    Task<int> CountOpenAsync(Guid tenantId, CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetAbandonedCartsAsync(Guid tenantId, int minutesOld, CancellationToken ct = default);
}

public interface IMessageRepository : IRepository<Message>
{
    Task<IEnumerable<Message>> GetByConversationAsync(Guid conversationId, int page, int pageSize, CancellationToken ct = default);
    Task<IEnumerable<Message>> GetRecentMessagesAsync(Guid conversationId, CancellationToken ct = default);
    Task<Message?> GetByWhatsAppIdAsync(string whatsAppMessageId, CancellationToken ct = default);
    Task InvalidateConversationCacheAsync(Guid conversationId, CancellationToken ct = default);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByTenantAsync(Guid tenantId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<IEnumerable<Order>> GetByContactAsync(Guid contactId, CancellationToken ct = default);
    Task<string> GenerateOrderNumberAsync(Guid tenantId, CancellationToken ct = default);
    Task<Order?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<decimal> GetRevenueAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> CountByStatusAsync(Guid tenantId, OrderStatus status, CancellationToken ct = default);
}

public interface ICampaignRepository : IRepository<Campaign>
{
    Task<IEnumerable<Campaign>> GetActiveByTriggerAsync(Guid tenantId, CampaignTrigger trigger, CancellationToken ct = default);
    Task<IEnumerable<Campaign>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IAutoReplyTemplateRepository : IRepository<AutoReplyTemplate>
{
    Task<IEnumerable<AutoReplyTemplate>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
