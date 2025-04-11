using OrleansApp.Interfaces;

namespace Orleans.Interfaces.SellerView;

/**
 * To have strong consistent seller dashboard, methods that alter the view state should not be one way
 * However, there is a severe performance penalty compared to baseline (in-memory view maintenance)
 */
public interface ISellerViewActor : ISellerActor
{

}