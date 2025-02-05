using System.Data;

namespace P7Internet.Persistence.Connection;

public interface IDbConnectionFactory
{
    public IDbConnection Connection { get; }
}