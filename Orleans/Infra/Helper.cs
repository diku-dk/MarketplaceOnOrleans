namespace Orleans.Infra;

public static class Helper
{
    public static int GetShipmentActorID(int customerID) => customerID % Constants.NumShipmentActors;
   
}