using ClinicMvc.Models;

namespace ClinicMvc.Repositories;

public interface IAuditLogRepository
{
    Task LogAsync(string actionType, string entityName, int entityId, string username, string? description = null);

    /// <summary>Странирано пребарување со филтри (тип на акција, ентитет, корисник, датумски опсег). Без филтри = сите.</summary>
    Task<IEnumerable<AuditLog>> SearchPagedAsync(AuditLogFilter filter, int page, int pageSize);

    /// <summary>Вкупен број записи кои одговараат на филтрите.</summary>
    Task<int> SearchCountAsync(AuditLogFilter filter);
}
