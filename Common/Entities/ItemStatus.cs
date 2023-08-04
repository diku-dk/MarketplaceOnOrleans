using System;
namespace Common.Entities
{
	/**
	 * Refers to cart or stock item
	 */
	public enum ItemStatus
	{
		DELETED, // deleted from DB
		OUT_OF_STOCK, //
		PRICE_DIVERGENCE,
		IN_STOCK
	}
}

