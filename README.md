# MarketplaceOnOrleans

Modeling actors:
1 cart actor per customer. ID is customer_id
1 customer actor per customer. ID is customer_id
1 product actor per product. ID is composite [seller_id,product_id]
1 seller actor per seller. ID is seller_id
1 stock actor per stock item. ID is composite [seller_id,product_id]
1 order actor per customer. ID is customer_id
1 payment actor per customer. ID is customer_id
1 shipment actor per partition of customers. Hash is customer_id. Number of partitions is predefined.


Actors that log historical records:
order, payment, shipment
also seller because of the dashboard

It is single thread per function.
Since we have one event per function call, to minimize latency, we map each entity to a logical function, e.g., order, payment, and shipment.


order_id he hashes it to N
the function that is called

set N to the number of customers. and switch to customr id as hash attribute.
