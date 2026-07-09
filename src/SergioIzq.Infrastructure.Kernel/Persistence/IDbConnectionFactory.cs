using System.Data;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
