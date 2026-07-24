using System.Text;
using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за термини / термински слотови. Поддржува Soft Delete, audit колони и
/// рестрикција по доктор (за докторска улога која смее да гледа само свои термини).
///
/// НАПОМЕНА: колоната во базата се вика PARIENTID (историски typo), но полето во
/// C# моделот е PatientId - алијасирано преку "AS" во сите SELECT изрази подолу.
/// Колоната сега дозволува NULL бидејќи слободните слотови немаат доделен пациент.
/// </summary>
public class AppointmentRepository : IAppointmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    // LEFT JOIN на PATIENTS бидејќи слободните слотови (PARIENTID = NULL) немаат пациент.
    private const string FromJoinSql = @"
        FROM APPOINTMENTS a
        JOIN      DOCTORS  d ON d.ID = a.DOCTORID
        LEFT JOIN PATIENTS p ON p.ID = a.PARIENTID";

    private const string SelectColumns = @"
        a.ID, a.DOCTORID, a.PARIENTID AS PATIENTID,
        a.APPOINTMENTDATE, a.APPOINTMENTTIME, a.STATUS, a.NOTES,
        (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
        (p.FIRSTNAME || ' ' || p.LASTNAME) AS PATIENTNAME,
        p.EMAIL AS PATIENTEMAIL,
        d.SPECIALTY AS DOCTORSPECIALTY";

    public AppointmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Ги гради WHERE условите за филтрите. Секогаш ISDELETED = FALSE (Soft Delete правило).
    /// Ако RestrictToDoctorId е поставен (доктор-корисник) - дополнително ги ограничува резултатите.
    /// </summary>
    private static (string WhereClause, DynamicParameters Parameters) BuildWhereClause(AppointmentFilter filter)
    {
        var sb = new StringBuilder("WHERE a.ISDELETED = FALSE");
        var p  = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.PatientFirstName))
        {
            sb.Append(" AND UPPER(p.FIRSTNAME) LIKE UPPER(@PatientFirstName)");
            p.Add("PatientFirstName", $"%{filter.PatientFirstName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.PatientLastName))
        {
            sb.Append(" AND UPPER(p.LASTNAME) LIKE UPPER(@PatientLastName)");
            p.Add("PatientLastName", $"%{filter.PatientLastName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.PatientEmbg))
        {
            sb.Append(" AND p.EMBG LIKE @PatientEmbg");
            p.Add("PatientEmbg", $"%{filter.PatientEmbg.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.DoctorName))
        {
            sb.Append(" AND UPPER(d.FIRSTNAME || ' ' || d.LASTNAME) LIKE UPPER(@DoctorName)");
            p.Add("DoctorName", $"%{filter.DoctorName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.Specialty))
        {
            sb.Append(" AND d.SPECIALTY = @Specialty");
            p.Add("Specialty", filter.Specialty);
        }
        if (filter.Date.HasValue)
        {
            sb.Append(" AND a.APPOINTMENTDATE = @Date");
            p.Add("Date", filter.Date.Value.Date);
        }

        // Безбедносна рестрикција - доктор-корисник смее да гледа само свои термини.
        // Ова НЕ доаѓа од корисничкиот интерфејс, туку го поставува контролерот
        // според најавениот корисник, значи не може да се заобиколи преку query string.
        if (filter.RestrictToDoctorId.HasValue)
        {
            sb.Append(" AND a.DOCTORID = @RestrictToDoctorId");
            p.Add("RestrictToDoctorId", filter.RestrictToDoctorId.Value);
        }

        return (sb.ToString(), p);
    }

    private static string GetOrderByColumn(string? sortBy) => sortBy?.ToLowerInvariant() switch
    {
        "time"    => "a.APPOINTMENTTIME",
        "patient" => "p.LASTNAME, p.FIRSTNAME",
        "doctor"  => "d.LASTNAME, d.FIRSTNAME",
        "status"  => "a.STATUS",
        _         => "a.APPOINTMENTDATE"
    };

    public async Task<IEnumerable<Appointment>> SearchAsync(AppointmentFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var orderColumn = GetOrderByColumn(filter.SortBy);
        var direction = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC" : "ASC";

        var page = filter.Page < 1 ? 1 : filter.Page;
        var skip = (page - 1) * AppointmentFilter.PageSize;
        p.Add("PageSize", AppointmentFilter.PageSize);
        p.Add("Skip", skip);

        var sql = $@"
            SELECT FIRST @PageSize SKIP @Skip
                {SelectColumns}
            {FromJoinSql}
            {where}
            ORDER BY {orderColumn} {direction}";

        return await connection.QueryAsync<Appointment>(sql, p);
    }

    public async Task<int> CountAsync(AppointmentFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);
        var sql = $@"SELECT COUNT(*) {FromJoinSql} {where}";
        return await connection.ExecuteScalarAsync<int>(sql, p);
    }

    public async Task<AppointmentStatistics> GetStatisticsAsync(AppointmentFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var sql = $@"
            SELECT
                COUNT(*) AS TOTAL,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Free'      THEN 1 ELSE 0 END), 0) AS FREE,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Booked'    THEN 1 ELSE 0 END), 0) AS BOOKED,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Completed' THEN 1 ELSE 0 END), 0) AS COMPLETED,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Cancelled' THEN 1 ELSE 0 END), 0) AS CANCELLED
            {FromJoinSql}
            {where}";

        var stats = await connection.QueryFirstOrDefaultAsync<AppointmentStatistics>(sql, p);
        return stats ?? new AppointmentStatistics();
    }

    public async Task<Appointment?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $@"SELECT {SelectColumns} {FromJoinSql} WHERE a.ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Appointment>(sql, new { Id = id });
    }

    /// <summary>Креира нов слободен термински слот - PARIENTID = NULL, STATUS = 'Free'.</summary>
    public async Task<int> CreateSlotAsync(int doctorId, DateTime date, TimeSpan time, string createdBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO APPOINTMENTS
                                (DOCTORID, PARIENTID, APPOINTMENTDATE, APPOINTMENTTIME, STATUS, NOTES,
                                 ISDELETED, CREATEDON, CREATEDBY)
                              VALUES
                                (@DoctorId, NULL, @AppointmentDate, @AppointmentTime, @Status, NULL,
                                 FALSE, CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            DoctorId = doctorId,
            AppointmentDate = date.Date,
            AppointmentTime = time,
            Status = AppointmentStatus.Free,
            CreatedBy = createdBy
        });
    }

    public async Task UpdateSlotAsync(int id, int doctorId, DateTime date, TimeSpan time, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS SET
                                DOCTORID        = @DoctorId,
                                APPOINTMENTDATE = @AppointmentDate,
                                APPOINTMENTTIME = @AppointmentTime,
                                MODIFIEDON      = CURRENT_TIMESTAMP,
                                MODIFIEDBY      = @ModifiedBy
                              WHERE ID = @Id AND STATUS = @FreeStatus";
        await connection.ExecuteAsync(sql, new
        {
            Id = id, DoctorId = doctorId, AppointmentDate = date.Date, AppointmentTime = time,
            ModifiedBy = modifiedBy, FreeStatus = AppointmentStatus.Free
        });
    }

    /// <summary>SOFT DELETE - записот останува во базата (медицинска историја мора да се зачува).</summary>
    public async Task DeleteAsync(int id, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS
                              SET ISDELETED  = TRUE,
                                  MODIFIEDON = CURRENT_TIMESTAMP,
                                  MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, ModifiedBy = modifiedBy });
    }

    /// <summary>
    /// Атомска резервација - условот "STATUS = Free" е дел од самиот UPDATE, не претходна проверка,
    /// за да се избегне "race condition" кога двајца пациенти кликнуваат "Резервирај" во ист момент.
    /// Ако 0 редови се засегнати, слотот веќе бил зафатен - враќа false.
    /// </summary>
    public async Task<bool> BookSlotAsync(int id, int patientId, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS SET
                                PARIENTID  = @PatientId,
                                STATUS     = @BookedStatus,
                                MODIFIEDON = CURRENT_TIMESTAMP,
                                MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id AND STATUS = @FreeStatus AND ISDELETED = FALSE";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            Id = id,
            PatientId = patientId,
            ModifiedBy = modifiedBy,
            BookedStatus = AppointmentStatus.Booked,
            FreeStatus = AppointmentStatus.Free
        });

        return rowsAffected > 0;
    }

    public async Task UpdateStatusAsync(int id, string newStatus, string modifiedBy, string? notes = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS
                              SET STATUS     = @Status,
                                  NOTES      = COALESCE(@Notes, NOTES),
                                  MODIFIEDON = CURRENT_TIMESTAMP,
                                  MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, Status = newStatus, Notes = notes, ModifiedBy = modifiedBy });
    }

    public async Task<IEnumerable<FreeSlot>> SearchFreeSlotsAsync(FreeSlotsFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sb = new StringBuilder(@"
            SELECT a.ID, a.DOCTORID,
                   (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
                   d.SPECIALTY AS DOCTORSPECIALTY,
                   a.APPOINTMENTDATE AS ""DATE"",
                   a.APPOINTMENTTIME AS ""TIME""
            FROM APPOINTMENTS a
            JOIN DOCTORS d ON d.ID = a.DOCTORID
            WHERE a.ISDELETED = FALSE AND a.STATUS = @FreeStatus AND d.ISACTIVE = TRUE");
        var p = new DynamicParameters();
        p.Add("FreeStatus", AppointmentStatus.Free);

        if (filter.DoctorId.HasValue)
        {
            sb.Append(" AND a.DOCTORID = @DoctorId");
            p.Add("DoctorId", filter.DoctorId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Specialty))
        {
            sb.Append(" AND d.SPECIALTY = @Specialty");
            p.Add("Specialty", filter.Specialty);
        }
        if (filter.Date.HasValue)
        {
            sb.Append(" AND a.APPOINTMENTDATE = @Date");
            p.Add("Date", filter.Date.Value.Date);
        }
        else
        {
            // Без конкретен датум - сепак не прикажувај минати слотови
            sb.Append(" AND a.APPOINTMENTDATE >= @Today");
            p.Add("Today", DateTime.Today);
        }

        sb.Append(" ORDER BY a.APPOINTMENTDATE, a.APPOINTMENTTIME");

        return await connection.QueryAsync<FreeSlot>(sb.ToString(), p);
    }

    public async Task<IEnumerable<Appointment>> GetPatientAppointmentsAsync(int patientId, bool upcomingOnly)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = $@"
            SELECT {SelectColumns}
            {FromJoinSql}
            WHERE a.ISDELETED = FALSE AND a.PARIENTID = @PatientId
              AND {(upcomingOnly ? "a.STATUS = 'Booked'" : "a.STATUS IN ('Completed', 'Cancelled')")}
            ORDER BY a.APPOINTMENTDATE {(upcomingOnly ? "ASC" : "DESC")}, a.APPOINTMENTTIME ASC";

        return await connection.QueryAsync<Appointment>(sql, new { PatientId = patientId });
    }

    public async Task<bool> ExistsAtSlotAsync(int doctorId, DateTime date, TimeSpan time, int excludeId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT COUNT(*) FROM APPOINTMENTS
                              WHERE ISDELETED = FALSE AND DOCTORID = @DoctorId
                                AND APPOINTMENTDATE = @Date AND APPOINTMENTTIME = @Time
                                AND ID <> @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql, new
        {
            DoctorId = doctorId,
            Date = date.Date,
            Time = time,
            ExcludeId = excludeId
        });
        return count > 0;
    }
}
