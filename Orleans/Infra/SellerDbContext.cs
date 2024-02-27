using Microsoft.EntityFrameworkCore;
using Common.Entities;
using Common.Integration;
using Common.Config;

namespace SellerMS.Infra
{
    public sealed class SellerDbContext : DbContext
    {

        public DbSet<OrderEntry> OrderEntries => Set<OrderEntry>();

        public DbSet<OrderSellerView> OrderSellerView => Set<OrderSellerView>();

        private readonly AppConfig configuration;

        public SellerDbContext(AppConfig configuration)
        {
            this.configuration = configuration;
        }

        public static readonly string RefreshMaterializedView = $"REFRESH MATERIALIZED VIEW CONCURRENTLY public.order_seller_view;";

        // the amount being transacted at the moment
        public static readonly string OrderSellerViewSql = $"CREATE MATERIALIZED VIEW IF NOT EXISTS public.order_seller_view " +
                                                            $"AS SELECT seller_id, COUNT(DISTINCT order_id) as count_orders, COUNT(product_id) as count_items, SUM(total_amount) as total_amount, SUM(freight_value) as total_freight, " +
                                                            $"SUM(total_items - total_amount) as total_incentive, SUM(total_invoice) as total_invoice, SUM(total_items) as total_items FROM public.order_entries GROUP BY seller_id";

        public static string CreateCustomOrderSellerViewSql(int sellerId)
        {
            return $"CREATE MATERIALIZED VIEW IF NOT EXISTS public.order_seller_view_{sellerId} " +
                                                            $"AS SELECT seller_id, COUNT(DISTINCT order_id) as count_orders, COUNT(product_id) as count_items, SUM(total_amount) as total_amount, SUM(freight_value) as total_freight, " +
                                                            $"SUM(total_items - total_amount) as total_incentive, SUM(total_invoice) as total_invoice, SUM(total_items) as total_items FROM public.order_entries WHERE seller_id = {sellerId} GROUP BY seller_id";
        }

        public static string GetRefreshCustomOrderSellerViewSql(int sellerId)
        {
            return $"REFRESH MATERIALIZED VIEW public.order_seller_view_{sellerId};";
        }

        public const string OrderSellerViewSqlIndex = $"CREATE UNIQUE INDEX IF NOT EXISTS order_seller_index ON public.order_seller_view (seller_id)";

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options
                .UseNpgsql(configuration.AdoNetConnectionString)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("public");

            modelBuilder.Entity<OrderEntry>().ToTable("order_entries");

            modelBuilder.Entity<OrderEntry>(e =>
            {
                e.HasKey(oe => oe.id);
                e.Property(e => e.id).ValueGeneratedOnAdd();

                e.HasIndex(oe =>  oe.seller_id, "seller_idx");
                // e.HasIndex(oe => new { oe.customer_id, oe.order_id }, "order_entry_idx");

                e.Property(e => e.order_status).HasConversion<string>();
                e.Property(e => e.delivery_status).HasConversion<string>();
            });

            modelBuilder.Entity<OrderSellerView>(e =>
            {
                e.HasNoKey();
                e.ToView("order_seller_view");
            });

        }
    }
}