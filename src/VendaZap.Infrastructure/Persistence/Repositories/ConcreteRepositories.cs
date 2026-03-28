using Microsoft.EntityFrameworkCore;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Enums;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.Persistence.Repositories;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext context) : base(context) { }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => await _dbSet.AnyAsync(t => t.Slug == slug, ct);

    public async Task<Tenant?> GetWithUsersAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.Include(t => t.Users).FirstOrDefaultAsync(t => t.Id == id, ct);
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email && u.TenantId == tenantId, ct);

    public async Task<IEnumerable<User>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbSet.Where(u => u.TenantId == tenantId).ToListAsync(ct);

    public async Task<bool> EmailExistsAsync(string email, Guid tenantId, CancellationToken ct = default)
        => await _dbSet.AnyAsync(u => u.Email == email && u.TenantId == tenantId, ct);
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> GetByTenantAsync(Guid tenantId, bool activeOnly = true, CancellationToken ct = default)
    {
        var query = _dbSet.Where(p => p.TenantId == tenantId);
        if (activeOnly) query = query.Where(p => p.Status == ProductStatus.Active);
        return await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default)
        => await _dbSet.Where(p => p.TenantId == tenantId && p.Category == category && p.Status == ProductStatus.Active)
                       .ToListAsync(ct);

    public async Task<IEnumerable<Product>> SearchAsync(Guid tenantId, string query, CancellationToken ct = default)
    {
        var lower = query.ToLower();
        return await _dbSet
            .Where(p => p.TenantId == tenantId && p.Status == ProductStatus.Active &&
                (EF.Functions.ILike(p.Name, $"%{lower}%") || EF.Functions.ILike(p.Description, $"%{lower}%")))
            .ToListAsync(ct);
    }
}

public class ContactRepository : Repository<Contact>, IContactRepository
{
    public ContactRepository(AppDbContext context) : base(context) { }

    public async Task<Contact?> GetByPhoneAsync(string phone, Guid tenantId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(c => c.PhoneNumber == phone && c.TenantId == tenantId, ct);

    public async Task<IEnumerable<Contact>> GetByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct = default)
        => await _dbSet.Where(c => c.TenantId == tenantId)
                       .OrderByDescending(c => c.LastInteractionAt)
                       .Skip((page - 1) * pageSize).Take(pageSize)
                       .ToListAsync(ct);

    public async Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbSet.CountAsync(c => c.TenantId == tenantId, ct);

    public async Task<Contact?> GetOrCreateAsync(Guid tenantId, string phone, string? name, CancellationToken ct = default)
    {
        var contact = await GetByPhoneAsync(phone, tenantId, ct);
        if (contact is not null)
        {
            if (name is not null && contact.Name is null)
                contact.UpdateProfile(name, null, null, null, null, null);
            return contact;
        }
        contact = Contact.Create(tenantId, phone, name);
        await _dbSet.AddAsync(contact, ct);
        return contact;
    }
}

public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(AppDbContext context) : base(context) { }

    public async Task<Conversation?> GetActiveByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct = default)
        => await _dbSet
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId && c.ContactId == contactId && c.Status == ConversationStatus.Open, ct);

    public async Task<IEnumerable<Conversation>> GetByTenantAsync(Guid tenantId, ConversationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbSet
            .Include(c => c.Contact)
            .Include(c => c.AssignedToUser)
            .Where(c => c.TenantId == tenantId);

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);

        return await query
            .OrderByDescending(c => c.LastMessageAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(c => c.Contact)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt).Take(100))
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<int> CountOpenAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbSet.CountAsync(c => c.TenantId == tenantId && c.Status == ConversationStatus.Open, ct);

    public async Task<IEnumerable<Conversation>> GetAbandonedCartsAsync(Guid tenantId, int minutesOld, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutesOld);
        return await _dbSet
            .Include(c => c.Contact)
            .Where(c => c.TenantId == tenantId &&
                c.Status == ConversationStatus.Open &&
                c.Stage == ConversationStage.AbandonedCart &&
                c.LastMessageAt < cutoff &&
                c.CartJson != "{}")
            .ToListAsync(ct);
    }
}

public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Message>> GetByConversationAsync(Guid conversationId, int page, int pageSize, CancellationToken ct = default)
        => await _dbSet
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

    public async Task<Message?> GetByWhatsAppIdAsync(string whatsAppMessageId, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(m => m.WhatsAppMessageId == whatsAppMessageId, ct);
}

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Order>> GetByTenantAsync(Guid tenantId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbSet
            .Include(o => o.Contact)
            .Include(o => o.Items)
            .Where(o => o.TenantId == tenantId);

        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        return await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Order>> GetByContactAsync(Guid contactId, CancellationToken ct = default)
        => await _dbSet.Include(o => o.Items)
                       .Where(o => o.ContactId == contactId)
                       .OrderByDescending(o => o.CreatedAt)
                       .ToListAsync(ct);

    public async Task<string> GenerateOrderNumberAsync(Guid tenantId, CancellationToken ct = default)
    {
        var count = await _dbSet.CountAsync(o => o.TenantId == tenantId, ct);
        return $"VZ-{DateTime.UtcNow:yyMM}-{(count + 1):D4}";
    }

    public async Task<Order?> GetWithItemsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<decimal> GetRevenueAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
        => await _dbSet
            .Where(o => o.TenantId == tenantId &&
                (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Delivered) &&
                o.CreatedAt >= from && o.CreatedAt <= to)
            .SumAsync(o => (decimal?)o.Total.Amount ?? 0, ct);

    public async Task<int> CountByStatusAsync(Guid tenantId, OrderStatus status, CancellationToken ct = default)
        => await _dbSet.CountAsync(o => o.TenantId == tenantId && o.Status == status, ct);
}

public class CampaignRepository : Repository<Campaign>, ICampaignRepository
{
    public CampaignRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Campaign>> GetActiveByTriggerAsync(Guid tenantId, CampaignTrigger trigger, CancellationToken ct = default)
        => await _dbSet
            .Where(c => c.TenantId == tenantId && c.Status == CampaignStatus.Active && c.Trigger == trigger)
            .ToListAsync(ct);

    public async Task<IEnumerable<Campaign>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbSet.Where(c => c.TenantId == tenantId).OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
}

public class AutoReplyTemplateRepository : Repository<AutoReplyTemplate>, IAutoReplyTemplateRepository
{
    public AutoReplyTemplateRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<AutoReplyTemplate>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _dbSet
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderByDescending(t => t.Priority)
            .ToListAsync(ct);
}
