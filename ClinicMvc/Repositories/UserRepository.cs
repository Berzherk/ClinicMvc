using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за управување со корисници (USERS табела).
/// Се користи за најава и управување со кориснички сметки.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

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
        const string sql = @"SELECT ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID,
                                     CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM USERS WHERE USERNAME = @Username";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    /// <summary>
    /// Го враќа корисничкиот запис поврзан со конкретен доктор (ако постои).
    /// </summary>
    public async Task<User?> GetByDoctorIdAsync(int doctorId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID,
                                     CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM USERS WHERE DOCTORID = @DoctorId";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { DoctorId = doctorId });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID,
                                     CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM USERS ORDER BY USERNAME";
        return await connection.QueryAsync<User>(sql);
    }

    /// <summary>Странирана листа на кориснички сметки - 10 по страница.</summary>
    public async Task<IEnumerable<User>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();
        var validPage = page < 1 ? 1 : page;
        var skip = (validPage - 1) * pageSize;

        const string sql = @"SELECT FIRST @PageSize SKIP @Skip
                                ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID,
                                CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
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
        const string sql = @"SELECT ID, USERNAME, PASSWORDHASH, ROLE, DOCTORID,
                                     CREATEDON, CREATEDBY, MODIFIEDON, MODIFIEDBY
                              FROM USERS WHERE ID = @Id";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO USERS
                                (USERNAME, PASSWORDHASH, ROLE, DOCTORID, CREATEDON, CREATEDBY)
                              VALUES
                                (@Username, @PasswordHash, @Role, @DoctorId, CURRENT_TIMESTAMP, @CreatedBy)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task UpdateAsync(User user)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"UPDATE USERS SET
                                USERNAME     = @Username,
                                ROLE         = @Role,
                                DOCTORID     = @DoctorId,
                                MODIFIEDON   = CURRENT_TIMESTAMP,
                                MODIFIEDBY   = @ModifiedBy
                              WHERE ID = @Id";
        await connection.ExecuteAsync(sql, user);
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
}
