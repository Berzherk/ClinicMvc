using System.Text;
using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за термини. Поддржува Soft Delete, audit колони и рестрикција по доктор
/// (за докторска улога која смее да гледа само свои термини).
/// </summary>
public class AppointmentRepository : IAppointmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private const string FromJoinSql = @"
        FROM APPOINTMENTS a
        JOIN DOCTORS  d ON d.ID = a.DOCTORID
        JOIN PATIENTS p ON p.ID = a.PARIENTID";

    public AppointmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Appointment>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT
                a.ID, a.DOCTORID, a.PARIENTID AS PATIENTID,
                a.APPOINTMENTDATE, a.APPOINTMENTTIME, a.STATUS, a.NOTES,
                (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
                (p.FIRSTNAME || ' ' || p.LASTNAME) AS PATIENTNAME,
                d.SPECIALTY AS DOCTORSPECIALTY
            " + FromJoinSql + @"
            WHERE a.ISDELETED = FALSE
            ORDER BY a.APPOINTMENTDATE, a.APPOINTMENTTIME";
        return await connection.QueryAsync<Appointment>(sql);
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
                a.ID, a.DOCTORID, a.PARIENTID AS PATIENTID,
                a.APPOINTMENTDATE, a.APPOINTMENTTIME, a.STATUS, a.NOTES,
                (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
                (p.FIRSTNAME || ' ' || p.LASTNAME) AS PATIENTNAME,
                d.SPECIALTY AS DOCTORSPECIALTY
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
                COALESCE(SUM(CASE WHEN a.STATUS = 'Zakazan' THEN 1 ELSE 0 END), 0) AS SCHEDULED,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Zavrsen' THEN 1 ELSE 0 END), 0) AS COMPLETED,
                COALESCE(SUM(CASE WHEN a.STATUS = 'Otkazen' THEN 1 ELSE 0 END), 0) AS CANCELLED
            {FromJoinSql}
            {where}";

        var stats = await connection.QueryFirstOrDefaultAsync<AppointmentStatistics>(sql, p);
        return stats ?? new AppointmentStatistics();
    }

    public async Task<Appointment?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, DOCTORID, PARIENTID AS PATIENTID,
                                     APPOINTMENTDATE, APPOINTMENTTIME, STATUS, NOTES
                              FROM APPOINTMENTS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Appointment>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(Appointment appointment, string createdBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO APPOINTMENTS
                                (DOCTORID, PARIENTID, APPOINTMENTDATE, APPOINTMENTTIME, STATUS, NOTES,
                                 ISDELETED, CREATEDON, CREATEDBY)
                              VALUES
                                (@DoctorId, @PatientId, @AppointmentDate, @AppointmentTime, @Status, @Notes,
                                 FALSE, CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            appointment.DoctorId, appointment.PatientId, appointment.AppointmentDate,
            appointment.AppointmentTime, appointment.Status, appointment.Notes, CreatedBy = createdBy
        });
    }

    public async Task UpdateAsync(Appointment appointment, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS SET
                                DOCTORID        = @DoctorId,
                                PARIENTID       = @PatientId,
                                APPOINTMENTDATE = @AppointmentDate,
                                APPOINTMENTTIME = @AppointmentTime,
                                STATUS          = @Status,
                                NOTES           = @Notes,
                                MODIFIEDON      = CURRENT_TIMESTAMP,
                                MODIFIEDBY      = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new
        {
            appointment.Id, appointment.DoctorId, appointment.PatientId, appointment.AppointmentDate,
            appointment.AppointmentTime, appointment.Status, appointment.Notes, ModifiedBy = modifiedBy
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
    /// Проверува дали новиот/изменетиот термин паѓа премногу близу до постоечки термин
    /// за ИСТИОТ доктор на ИСТИОТ датум. Бара минимум 15 минути слободно време
    /// и пред и по секој термин (се дозволува слободен избор на време, не фиксни слотови).
    /// excludeId - ID на терминот кој се игнорира (при измена на постоечки термин).
    /// </summary>
    public async Task<bool> HasConflictAsync(int doctorId, DateTime date, TimeSpan time, int excludeId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Ги земаме СИТЕ времиња на терминиte на овој доктор за тој датум
        // (пресметката на минималната разлика се прави во C#, не во SQL,
        // бидејќи Firebird нема удобна ABS/TIME-diff функција преку Dapper параметри)
        const string sql = @"SELECT APPOINTMENTTIME FROM APPOINTMENTS
                              WHERE DOCTORID        = @DoctorId
                                AND APPOINTMENTDATE = @Date
                                AND ID             <> @ExcludeId
                                AND ISDELETED       = FALSE";
        var existingTimes = await connection.QueryAsync<TimeSpan>(sql,
            new { DoctorId = doctorId, Date = date.Date, ExcludeId = excludeId });

        const int bufferMinutes = 15;

        // Ако разликата до кој било постоечки термин е 15 минути или помалку - конфликт
        foreach (var existingTime in existingTimes)
        {
            var diffMinutes = Math.Abs((time - existingTime).TotalMinutes);
            if (diffMinutes <= bufferMinutes)
            {
                return true;
            }
        }

        return false;
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

    public async Task<IEnumerable<TimeSpan>> GetBookedTimesAsync(int doctorId, DateTime date)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT APPOINTMENTTIME
                              FROM APPOINTMENTS
                              WHERE DOCTORID        = @DoctorId
                                AND APPOINTMENTDATE = @Date
                                AND STATUS          <> 'Otkazen'
                                AND ISDELETED        = FALSE";
        return await connection.QueryAsync<TimeSpan>(sql, new { DoctorId = doctorId, Date = date.Date });
    }
}
