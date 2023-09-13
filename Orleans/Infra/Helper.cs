using System.Globalization;
using System.IO;
using System.Text;
using Npgsql;

namespace Orleans.Infra;

public static class Helper
{
    static readonly CultureInfo enUS = CultureInfo.CreateSpecificCulture("en-US");
    static readonly DateTimeFormatInfo dtfi = enUS.DateTimeFormat;

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

    public static void CleanLogFiles()
    {
		var startDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
        Environment.CurrentDirectory = startDirectory;
		Directory.Delete("WAL");
		Directory.Delete("Orleans.Grains.OrderActor");
        Directory.Delete("Orleans.Grains.PaymentActor");
        Directory.Delete("Orleans.Grains.SellerActor");
        Directory.Delete("Orleans.Grains.ShipmentActor");
    }

}