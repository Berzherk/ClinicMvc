using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со доктори во базата на податоци.
/// Поддржува Soft Delete (записите никогаш не се физички бришат) и audit колони.
/// </summary>
public class DoctorRepository : IDoctorRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DoctorRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Ги враќа сите АКТИВНИ (не-избришани) доктори, подредени по презиме.
    /// ISDELETED = FALSE секогаш се проверува - ова е Soft Delete образецот.
    /// </summary>
    public async Task<IEnumerable<Doctor>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE,
                                     ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM DOCTORS
                              WHERE ISDELETED = FALSE
                              ORDER BY LASTNAME";
        return await connection.QueryAsync<Doctor>(sql);
    }

    /// <summary>
    /// Странирана листа на активни доктори - 10 по страница (Firebird FIRST/SKIP синтакса).
    /// </summary>
    public async Task<IEnumerable<Doctor>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var validPage = page < 1 ? 1 : page;
        var skip = (validPage - 1) * pageSize;

        const string sql = @"SELECT FIRST @PageSize SKIP @Skip
                                ID, FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE,
                                ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM DOCTORS
                              WHERE ISDELETED = FALSE
                              ORDER BY LASTNAME";
        return await connection.QueryAsync<Doctor>(sql, new { PageSize = pageSize, Skip = skip });
    }

    /// <summary>Вкупен број активни доктори - потребен за пресметка на бројот на страници.</summary>
    public async Task<int> CountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(*) FROM DOCTORS WHERE ISDELETED = FALSE";
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    public async Task<Doctor?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE,
                                     ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM DOCTORS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Doctor>(sql, new { Id = id });
    }

    /// <summary>
    /// Креира нов доктор. createdBy доаѓа од најавениот корисник (се повикува од контролерот).
    /// </summary>
    public async Task<int> CreateAsync(Doctor doctor, string createdBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO DOCTORS
                                (FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE, ISDELETED, CREATEDON, CREATEDBY)
                              VALUES
                                (@FirstName, @LastName, @Specialty, @Phone, @IsActive, FALSE, CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            doctor.FirstName, doctor.LastName, doctor.Specialty, doctor.Phone, doctor.IsActive, CreatedBy = createdBy
        });
    }

    /// <summary>
    /// Ажурира доктор. modifiedBy доаѓа од најавениот корисник.
    /// </summary>
    public async Task UpdateAsync(Doctor doctor, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE DOCTORS
                              SET FIRSTNAME  = @FirstName,
                                  LASTNAME   = @LastName,
                                  SPECIALTY  = @Specialty,
                                  PHONE      = @Phone,
                                  ISACTIVE   = @IsActive,
                                  MODIFIEDON = CURRENT_TIMESTAMP,
                                  MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new
        {
            doctor.Id, doctor.FirstName, doctor.LastName, doctor.Specialty,
            doctor.Phone, doctor.IsActive, ModifiedBy = modifiedBy
        });
    }

    /// <summary>
    /// SOFT DELETE - НЕ го брише редот физички, само поставува ISDELETED = TRUE.
    /// Записот останува во базата (важно за историски термини кои упатуваат кон него).
    /// </summary>
    public async Task DeleteAsync(int id, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE DOCTORS
                              SET ISDELETED  = TRUE,
                                  MODIFIEDON = CURRENT_TIMESTAMP,
                                  MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, ModifiedBy = modifiedBy });
    }

    /// <summary>
    /// Ги враќа сите уникатни специјалности од АКТИВНИТЕ доктори.
    /// </summary>
    public async Task<IEnumerable<string>> GetSpecialtiesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT DISTINCT SPECIALTY
                              FROM DOCTORS
                              WHERE SPECIALTY IS NOT NULL AND ISDELETED = FALSE
                              ORDER BY SPECIALTY";
        return await connection.QueryAsync<string>(sql);
    }

    /// <summary>
    /// Ги враќа денешните термини за конкретен доктор (за Doctor Details / Dashboard страниците).
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetTodayScheduleAsync(int doctorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT
                                a.ID, a.DOCTORID, a.PARIENTID AS PATIENTID,
                                a.APPOINTMENTDATE, a.APPOINTMENTTIME, a.STATUS, a.NOTES,
                                (p.FIRSTNAME || ' ' || p.LASTNAME) AS PATIENTNAME
                              FROM APPOINTMENTS a
                              JOIN PATIENTS p ON p.ID = a.PARIENTID
                              WHERE a.DOCTORID = @DoctorId
                                AND a.APPOINTMENTDATE = CURRENT_DATE
                                AND a.ISDELETED = FALSE
                              ORDER BY a.APPOINTMENTTIME";
        return await connection.QueryAsync<Appointment>(sql, new { DoctorId = doctorId });
    }
}
