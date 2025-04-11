namespace OrleansApp.Infra;

public static class Helper
{

    public static int GetShipmentActorID(int customerID, int NumShipmentActors) => customerID % NumShipmentActors;
   
}