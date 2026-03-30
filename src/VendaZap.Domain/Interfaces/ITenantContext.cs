namespace VendaZap.Domain.Interfaces;

/// <summary>
/// Fornece o contexto do tenant atual para isolamento de dados multi-tenant.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Id do tenant atual. Null quando não há tenant autenticado (ex: registro).
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Indica se há um tenant resolvido no contexto atual.
    /// </summary>
    bool HasTenant => TenantId.HasValue;
}
