using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со доктори во базата на податоци.
/// Содржи сите SQL операции поврзани со табелата DOCTORS.
/// </summary>
public class DoctorRepository : IDoctorRepository
{
    // Фабрика за креирање на конекција со Firebird базата
    private readonly IDbConnectionFactory _connectionFactory;

    public DoctorRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Ги враќа сите доктори подредени по презиме.
    /// </summary>
    public async Task<IEnumerable<Doctor>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE
                              FROM DOCTORS
                              ORDER BY LASTNAME";
        return await connection.QueryAsync<Doctor>(sql);
    }

    /// <summary>
    /// Го враќа еден доктор според ID.
    /// Враќа null ако докторот не постои.
    /// </summary>
    public async Task<Doctor?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE
                              FROM DOCTORS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<Doctor>(sql, new { Id = id });
    }

    /// <summary>
    /// Креира нов доктор во базата.
    /// Го враќа ID-то на новиот запис (RETURNING ID).
    /// </summary>
    public async Task<int> CreateAsync(Doctor doctor)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO DOCTORS (FIRSTNAME, LASTNAME, SPECIALTY, PHONE, ISACTIVE)
                              VALUES (@FirstName, @LastName, @Specialty, @Phone, @IsActive)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, doctor);
    }

    /// <summary>
    /// Ажурира постоечки доктор во базата.
    /// </summary>
    public async Task UpdateAsync(Doctor doctor)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE DOCTORS
                              SET FIRSTNAME = @FirstName,
                                  LASTNAME  = @LastName,
                                  SPECIALTY = @Specialty,
                                  PHONE     = @Phone,
                                  ISACTIVE  = @IsActive
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, doctor);
    }

    /// <summary>
    /// Брише доктор од базата според ID.
    /// Ќе фрли грешка ако докторот има поврзани термини (FK constraint).
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM DOCTORS WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    /// <summary>
    /// Ги враќа сите уникатни специјалности од табелата DOCTORS.
    /// Се користи за полнење на филтерот по специјалност.
    /// </summary>
    public async Task<IEnumerable<string>> GetSpecialtiesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT DISTINCT SPECIALTY
                              FROM DOCTORS
                              WHERE SPECIALTY IS NOT NULL
                              ORDER BY SPECIALTY";
        return await connection.QueryAsync<string>(sql);
    }
}
