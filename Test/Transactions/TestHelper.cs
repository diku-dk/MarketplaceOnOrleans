using Npgsql;
using Orleans.Infra;

namespace Test.Transactions;

internal static class TestHelper
{
    public static async Task CleanUpPostgres()
    {
        var dataSource = NpgsqlDataSource.Create(Constants.postgresConnectionString);
        var cmd = dataSource.CreateCommand("Delete From public.orleansstorage");
        await cmd.ExecuteNonQueryAsync();
    }
}