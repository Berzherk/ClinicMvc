using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со пациенти во базата на податоци.
/// Содржи сите SQL операции поврзани со табелата PATIENTS.
/// </summary>
public class PatientRepository : IPatientRepository
{
    // Фабрика за креирање на конекција со Firebird базата
    private readonly IDbConnectionFactory _connectionFactory;

    public PatientRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Ги враќа сите пациенти подредени по презиме па по име.
    /// </summary>
    public async Task<IEnumerable<Patient>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL
                              FROM PATIENTS
                              ORDER BY LASTNAME, FIRSTNAME";
        return await connection.QueryAsync<Patient>(sql);
    }

    /// <summary>
    /// Го враќа еден пациент според ID.
    /// Враќа null ако пациентот не постои.
    /// </summary>
    public async Task<Patient?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL
                              FROM PATIENTS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Patient>(sql, new { Id = id });
    }

    /// <summary>
    /// Креира нов пациент во базата.
    /// Го враќа ID-то на новиот запис (RETURNING ID).
    /// </summary>
    public async Task<int> CreateAsync(Patient patient)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO PATIENTS (FIRSTNAME, LASTNAME, EMBG, PHONE, EMAIL)
                              VALUES (@FirstName, @LastName, @Embg, @Phone, @Email)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, patient);
    }

    /// <summary>
    /// Ажурира постоечки пациент во базата.
    /// </summary>
    public async Task UpdateAsync(Patient patient)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE PATIENTS SET
                                FIRSTNAME = @FirstName,
                                LASTNAME  = @LastName,
                                EMBG      = @Embg,
                                PHONE     = @Phone,
                                EMAIL     = @Email
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, patient);
    }

    /// <summary>
    /// Брише пациент од базата според ID.
    /// Ќе фрли грешка ако пациентот има поврзани термини (FK constraint).
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM PATIENTS WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Проверува дали ЕМБГ веќе постои во базата.
    /// excludeId - ID на пациентот кој се игнорира (при измена на постоечки пациент)
    /// Враќа true ако ЕМБГ е веќе зафатен од друг пациент.
    /// </summary>
    public async Task<bool> EmbgExistsAsync(string embg, int excludeId = 0)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT COUNT(*) FROM PATIENTS
                              WHERE EMBG = @Embg AND ID <> @ExcludeId";
        var count = await connection.ExecuteScalarAsync<int>(sql,
            new { Embg = embg, ExcludeId = excludeId });
        return count > 0;
    }
}
