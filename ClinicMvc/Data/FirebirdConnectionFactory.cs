using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace ClinicMvc.Data;

/// <summary>
/// Фабрика за креирање на конекции со Firebird базата на податоци.
/// Го чита connection string-от од appsettings.json преку IConfiguration.
/// </summary>
public class FirebirdConnectionFactory : IDbConnectionFactory
{
    // Connection string прочитан од appsettings.json
    private readonly string _connectionString;

    /// <summary>
    /// Конструктор - го зема connection string-от од конфигурацијата
    /// </summary>
    public FirebirdConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("FirebirdConnection")
            ?? throw new InvalidOperationException(
                "Конекциониот стринг 'FirebirdConnection' не е дефиниран во appsettings.json.");
    }

    /// <summary>
    /// Креира и отвора нова конекција со Firebird базата.
    /// Секој повик враќа нова конекција која треба да се затвори по употреба (using блок).
    /// </summary>
    public IDbConnection CreateConnection()
    {
        var connection = new FbConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
