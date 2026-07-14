using System.Text;
using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со термини во базата на податоци.
/// Содржи сите SQL операции поврзани со табелата APPOINTMENTS.
/// </summary>
public class AppointmentRepository : IAppointmentRepository
{
    // Фабрика за креирање на конекција со Firebird базата
    private readonly IDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Основен SELECT со JOIN-ови за прикажување на термините со имиња на лекар и пациент.
    /// Забелешка: колоната PARIENTID е типографска грешка во базата (наместо PATIENTID),
    /// затоа ја алиасираме до PATIENTID за да одговара на C# моделот.
    /// </summary>
    private const string SelectSql = @"
        SELECT
            a.ID,
            a.DOCTORID,
            a.PARIENTID      AS PATIENTID,
            a.APPOINTMENTDATE,
            a.APPOINTMENTTIME,
            a.STATUS,
            a.NOTES,
            (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
            (p.FIRSTNAME || ' ' || p.LASTNAME) AS PATIENTNAME,
            d.SPECIALTY AS DOCTORSPECIALTY
        FROM APPOINTMENTS a
        JOIN DOCTORS  d ON d.ID = a.DOCTORID
        JOIN PATIENTS p ON p.ID = a.PARIENTID";

    public AppointmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Ги враќа сите термини без филтри.
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetAllAsync()
    {
        return await SearchAsync(new AppointmentFilter());
    }

    /// <summary>
    /// Пребарува термини според зadadените филтри.
    /// Динамички гради WHERE клаузула само за полињата кои се пополнети.
    /// </summary>
    public async Task<IEnumerable<Appointment>> SearchAsync(AppointmentFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Почни со базичниот SQL и додавај услови според филтрите
        var sql = new StringBuilder(SelectSql + " WHERE 1=1");
        var p   = new DynamicParameters();

        // Филтер по лекар
        if (filter.DoctorId.HasValue)
        {
            sql.Append(" AND a.DOCTORID = @DoctorId");
            p.Add("DoctorId", filter.DoctorId.Value);
        }
        // Филтер по специјалност
        if (!string.IsNullOrWhiteSpace(filter.Specialty))
        {
            sql.Append(" AND d.SPECIALTY = @Specialty");
            p.Add("Specialty", filter.Specialty);
        }
        // Филтер по статус
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            sql.Append(" AND a.STATUS = @Status");
            p.Add("Status", filter.Status);
        }
        // Филтер по почетен датум
        if (filter.DateFrom.HasValue)
        {
            sql.Append(" AND a.APPOINTMENTDATE >= @DateFrom");
            p.Add("DateFrom", filter.DateFrom.Value.Date);
        }
        // Филтер по краен датум
        if (filter.DateTo.HasValue)
        {
            sql.Append(" AND a.APPOINTMENTDATE <= @DateTo");
            p.Add("DateTo", filter.DateTo.Value.Date);
        }

        // Подреди по датум и време
        sql.Append(" ORDER BY a.APPOINTMENTDATE, a.APPOINTMENTTIME");
        return await connection.QueryAsync<Appointment>(sql.ToString(), p);
    }

    /// <summary>
    /// Го враќа еден термин според ID.
    /// Се користи за полнење на Edit модалот.
    /// </summary>
    public async Task<Appointment?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, DOCTORID, PARIENTID AS PATIENTID,
                                     APPOINTMENTDATE, APPOINTMENTTIME, STATUS, NOTES
                              FROM APPOINTMENTS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Appointment>(sql, new { Id = id });
    }

    /// <summary>
    /// Креира нов термин во базата.
    /// Го враќа ID-то на новиот запис (RETURNING ID).
    /// </summary>
    public async Task<int> CreateAsync(Appointment appointment)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO APPOINTMENTS
                                (DOCTORID, PARIENTID, APPOINTMENTDATE, APPOINTMENTTIME, STATUS, NOTES)
                              VALUES
                                (@DoctorId, @PatientId, @AppointmentDate, @AppointmentTime, @Status, @Notes)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, appointment);
    }

    /// <summary>
    /// Ажурира постоечки термин во базата.
    /// </summary>
    public async Task UpdateAsync(Appointment appointment)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE APPOINTMENTS SET
                                DOCTORID          = @DoctorId,
                                PARIENTID         = @PatientId,
                                APPOINTMENTDATE   = @AppointmentDate,
                                APPOINTMENTTIME   = @AppointmentTime,
                                STATUS            = @Status,
                                NOTES             = @Notes
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, appointment);
    }

    /// <summary>
    /// Брише термин од базата според ID.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM APPOINTMENTS WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Проверува дали докторот веќе има термин во истото датум и време.
    /// Се користи за спречување на дупли термини.
    /// excludeId - ID на терминот кој се игнорира (при измена на постоечки термин)
    /// </summary>
    public async Task<bool> HasConflictAsync(int doctorId, DateTime date, TimeSpan time, int excludeId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT COUNT(*) FROM APPOINTMENTS
                              WHERE DOCTORID        = @DoctorId
                                AND APPOINTMENTDATE = @Date
                                AND APPOINTMENTTIME = @Time
                                AND ID             <> @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql,
            new { DoctorId = doctorId, Date = date.Date, Time = time, ExcludeId = excludeId });
        return count > 0;
    }

    /// <summary>
    /// Го менува статусот на термин (пр. Zakazan → Vo tek → Zavrsen).
    /// Ако се проследат белешки, ги ажурира и нив (се користи при завршување на преглед).
    /// Ако notes е null, полето NOTES во базата останува непроменето.
    /// </summary>
    public async Task UpdateStatusAsync(int id, string newStatus, string? notes = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Ако се проследени белешки - ажурирај и статус и белешки
        // Ако не се проследени - ажурирај само статус (COALESCE го задржува старото NOTES)
        const string sql = @"UPDATE APPOINTMENTS
                              SET STATUS = @Status,
                                  NOTES  = COALESCE(@Notes, NOTES)
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, Status = newStatus, Notes = notes });
    }

    /// <summary>
    /// Ги враќа сите закажани времиња за конкретен доктор на конкретен датум.
    /// Откажаните термини (Otkazen) НЕ се сметаат за зафатени - тој термин е слободен.
    /// Се користи за пресметка на слободни термини.
    /// </summary>
    public async Task<IEnumerable<TimeSpan>> GetBookedTimesAsync(int doctorId, DateTime date)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT APPOINTMENTTIME
                              FROM APPOINTMENTS
                              WHERE DOCTORID        = @DoctorId
                                AND APPOINTMENTDATE = @Date
                                AND STATUS          <> 'Otkazen'";
        return await connection.QueryAsync<TimeSpan>(sql, new { DoctorId = doctorId, Date = date.Date });
    }
}
