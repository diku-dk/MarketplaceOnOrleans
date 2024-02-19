using Common.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace OrleansApp.Infra;

public interface IPersistence
{
    Task Log(string type, string key, string value, string tableName = "log");
    Task SetUpLog();
    Task CleanLog();
    Task TruncateStorage();
    Task ResetActorStates();
}

public class EtcNullPersistence : IPersistence
{
    public Task CleanLog()
    {
        return Task.CompletedTask;
    }

    public Task Log(string type, string key, string value, string tableName = "log")
    {
        return Task.CompletedTask;
    }

    public Task ResetActorStates()
    {
        return Task.CompletedTask;
    }

    public Task SetUpLog()
    {
        return Task.CompletedTask;
    }

    public Task TruncateStorage()
    {
        return Task.CompletedTask;
    }
}

public class PostgreSQLPersistence : IPersistence
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ILogger<PostgreSQLPersistence> logger;

    public PostgreSQLPersistence(AppConfig config, ILogger<PostgreSQLPersistence> logger)
    {
        this.dataSource = NpgsqlDataSource.Create(config.AdoNetConnectionString);
        this.logger = logger;
    }

    public async Task SetUpLog()
    {
        var cmd = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS public.log (\"type\" varchar NULL,\"key\" varchar NULL, value varchar NULL);");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CleanLog()
    {
         var cmd = dataSource.CreateCommand("TRUNCATE TABLE public.log;");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task Log(string type, string key, string value, string tableName = "log")
    {
        var stmt = string.Format(@"INSERT INTO public.""{0}"" (""type"",""key"",""value"") VALUES ('{1}','{2}','{3}')", tableName, type, key, value);
        using var command = dataSource.CreateCommand(stmt);
        // cannot return the command result task to orleans because orleans do not know how to deserialize it
        await command.ExecuteNonQueryAsync();
    }

    public async Task TruncateStorage()
    {
        var cmd = dataSource.CreateCommand("TRUNCATE public.orleansstorage");
        await cmd.ExecuteNonQueryAsync();
    }

    // clean all orleans states in batch
    // THIS METHOD DOES NOT CLEAN THE STATE INSIDE ACTOR MEMORY ON RUNTIME!!!
    public async Task ResetActorStates()
    {
        var cmd = dataSource.CreateCommand("UPDATE public.orleansstorage SET payloadbinary=NULL");
        await cmd.ExecuteNonQueryAsync();
    }
}