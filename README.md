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
- [PostgreSQL](https://www.postgresql.org/): If you want to either have durable state, audit logging, or seller dashboard performed via PostgreSQL
- IDE (if you want to modify or debug the code): [Visual Studio](https://visualstudio.microsoft.com/vs/community/) or [VSCode](https://code.visualstudio.com/)
- [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): Version > '7.0.11'. Install only if you want to perform chances in the seller dashboard schema


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

Applicatins settings ca be defined per environment using two files: Development (`appsettings.Development.json`) and Production (`appsettings.Production.json`). We suggest using development file while evolving the application or debugging; while the production file should be used when running experiments.

The `settings.[Development|Production].json` file defines entries that refer to configuration parameters. These are applied dynamically on application startup. The parameters and possible values are found in the table below:

Parameter     | Description                                                                         | Value                                             |
------------- |-------------------------------------------------------------------------------------|---------------------------------------------------|
OrleansStorage | Defines whether Orleans storage is enabled (default to in-memory). Works independently of Orleans Transactions.  | `true/false` |
OrleansTransactions | Defines whether Orleans transactions is enabled. Only works if Orleans Storage is set to true.  | `true/false`    |
AdoNetGrainStorage | Defines whether PostgreSQL is used for Orleans storage (otherwise in-mmeory is used). Only applies if OrleansStorage is set to true. | `true/false`  |
SellerViewPostgres  | Defines whether PostgreSQL is used to provide the Seller Dashboard            | `true/false`                          |
StreamReplication   | Defines whether Orleans Streams is used to stream product updates to Cart actors |  `true/false`                      |
LogRecords        | Defines whether PostgreSQL is used for audit logging                            | `true/false`                          |
ConnectionString  | Defines the connection string to access PostgreSQL. Must be set in case LogRecords or AdoNetGrainStorage is enabled                 | `"Host=?;Port=5432;Database=?;Username=?;Password=?;"` |
NumShipmentActors | Defines the number of shipment actors                                           | `1-N`                                  |

We understand that the number of possibilities for deploying MarketplaceOnOrleans may lead to a certain confusion for newcomers, so we prepared a list of configuration templates that you can follow while experimenting with MarketplaceOnOrleans.

* Eventual consistency + In memory grain state (not managed by Orleans Storage) + Seller actor providing dashboard + No replication of products:


Parameter     | Value
--------------|-------|
OrleansTransactions | false |
OrleansStorage      | false |
StreamReplication   | false |
SellerViewPostgres  | false |

* Transactional consistency + Durable state + PostgreSQL providing Seller Dashboard + Replication of products:

Parameter     | Value
--------------|-------|
OrleansTransactions | true |
OrleansStorage      | true |
AdoNetGrainStorage  | true |
StreamReplication   | true |
SellerViewPostgres  | true |

* Eventual consistency + Durable state + Seller actor providing dashboard + No replication of products + Audit logging:

Parameter     | Value
--------------|-------|
OrleansTransactions | false |
OrleansStorage      | true  |
StreamReplication   | false |
SellerViewPostgres  | false |
LogRecords          | true  |


As can be seen above, the parameters are used to drive a myriad of guarantees and functionalities in OnlineMarketplace.


### <a name="deploy"></a>Deploy

You can initialize Orleans silo in two ways:

```
dotnet run --environment Production --urls "http://*:8081" --project Silo
```

```
dotnet run --launch-profile "Silo-Production" --urls "http://*:8081" --project Silo
```

The project Silo is the startup subproject for this project. Either `--environment` or `--launch-profile` can used to define the environment (i.e., the settings file) to execute the project. The parameter `--urls` is necessary to enable .NET exposing the port for external interaction.

### <a name="test"></a>Testing

There is a suite of tests available for checking some Online Marketplace benchmark functionalities and constraints. The tests can be found in [Test](Test).

To allow the tests to run concurrently, there are two different ClusterFixtures, one for transactional tests and another for non-transactional tests. This is not ideal and perhaps they could be better modularized or even merged, but maintaining the different properties on test runtime.

### <a name="test"></a>Notes about Seller Dashboard View Maintenance

In preliminary commits, we designed the seller dashboard as a materialized view on PostgreSQL.
The idea is to benefit of the query processingand transactional capabilities to obtain a consistent seller dashboard.

However, caution is necessary on refreshing PostgreSQL [Materialized Views](https://www.postgresql.org/docs/current/sql-refreshmaterializedview.html):
"Even with this option [(CONCURRENTLY)] only one REFRESH at a time may run against any one materialized view." 

In other words, seller actors cannot trigger the refresh concurrently. They have to eiher coordinate or let a background worker responsible for periodically refreshing the view.

However, another problem is that, if we have eventual update of the materialized view, the order entries queried as part of the seller dashboard may not be consistent with the view, thus violating the correctness criterion.

To accomodate the above constraints, we defined the following: each seller is responsible for its own materialized view. In this case, the actor single-thread model guarantees there is only one refresh at a time and consequently, the order entries are always in sync with the seller view.

If you desire to modify the data model of seller view, although you can create a new migration, it is simpler to delete the existing one and run the following command in the project's root folder:

```
dotnet ef migrations add InitialMigration --project Orleans
```

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