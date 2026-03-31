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
    public DbSet<WhatsAppAccount> WhatsAppAccounts => Set<WhatsAppAccount>();

    // Helpers avaliados em tempo de execução pelas expressões dos filtros.
    // Separar flag + valor evita NullReferenceException no .Value quando
    // não há tenant autenticado (ex: login, registro, migrations).
    private bool HasTenantFilter => _tenantContext?.TenantId.HasValue ?? false;
    private Guid TenantFilterId => _tenantContext?.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ─── Global Query Filters (isolamento multi-tenant) ───────────────────
        // Quando HasTenantFilter = false (sem tenant autenticado), o filtro
        // não é aplicado e todas as queries retornam normalmente.
        // Quando HasTenantFilter = true, todas as queries filtram pelo TenantId.
        modelBuilder.Entity<User>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Product>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Contact>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Message>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Order>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<Campaign>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<AutoReplyTemplate>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

        modelBuilder.Entity<WhatsAppAccount>()
            .HasQueryFilter(e => !HasTenantFilter || e.TenantId == TenantFilterId);

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
