using Microsoft.EntityFrameworkCore;
using Common.Entities;
using Common.Integration;
using Common.Config;

namespace SellerMS.Infra
{
    public class SellerDbContext : DbContext
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
                                                            $"SUM(total_items - total_amount) as total_incentive, SUM(total_invoice) as total_invoice, SUM(total_items) as total_items " +
                                                            $"FROM public.order_entries " +
                                                            $"WHERE order_status = \'{OrderStatus.INVOICED}\' OR order_status = \'{OrderStatus.PAYMENT_PROCESSED}\' " +
                                                            $"OR order_status = \'{OrderStatus.READY_FOR_SHIPMENT}\' OR order_status = \'{OrderStatus.IN_TRANSIT}\' GROUP BY seller_id";

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

            modelBuilder.Entity<OrderEntry>()
               .ToTable("order_entries");

            modelBuilder.Entity<OrderEntry>(e =>
            {
                e.HasKey(oe => new { oe.customer_id, oe.order_id, oe.product_id });
                e.HasIndex(oe => new { oe.customer_id, oe.order_id }, "order_entry_idx");
                e.HasIndex(oe => oe.order_status, "order_entry_open_idx").HasFilter($"order_status = \'{OrderStatus.INVOICED}\' OR order_status = \'{OrderStatus.PAYMENT_PROCESSED}\' OR order_status = \'{OrderStatus.READY_FOR_SHIPMENT}\' OR order_status = \'{OrderStatus.IN_TRANSIT}\'");
                e.Property(e => e.order_status)
                       .HasConversion<string>();
                e.Property(e => e.delivery_status)
                       .HasConversion<string>();
            });

            modelBuilder.Entity<OrderSellerView>(e =>
            {
                e.HasNoKey();
                e.ToView("order_seller_view");
            });

        }
    }
}