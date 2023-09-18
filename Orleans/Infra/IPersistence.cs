using Microsoft.Extensions.Logging;
using Npgsql;

namespace Orleans.Infra;

public interface IPersistence
{
    void Log(string type, string key, string value, string tableName = "log");
}

public class PostgreSQLPersistence : IPersistence
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<PostgreSQLPersistence> logger;

    public PostgreSQLPersistence(ILogger<PostgreSQLPersistence> logger)
    {
        this.dataSource = NpgsqlDataSource.Create(Constants.PostgresConnectionString);
        this.logger = logger;
    }
   
    public void Log(string type, string key, string value, string tableName = "log")
    {
        var stmt = string.Format(@"INSERT INTO public.""{0}"" (""type"",""key"",""value"") VALUES ('{1}','{2}','{3}')", tableName, type, key, value);
        using var command = dataSource.CreateCommand(stmt);
        command.ExecuteNonQuery();
    }

}