using Microsoft.EntityFrameworkCore;
using PizzaDeli.Models;

namespace PizzaDeli.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<Topping> Toppings { get; set; }
    public DbSet<ProductTopping> ProductToppings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite Keys
        modelBuilder.Entity<OrderDetail>()
            .HasKey(o => new { o.OrderId, o.ProductId });
            
        modelBuilder.Entity<ProductTopping>()
            .HasKey(pt => new { pt.ProductId, pt.ToppingId });
    }
}
