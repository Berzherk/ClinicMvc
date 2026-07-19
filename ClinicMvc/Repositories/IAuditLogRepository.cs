using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

public interface IAuditLogRepository
{
    Task LogAsync(string actionType, string entityName, int entityId, string username, string? description = null);
    Task<IEnumerable<AuditLog>> GetAllAsync();

    /// <summary>Странирана листа на логови, најнови прво. page почнува од 1.</summary>
    Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize);

    /// <summary>Вкупен број записи - потребен за пресметка на бројот на страници.</summary>
    Task<int> CountAsync();

    /// <summary>Странирано пребарување со филтри (тип на акција, ентитет, корисник, датумски опсег).</summary>
    Task<IEnumerable<AuditLog>> SearchPagedAsync(AuditLogFilter filter, int page, int pageSize);

    /// <summary>Вкупен број записи кои одговараат на филтрите.</summary>
    Task<int> SearchCountAsync(AuditLogFilter filter);
}
