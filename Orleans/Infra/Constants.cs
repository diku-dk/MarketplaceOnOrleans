namespace Orleans.Infra;

public sealed class Constants
{

    public const string OrleansStorage = "OrleansStorage";


    public const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=128";
  

    public const string DefaultStreamStorage = "PubSubStore";
    public const string DefaultStreamProvider = "StreamProvider";

    public const string ProductNameSpace = "ProductNs";
    public const string OrderNameSpace = "OrderNs";

    public static readonly Guid ProductStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC1");


    public const int NumShipmentActors = 10;

    public static readonly string MarkNamespace = "MarkNs";
    public static readonly Guid CheckoutMarkStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC2");
    public static readonly Guid ProductUpdateMarkStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC3");
}