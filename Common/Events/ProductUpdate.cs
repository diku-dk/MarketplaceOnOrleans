namespace Common.Events
{
    public record ProductUpdate
	(
         int seller_id,
         int product_id,
         float price,
         bool active,
         int instanceId
    );
}