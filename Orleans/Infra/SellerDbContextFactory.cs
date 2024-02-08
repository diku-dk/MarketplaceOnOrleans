using Microsoft.EntityFrameworkCore.Design;
using SellerMS.Infra;
using Common.Config;

namespace Orleans.Infra;

/**
 * Necessary to create a migration. Guess because it is not startup project. See more details in:
 * https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory
 */
public class BloggingContextFactory : IDesignTimeDbContextFactory<SellerDbContext>
{
    public SellerDbContext CreateDbContext(string[] args)
    {
        AppConfig config = new AppConfig();
        // this is not used to create the migration, so any string would work
        config.AdoNetConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10000";

        return new SellerDbContext(config);
    }
}

