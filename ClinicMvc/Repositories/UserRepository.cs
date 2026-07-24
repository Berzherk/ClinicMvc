using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со корисници (USERS табела).
/// Се користи за најава и управување со кориснички сметки (Administrator, Doctor, Patient).
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private const string SelectColumns = @"ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID, PATIENTID,
                                            EMAIL, EMAILCONFIRMED,
                                            CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY";

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Го враќа корисникот според корисничко ime.
    /// Клучен метод за најава - враќа null ако корисникот не постои.
    /// </summary>
    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {SelectColumns} FROM USERS WHERE USERNAME = @Username";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {SelectColumns} FROM USERS WHERE UPPER(EMAIL) = UPPER(@Email)";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(*) FROM USERS WHERE UPPER(EMAIL) = UPPER(@Email)";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Email = email });
        return count > 0;
    }

    /// <summary>Странирана листа на кориснички сметки - 10 по страница.</summary>
    public async Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var validPage = page < 1 ? 1 : page;
        var skip = (validPage - 1) * pageSize;

        var sql = $@"SELECT FIRST @PageSize SKIP @Skip {SelectColumns}
                      FROM USERS ORDER BY USERNAME";
        return await connection.QueryAsync<User>(sql, new { PageSize = pageSize, Skip = skip });
    }

    /// <summary>Вкупен број кориснички сметки.</summary>
    public async Task<int> CountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(*) FROM USERS";
        return await connection.ExecuteScalarAsync<int>(sql);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {SelectColumns} FROM USERS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO USERS
                                (USERNAME, PASSWORDHASH, ROLE, DOCTORID, PATIENTID, EMAIL, EMAILCONFIRMED,
                                 CREATEDON, CREATEDBY)
                              VALUES
                                (@Username, @PasswordHash, @Role, @DoctorId, @PatientId, @Email, @EmailConfirmed,
                                 CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM USERS WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task UpdatePasswordAsync(int id, string newPasswordHash, string modifiedBy)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE USERS SET
                                PASSWORDHASH = @PasswordHash,
                                MODIFIEDON   = CURRENT_TIMESTAMP,
                                MODIFIEDBY   = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, PasswordHash = newPasswordHash, ModifiedBy = modifiedBy });
    }

    public async Task<User?> GetByDoctorIdAsync(int doctorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = $"SELECT {SelectColumns} FROM USERS WHERE DOCTORID = @DoctorId";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { DoctorId = doctorId });
    }

    public async Task ConfirmEmailAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE USERS SET
                                EMAILCONFIRMED = TRUE,
                                MODIFIEDON     = CURRENT_TIMESTAMP,
                                MODIFIEDBY     = 'system (email confirmation)'
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = userId });
    }
}
