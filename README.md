# MarketplaceOnOrleans

Modeling actors:
1 cart actor per customer. ID is customer_id
1 customer actor per customer. ID is customer_id
1 product actor per product. ID is composite [seller_id,product_id]
1 seller actor per seller. ID is seller_id
1 stock actor per stock item. ID is composite [seller_id,product_id]
1 order actor per customer. ID is customer_id
1 payment actor per customer. ID is customer_id
1 shipment actor per seller. ID is seller_id


