using System.Data;
using MySql.Data.MySqlClient;

namespace Autoscaler.Persistence.Connection;

public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Connection => new MySqlConnection(_connectionString);
}