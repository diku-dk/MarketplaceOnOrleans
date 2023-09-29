# MarketplaceOnOrleans

# Deploy
dotnet run --environment Development --project Silo
dotnet run --environment Production --urls "http://*:8081" --project Silo
dotnet run --launch-profile "Silo-Production" --urls "http://*:8081" --project Silo

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
order, payment, shipment, and seller (because of the dashboard)

Actors that require resetting state after each run (otherwise it keeps accumulating records):
Seller, order, shipment.

It is single thread per function.
Since we have one event per function call, to minimize latency, we map each entity to a logical function, e.g., order, payment, and shipment.


order_id he hashes it to N
the function that is called

set N to the number of customers. and switch to customr id as hash attribute.



# Experiment deployment steps
how to run experiments:
1. start silo:  dotnet run --project Silo
2. run exp:  dotnet run --project Orleans "C:\Users\jhs316\Desktop\EventBenchmark\Configuration\orleans_local.json"

how to run exp on vm
1. Select vm: postgres => set name as postgres and set the "postgresql" folder as the DB folder
2. Submit postgres job
3. select vm: Ubuntu xfce => add "Big Data System" folder + connect to postgres run above
4. Submit Ubuntu job
5. Inside the Ubuntu job VM:
(1) select the right folder
(2) run "chmod +x dotnet_setup.sh"
(3) run "./dotnet_setup.sh"      
(4) run "/home/ucloud/.dotnet"
3. upload zip files to the "Big Data System" folder
need to upload both server code and driver
4. return to vm
(1) run "unzip MarketplaceOrleans.zip"
(2) run "unzip EventBenchmark.zip"
5.

# Etc
There is no default implementation for environment statistics: [link](https://github.com/dotnet/orleans/issues/8270)
Some comments about Orleans [performance](https://stackoverflow.com/questions/74310628/orleans-slow-with-minimalistic-use-case)