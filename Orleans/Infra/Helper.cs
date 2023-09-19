using Npgsql;

namespace Orleans.Infra;

public static class Helper
{

    static readonly NpgsqlDataSource dataSource = NpgsqlDataSource.Create(Constants.PostgresConnectionString);

    public static int GetShipmentActorID(int customerID) => customerID % Constants.NumShipmentActors;

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

    // clean all orleans states in batch
    // THIS METHOD DOES NOT CLEAN THE STATE INSIDE ACTOR MEMORY!!!
    public static void ResetActorStates()
    {
        var cmd = dataSource.CreateCommand("UPDATE public.orleansstorage SET payloadbinary=NULL");
        cmd.ExecuteNonQuery();
    }

}