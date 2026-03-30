using Microsoft.EntityFrameworkCore;
using VendaZap.Application.Common.Interfaces;
using VendaZap.Domain.Common;
using VendaZap.Domain.Entities;
using VendaZap.Domain.Interfaces;

namespace VendaZap.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenantService? _currentTenant;
    private readonly ITenantContext? _tenantContext;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantService? currentTenant = null,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _currentTenant = currentTenant;
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<AutoReplyTemplate> AutoReplyTemplates => Set<AutoReplyTemplate>();

    // Propriedade avaliada em tempo de execução pela expressão do filtro
    private Guid? CurrentTenantId => _tenantContext?.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── Global Query Filters (isolamento multi-tenant) ───────────────────
        // Quando CurrentTenantId é null (ex: migrations, registro de tenant),
        // o filtro não é aplicado. Quando há um tenant autenticado, todas as
        // queries são automaticamente filtradas pelo TenantId do contexto.
        modelBuilder.Entity<User>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Contact>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Message>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Order>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<Campaign>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        modelBuilder.Entity<AutoReplyTemplate>()
            .HasQueryFilter(e => CurrentTenantId == null || e.TenantId == CurrentTenantId.Value);

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events before saving
        var domainEntities = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        // Note: Domain event dispatching via MediatR can be wired here if needed
        // For now, events are dispatched via MassTransit consumers

        return result;
    }
}
