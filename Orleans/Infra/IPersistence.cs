using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Orleans.Infra;

public interface IPersistence
{
    Task Log(string type, string key, string value, string tableName = "log");
    Task SetUpLog();
    Task CleanLog();
    Task CleanDb();
}

public class PostgreSQLPersistence : IPersistence
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<PostgreSQLPersistence> logger;
    private readonly AppConfig config;

    public PostgreSQLPersistence(IOptions<AppConfig> config, ILogger<PostgreSQLPersistence> logger)
    {
        this.config = config.Value;
        this.dataSource = NpgsqlDataSource.Create(config.Value.ConnectionString);
        this.logger = logger;
    }

    public async Task SetUpLog()
    {
        var cmd = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS public.log (\"type\" varchar NULL,\"key\" varchar NULL, value varchar NULL);");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CleanLog()
    {
         var cmd = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS public.log (\"type\" varchar NULL,\"key\" varchar NULL, value varchar NULL);");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task Log(string type, string key, string value, string tableName = "log")
    {
        var stmt = string.Format(@"INSERT INTO public.""{0}"" (""type"",""key"",""value"") VALUES ('{1}','{2}','{3}')", tableName, type, key, value);
        using var command = dataSource.CreateCommand(stmt);
        // cannot return the command result task to orleans because orleans do not know how to deserialize it
        await command.ExecuteNonQueryAsync();
    }

    public async Task CleanDb()
    {
        var cmd = dataSource.CreateCommand("TRUNCATE public.orleansstorage");
        await cmd.ExecuteNonQueryAsync();
    }
}