using Microsoft.Extensions.Logging;
using Npgsql;

namespace Orleans.Infra;

public interface IPersistence
{
    Task Log(string type, string key, string value, string tableName = "log");
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
   
    public async Task Log(string type, string key, string value, string tableName = "log")
    {
        var stmt = string.Format(@"INSERT INTO public.""{0}"" (""type"",""key"",""value"") VALUES ('{1}','{2}','{3}')", tableName, type, key, value);
        using var command = dataSource.CreateCommand(stmt);
        // cannot return the command result task to orleans because orleans do not know how to deserialize it
        await command.ExecuteNonQueryAsync();
    }

}