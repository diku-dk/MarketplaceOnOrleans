using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Expressions.Internal;

namespace Orleans.Infra;

public interface IPersistence
{
    void Put(string tableName, string key, string value);
}

public class FilePersistence : IPersistence
{
    public FilePersistence()
    {
    }

    public void Put(string tableName, string key, string value)
    {
    }
}