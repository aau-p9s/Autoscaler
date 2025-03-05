using System.Data;
using Npgsql;

namespace Autoscaler.Persistence.Connection;

public class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection Connection => new NpgsqlConnection(_connectionString);
}