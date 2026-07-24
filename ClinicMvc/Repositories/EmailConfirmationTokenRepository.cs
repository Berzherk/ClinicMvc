using ClinicMvc.Data;
using ClinicMvc.Models;
using Dapper;

namespace ClinicMvc.Repositories;

/// <summary>
/// Репозиториум за EMAILCONFIRMATIONTOKENS табелата - токени со кои се потврдува
/// е-поштата на нови Doctor/Patient сметки.
/// </summary>
public class EmailConfirmationTokenRepository : IEmailConfirmationTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EmailConfirmationTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> CreateAsync(int userId, string token, DateTime expiresOn)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"INSERT INTO EMAILCONFIRMATIONTOKENS (USERID, TOKEN, EXPIRESON, CREATEDON)
                              VALUES (@UserId, @Token, @ExpiresOn, CURRENT_TIMESTAMP)
                              RETURNING ID";
        return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, Token = token, ExpiresOn = expiresOn });
    }

    public async Task<EmailConfirmationToken?> GetByTokenAsync(string token)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"SELECT ID, USERID, TOKEN, EXPIRESON, CREATEDON
                              FROM EMAILCONFIRMATIONTOKENS WHERE TOKEN = @Token";
        return await connection.QueryFirstOrDefaultAsync<EmailConfirmationToken>(sql, new { Token = token });
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM EMAILCONFIRMATIONTOKENS WHERE ID = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task DeleteAllForUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM EMAILCONFIRMATIONTOKENS WHERE USERID = @UserId";
        await connection.ExecuteAsync(sql, new { UserId = userId });
    }
}
