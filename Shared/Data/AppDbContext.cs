using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Shared.Models.AuthUser;
using Shared.Models.StockSales;

namespace Shared.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets de StockService
        public DbSet<Product> Products => Set<Product>();
        public DbSet<StockHistory> StockHistories => Set<StockHistory>();

        // DbSets de SalesService
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        public DbSet<UserModel> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // Strings
                var stringProperties = entityType.ClrType
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(string));

                foreach (var prop in stringProperties)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(prop.Name)
                        .HasMaxLength(200);
                }

                // Decimais, ignorando [NotMapped]
                var decimalProperties = entityType.ClrType
                    .GetProperties()
                    .Where(p =>
                        (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)) &&
                        !Attribute.IsDefined(p, typeof(NotMappedAttribute))
                    );

                foreach (var prop in decimalProperties)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(prop.Name)
                        .HasPrecision(18, 2);
                }
            }

            ConfigureOrderModel(modelBuilder);
            ConfigureOrderItemModel(modelBuilder);
            ConfigureProductModel(modelBuilder);
        }

        private static void ConfigureOrderModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderDate)
                      .HasDefaultValueSql("GETUTCDATE()")
                      .ValueGeneratedOnAdd();
                entity.Property(e => e.TotalAmount)
                      .HasColumnType("decimal(18,2)");
            });
        }

        private static void ConfigureOrderItemModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice)
                      .HasColumnType("decimal(18,2)");
                entity.Ignore(e => e.TotalPrice); // Não mapeia TotalPrice

                entity.HasOne(e => e.Order)
                      .WithMany() // relação 1:N sem coleção em Order
                      .HasForeignKey(e => e.OrderId);
            });
        }

        private static void ConfigureProductModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price)
                      .HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockQuantity)
                      .HasDefaultValue(0);
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await ProcessStockChanges();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private async Task ProcessStockChanges()
        {
            var entries = ChangeTracker.Entries<Product>()
                .Where(e => e.State == EntityState.Modified &&
                            e.Property(p => p.StockQuantity).IsModified);

            foreach (var entry in entries)
            {
                var originalQuantity = entry.OriginalValues.GetValue<int>(nameof(Product.StockQuantity));
                var currentQuantity = entry.CurrentValues.GetValue<int>(nameof(Product.StockQuantity));

                if (originalQuantity != currentQuantity)
                {
                    var history = new StockHistory(entry.Entity.Id, "system")
                    {
                        ProductId = entry.Entity.Id,
                        OldQuantity = originalQuantity,
                        NewQuantity = currentQuantity,
                        ChangedAt = DateTime.UtcNow
                    };

                    await StockHistories.AddAsync(history);
                }
            }
        }
    }
}
