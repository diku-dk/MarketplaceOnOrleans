# MarketplaceOnOrleans

MarketplaceOnOrleans is the [Microsoft Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/) port of Online Marketplace, the application prescribed as part of a microservice-based
benchmark of same name being designed by the [Data Management Systems (DMS) group](https://di.ku.dk/english/research/sdps/research-groups/dms/) at the University of Copenhagen.
Further details about the benchmark can be found in the benchmark driver [repository](https://github.com/diku-dk/EventBenchmark).

## Table of Contents
- [Getting Started](#getting-started)
    * [Prerequisites](#prerequisites)
    * [New Orleans Users](#orleans)
- [Online Marketplace](#running-benchmark)
    * [Configuration](#config)
    * [Deployment](#deploy)
    * [Testing](#test)
    * [UCloud](#ucloud)
   

### <a name="prerequisites"></a>Prerequisites

- [.NET Framework 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
- IDE (if you want to modify or debug the code): [Visual Studio](https://visualstudio.microsoft.com/vs/community/) or [VSCode](https://code.visualstudio.com/)

### <a name="orleans"></a>New Orleans Users

[Orleans](https://learn.microsoft.com/en-us/dotnet/orleans/) framework provide facilities to program distributed stateful applications at scale using the virtual actor model. We highly recommend starting from the [Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/overview) to further understand the model.

## <a name="running-benchmark"></a>Online Marketplace on Orleans

### <a name="modeling"></a>Actor Modeling

The Orleans virtual actor programming model prescribes a single thread per actor. Since we have one event per function call, to minimize latency, we map each entity to a logical actor, e.g., order, payment, and shipment.

* A cart actor per customer. ID is customer_id
* A customer actor per customer. ID is customer_id
* A product actor per product. ID is composite `[seller_id,product_id]`
* A seller actor per seller. ID is seller_id
* A stock actor per stock item. ID is composite `[seller_id,product_id]`
* An order actor per customer. ID is customer_id
* A payment actor per customer. ID is customer_id
* A shipment actor per partition of customers. Hash to define which shipment actor an order is forwarded to is defined by the hash of `[customer_id]`. Number of partitions is predefined (see [Configuration](#config)).

Actors that log historical records:
Order, Payment, Shipment, and Seller (because of the seller dashboard)

Actors that require resetting state after each run (otherwise they accumulate records from past experiments):
Seller, Order, and Shipment.


### <a name="config"></a>Configuration

The file `appsettings.Production.json` defines entries that refer to configuration parameters. These are applied dynamically on application startup. The parameters and possible values are found in the table below:

Parameter     | Description                                                                         | Value                                             |
------------- |-------------------------------------------------------------------------------------|---------------------------------------------------|
OrleansTransactions | Defines whether Orleans transactions is enabled                                     | `true/false`                                        |
OrleansStorage | Defines whether Orleans storage is enabled                                          | `true/false`                                      |
AdoNetGrainStorage | Defines whether PostgreSQL is used for Orleans storage (otherwise in-memory is used) | `true/false`                                        |
ConnectionString | Defines the connection string to access PostgreSQL                                  | `"Host=?;Port=5432;Database=?;Username=?;Password=?;"` |
LogRecords | Defines whether PostgreSQL is used for logging historical records                   | `true/false`                                        |
NumShipmentActors | Defines the number of shipment actors | `1-N` |

### <a name="deploy"></a>Deploy

You can initialize Orleans silo in two ways:

```
dotnet run --environment Production --urls "http://*:8081" --project Silo
```

```
dotnet run --launch-profile "Silo-Production" --urls "http://*:8081" --project Silo
```

### <a name="test"></a>Testing

There is a suite of tests available for checking some Online Marketplace benchmark constraints. The tests can be found in the following path: [link](Test/Workflows)

### <a name="ucloud"></a>UCloud

The experiment deployment steps below only aply if you have access to [UCloud](https://docs.cloud.sdu.dk/index.html).

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


### <a name="troubleshooting"></a>Supplemental Links for Troubleshooting
* There is no default implementation for environment statistics: [link](https://github.com/dotnet/orleans/issues/8270)
* Some comments about Orleans performance: [link](https://stackoverflow.com/questions/74310628/orleans-slow-with-minimalistic-use-case)
* OneWay messages can lead to deadlock: [link](https://github.com/dotnet/orleans/issues/4808)
* Apparently there is no way to configure injection for IOptions<AppConfig> in Orleans testing. That is why a default AppConfig is provided in the Helper class.
* Some tuning for PostgreSQL: [link](https://stackoverflow.com/questions/30778015/how-to-increase-the-max-connections-in-postgres)