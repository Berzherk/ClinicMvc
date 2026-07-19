using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со пациенти. Поддржува Soft Delete и audit колони.
/// </summary>
public class PatientRepository : IPatientRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PatientRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Patient>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL,
                                     ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM PATIENTS
                              WHERE ISDELETED = FALSE
                              ORDER BY LASTNAME, FIRSTNAME";
        return await connection.QueryAsync<Patient>(sql);
    }

    /// <summary>Странирана листа на активни пациенти - 10 по страница.</summary>
    public async Task<IEnumerable<Patient>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var validPage = page < 1 ? 1 : page;
        var skip = (validPage - 1) * pageSize;

        const string sql = @"SELECT FIRST @PageSize SKIP @Skip
                                ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL,
                                ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM PATIENTS
                              WHERE ISDELETED = FALSE
                              ORDER BY LASTNAME, FIRSTNAME";
        return await connection.QueryAsync<Patient>(sql, new { PageSize = pageSize, Skip = skip });
    }

    /// <summary>Вкупен број активни пациенти.</summary>
    public async Task<int> CountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(*) FROM PATIENTS WHERE ISDELETED = FALSE";
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    /// <summary>
    /// Ги гради WHERE условите за филтрите Ime/Презиме/ЕМБГ - заеднички за
    /// SearchPagedAsync и SearchCountAsync, за да не се повторува иста логика.
    /// </summary>
    private static (string WhereClause, DynamicParameters Parameters) BuildWhereClause(PatientFilter filter)
    {
        var sb = new System.Text.StringBuilder("WHERE ISDELETED = FALSE");
        var p  = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.FirstName))
        {
            sb.Append(" AND UPPER(FIRSTNAME) LIKE UPPER(@FirstName)");
            p.Add("FirstName", $"%{filter.FirstName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.LastName))
        {
            sb.Append(" AND UPPER(LASTNAME) LIKE UPPER(@LastName)");
            p.Add("LastName", $"%{filter.LastName.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.Embg))
        {
            sb.Append(" AND EMBG LIKE @Embg");
            p.Add("Embg", $"%{filter.Embg.Trim()}%");
        }

        return (sb.ToString(), p);
    }

    /// <summary>Странирано пребарување на пациенти според филтрите.</summary>
    public async Task<IEnumerable<Patient>> SearchPagedAsync(PatientFilter filter, int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var validPage = page < 1 ? 1 : page;
        p.Add("PageSize", pageSize);
        p.Add("Skip", (validPage - 1) * pageSize);

        var sql = $@"SELECT FIRST @PageSize SKIP @Skip
                        ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL,
                        ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                     FROM PATIENTS
                     {where}
                     ORDER BY LASTNAME, FIRSTNAME";
        return await connection.QueryAsync<Patient>(sql, p);
    }

    /// <summary>Вкупен број пациенти кои одговараат на филтрите.</summary>
    public async Task<int> SearchCountAsync(PatientFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var (where, p) = BuildWhereClause(filter);

        var sql = $"SELECT COUNT(*) FROM PATIENTS {where}";
        return await connection.ExecuteScalarAsync<int>(sql, p);
    }

    public async Task<Patient?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL,
                                     ISDELETED, CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM PATIENTS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Patient>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(Patient patient, string createdBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO PATIENTS
                                (FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL, ISDELETED, CREATEDON, CREATEDBY)
                              VALUES
                                (@FirstName, @LastName, @Embg, @Phone, @Email, FALSE, CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            patient.FirstName, patient.LastName, patient.Embg, patient.Phone, patient.Email, CreatedBy = createdBy
        });
    }

    public async Task UpdateAsync(Patient patient, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE PATIENTS SET
                                FIRSTNAME  = @FirstName,
                                LASTNAME   = @LastName,
                                EMBG       = @Embg,
                                PHONE      = @Phone,
                                EMAIL      = @Email,
                                MODIFIEDON = CURRENT_TIMESTAMP,
                                MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new
        {
            patient.Id, patient.FirstName, patient.LastName, patient.Embg,
            patient.Phone, patient.Email, ModifiedBy = modifiedBy
        });
    }

    /// <summary>SOFT DELETE - записот останува во базата поради поврзаните термини (историја).</summary>
    public async Task DeleteAsync(int id, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE PATIENTS
                              SET ISDELETED  = TRUE,
                                  MODIFIEDON = CURRENT_TIMESTAMP,
                                  MODIFIEDBY = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, ModifiedBy = modifiedBy });
    }

    public async Task<bool> EmbgExistsAsync(string embg, int excludeId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT COUNT(*) FROM PATIENTS
                              WHERE EMBG = @Embg AND ID <> @ExcludeId AND ISDELETED = FALSE";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Embg = embg, ExcludeId = excludeId });
        return count > 0;
    }

    /// <summary>
    /// Медицинска историја - сите претходни термини на пациентот, со ime на доктор и специјалност.
    /// Се користи на Patient Details страницата.
    /// </summary>
    public async Task<IEnumerable<Appointment>> GetMedicalHistoryAsync(int patientId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT
                                a.ID, a.DOCTORID, a.PARIENTID AS PATIENTID,
                                a.APPOINTMENTDATE, a.APPOINTMENTTIME, a.STATUS, a.NOTES,
                                (d.FIRSTNAME || ' ' || d.LASTNAME) AS DOCTORNAME,
                                d.SPECIALTY AS DOCTORSPECIALTY
                              FROM APPOINTMENTS a
                              JOIN DOCTORS d ON d.ID = a.DOCTORID
                              WHERE a.PARIENTID = @PatientId AND a.ISDELETED = FALSE
                              ORDER BY a.APPOINTMENTDATE DESC, a.APPOINTMENTTIME DESC";
        return await connection.QueryAsync<Appointment>(sql, new { PatientId = patientId });
    }
}
