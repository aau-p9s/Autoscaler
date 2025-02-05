using System.Data;

namespace Autoscaler.Persistence.Connection;

public interface IDbConnectionFactory
{
    public IDbConnection Connection { get; }
}