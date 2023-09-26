using Npgsql;

namespace Test.Infra;

public class DBHelper
{
    public const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=128";
    static readonly NpgsqlDataSource dataSource = NpgsqlDataSource.Create(PostgresConnectionString);

    public static void SetUpLog()
    {
        var cmd = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS public.log (\"type\" varchar NULL,\"key\" varchar NULL, value varchar NULL);");
        cmd.ExecuteNonQuery();
    }

    public static void CleanLog()
    {
        var cmd = dataSource.CreateCommand("TRUNCATE public.log");
        cmd.ExecuteNonQuery();
    }

    /*
     * https://www.cybertec-postgresql.com/en/postgresql-delete-vs-truncate/
     */
    public static void TruncateOrleansStorage()
    {
        var cmd = dataSource.CreateCommand("TRUNCATE public.orleansstorage");
        cmd.ExecuteNonQuery();
    }

}

