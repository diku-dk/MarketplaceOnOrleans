using System.Globalization;
using System.Text;
using Npgsql;

namespace Orleans.Infra;

public static class Helper
{
    static readonly CultureInfo enUS;
    static readonly DateTimeFormatInfo dtfi;

    static Helper()
    {
        enUS = CultureInfo.CreateSpecificCulture("en-US");
        dtfi = enUS.DateTimeFormat;
    }

    public static string GetInvoiceNumber(int customerId, DateTime timestamp, int orderId)
        => new StringBuilder().Append(customerId).Append("-")
                              .Append(timestamp.ToString("d", enUS)).Append("-")
                              .Append(orderId).ToString();

    public static int GetShipmentActorID(int customerID) => customerID % Constants.NumShipmentActors;

    /*
     * https://www.cybertec-postgresql.com/en/postgresql-delete-vs-truncate/
     */
    public static async Task CleanUpPostgres()
    {
        var dataSource = NpgsqlDataSource.Create(Constants.postgresConnectionString);
        var cmd = dataSource.CreateCommand("TRUNCATE public.orleansstorage");
        await cmd.ExecuteNonQueryAsync();
    }

    // clean all orleans states in batch
    public static async Task ResetPostgres()
    {
        var dataSource = NpgsqlDataSource.Create(Constants.postgresConnectionString);
        var cmd = dataSource.CreateCommand("UPDATE public.orleansstorage SET payloadbinary=NULL");
        await cmd.ExecuteNonQueryAsync();
    }

    private static readonly string[] dirs = { "Orleans.Grains.OrderActor", "Orleans.Grains.PaymentActor", "Orleans.Grains.SellerActor", "Orleans.Grains.ShipmentActor" };

    public static void CleanLogFiles()
    {
		string startDirectory = Directory.GetCurrentDirectory();
        Environment.CurrentDirectory = startDirectory;
        foreach(var dir in dirs)
        {
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToList().ForEach(File.Delete);
            Directory.Delete(dir, true);
        }

        if(Directory.Exists("WAL"))
            Directory.Delete("WAL", true);
    }
}