using System.Globalization;
using System.Text;

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
}