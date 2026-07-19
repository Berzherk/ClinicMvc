using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за AuditLogs табелата.
/// LogAsync се повикува рачно по секоја успешна Create/Update/Delete операција
/// во контролерите (наместо автоматски "hook", за целосна контрола и јасност).
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditLogRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task LogAsync(string actionType, string entityName, int entityId, string username, string? description = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO AUDITLOGS
                                (ACTIONTYPE, ENTITYNAME, ENTITYID, USERNAME, LOGDATETIME, DESCRIPTION)
                              VALUES
                                (@ActionType, @EntityName, @EntityId, @Username, CURRENT_TIMESTAMP, @Description)";
        await connection.ExecuteAsync(sql, new { ActionType = actionType, EntityName = entityName, EntityId = entityId, Username = username, Description = description });
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, ACTIONTYPE, ENTITYNAME, ENTITYID, USERNAME, LOGDATETIME, DESCRIPTION
                              FROM AUDITLOGS
                              ORDER BY LOGDATETIME DESC";
        return await connection.QueryAsync<AuditLog>(sql);
    }

    public async Task<IEnumerable<AuditLog>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var validPage = page < 1 ? 1 : page;
        var skip = (validPage - 1) * pageSize;

        // Firebird синтакса: FIRST/SKIP оди веднаш по SELECT
        const string sql = @"SELECT FIRST @PageSize SKIP @Skip
                                ID, ACTIONTYPE, ENTITYNAME, ENTITYID, USERNAME, LOGDATETIME, DESCRIPTION
                              FROM AUDITLOGS
                              ORDER BY LOGDATETIME DESC";
        return await connection.QueryAsync<AuditLog>(sql, new { PageSize = pageSize, Skip = skip });
    }

    public async Task<int> CountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(*) FROM AUDITLOGS";
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    /// <summary>
    /// Ги гради WHERE условите за филтрите - заеднички за SearchPagedAsync и SearchCountAsync.
    /// </summary>
    private static (string WhereClause, DynamicParameters Parameters) BuildWhereClause(AuditLogFilter filter)
    {
        var sb = new System.Text.StringBuilder("WHERE 1=1");
        var p  = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
        {
            sb.Append(" AND ACTIONTYPE = @ActionType");
            p.Add("ActionType", filter.ActionType);
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            sb.Append(" AND ENTITYNAME = @EntityName");
            p.Add("EntityName", filter.EntityName);
        }
        if (!string.IsNullOrWhiteSpace(filter.Username))
        {
            sb.Append(" AND UPPER(USERNAME) LIKE UPPER(@Username)");
            p.Add("Username", $"%{filter.Username.Trim()}%");
        }
        if (filter.DateFrom.HasValue)
        {
            sb.Append(" AND LOGDATETIME >= @DateFrom");
            p.Add("DateFrom", filter.DateFrom.Value.Date);
        }
        if (filter.DateTo.HasValue)
        {
            // До крајот на избраниот ден (23:59:59), инаку записите ОД тој ден би отпаднале
            sb.Append(" AND LOGDATETIME < @DateTo");
            p.Add("DateTo", filter.DateTo.Value.Date.AddDays(1));
        }

        return (sb.ToString(), p);
    }

    /// <summary>Странирано пребарување на логови според филтрите.</summary>
    public async Task<IEnumerable<AuditLog>> SearchPagedAsync(AuditLogFilter filter, int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var validPage = page < 1 ? 1 : page;
        p.Add("PageSize", pageSize);
        p.Add("Skip", (validPage - 1) * pageSize);

        var sql = $@"SELECT FIRST @PageSize SKIP @Skip
                        ID, ACTIONTYPE, ENTITYNAME, ENTITYID, USERNAME, LOGDATETIME, DESCRIPTION
                     FROM AUDITLOGS
                     {where}
                     ORDER BY LOGDATETIME DESC";
        return await connection.QueryAsync<AuditLog>(sql, p);
    }

    /// <summary>Вкупен број логови кои одговараат на филтрите.</summary>
    public async Task<int> SearchCountAsync(AuditLogFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var sql = $"SELECT COUNT(*) FROM AUDITLOGS {where}";
        return await connection.ExecuteScalarAsync<int>(sql, p);
    }
}
