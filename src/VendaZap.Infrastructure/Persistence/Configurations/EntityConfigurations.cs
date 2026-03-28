using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VendaZap.Domain.Entities;
using VendaZap.Domain.ValueObjects;

namespace VendaZap.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.Property(t => t.WhatsAppPhoneNumberId).HasMaxLength(50).IsRequired();
        builder.Property(t => t.WhatsAppAccessToken).HasMaxLength(500).IsRequired();
        builder.Property(t => t.OpenAiAssistantId).HasMaxLength(100);
        builder.Property(t => t.WelcomeMessage).HasMaxLength(1000);
        builder.Property(t => t.AwayMessage).HasMaxLength(1000);
        builder.HasMany(t => t.Users).WithOne(u => u.Tenant).HasForeignKey(u => u.TenantId);
        builder.HasMany(t => t.Products).WithOne(p => p.Tenant).HasForeignKey(p => p.TenantId);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => new { u.Email, u.TenantId }).IsUnique();
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.RefreshToken).HasMaxLength(200);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.ExternalLink).HasMaxLength(500);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Sku).HasMaxLength(100);

        // Value Object: Money
        builder.OwnsOne(p => p.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(p => p.TenantId);
        builder.HasIndex(p => new { p.TenantId, p.Status });
    }
}

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.PhoneNumber, c.TenantId }).IsUnique();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(50);
        builder.Property(c => c.ZipCode).HasMaxLength(10);
        builder.Property(c => c.TagsJson).HasMaxLength(2000).HasDefaultValue("[]");
        builder.Property(c => c.TotalSpent).HasPrecision(18, 2);
        builder.HasIndex(c => c.TenantId);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.ContactId, c.Status });
        builder.Property(c => c.LastMessagePreview).HasMaxLength(200);
        builder.Property(c => c.WhatsAppConversationId).HasMaxLength(100);
        builder.Property(c => c.AiThreadId).HasMaxLength(100);
        builder.Property(c => c.CartJson).HasMaxLength(5000).HasDefaultValue("{}");

        builder.HasMany(c => c.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId);
        builder.HasOne(c => c.Contact).WithMany(co => co.Conversations).HasForeignKey(c => c.ContactId);
        builder.HasOne(c => c.AssignedToUser).WithMany().HasForeignKey(c => c.AssignedToUserId).IsRequired(false);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.WhatsAppMessageId);
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
        builder.Property(m => m.Content).HasMaxLength(4096).IsRequired();
        builder.Property(m => m.WhatsAppMessageId).HasMaxLength(100);
        builder.Property(m => m.MediaUrl).HasMaxLength(500);
        builder.Property(m => m.MediaMimeType).HasMaxLength(100);
        builder.Property(m => m.ErrorMessage).HasMaxLength(500);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => new { o.TenantId, o.OrderNumber }).IsUnique();
        builder.HasIndex(o => new { o.TenantId, o.Status });
        builder.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentLink).HasMaxLength(500);
        builder.Property(o => o.PixKey).HasMaxLength(150);
        builder.Property(o => o.TrackingCode).HasMaxLength(100);
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.OwnsOne(o => o.Subtotal, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Subtotal").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("SubtotalCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.ShippingCost, m =>
        {
            m.Property(x => x.Amount).HasColumnName("ShippingCost").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("ShippingCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(o => o.Total, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Total").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
        });

        builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId);
        builder.HasOne(o => o.Contact).WithMany(c => c.Orders).HasForeignKey(o => o.ContactId);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
        });
        builder.OwnsOne(i => i.Total, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Total").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("TotalCurrency").HasMaxLength(3);
        });
    }
}

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.MessageTemplate).HasMaxLength(4096).IsRequired();
        builder.Property(c => c.FiltersJson).HasMaxLength(2000).HasDefaultValue("{}");
        builder.Property(c => c.MediaUrl).HasMaxLength(500);
        builder.HasIndex(c => new { c.TenantId, c.Status });
    }
}

public class AutoReplyTemplateConfiguration : IEntityTypeConfiguration<AutoReplyTemplate>
{
    public void Configure(EntityTypeBuilder<AutoReplyTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Response).HasMaxLength(4096).IsRequired();
        builder.Property(t => t.Triggers)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
        builder.HasIndex(t => new { t.TenantId, t.IsActive });
    }
}
