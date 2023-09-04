﻿namespace Orleans.Infra
{
    public sealed class Constants
    {

        public const string OrleansStorage = "OrleansStorage";

        public const string DefaultStreamStorage = "PubSubStore";
        public const string DefaultStreamProvider = "StreamProvider";

        public const string ProductNameSpace = "ProductNs";
        public const string OrderNameSpace = "OrderNs";

        public static readonly Guid ProductStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC1");


        public const int NumShipmentActors = 100;

        public static readonly string MarkNamespace = "MarkNs";
        public static readonly Guid CheckoutMarkStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC2");

    }
}
