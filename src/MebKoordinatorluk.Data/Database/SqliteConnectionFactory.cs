using System.Data;
using Microsoft.Data.Sqlite;

namespace MebKoordinatorluk.Data.Database;

public interface ISqliteConnectionFactory
{
    IDbConnection CreateConnection();
    string DatabasePath { get; }
}

public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly DatabaseConfig _config;

    public SqliteConnectionFactory(DatabaseConfig config)
    {
        _config = config;
    }

    public string DatabasePath => _config.DatabasePath;

    public IDbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_config.ConnectionString);
        connection.Open();
        return connection;
    }
}
