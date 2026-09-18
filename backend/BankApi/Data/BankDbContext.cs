using BankApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApi.Data;

public class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Accounts)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId);

        modelBuilder.Entity<Account>()
            .HasIndex(a => a.Iban)
            .IsUnique();

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        // Données de démonstration (seed) pour tester rapidement l'API
        var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var accountId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<Customer>().HasData(new Customer
        {
            Id = customerId,
            FullName = "Ahmed Ben Salah",
            Email = "ahmed.bensalah@example.com",
            // Mot de passe de démonstration : "password123" (à changer si vous partagez le projet publiquement)
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<Account>().HasData(new Account
        {
            Id = accountId,
            Iban = "TN59 1000 6035 0000 9876 5432",
            Balance = 15230.50m,
            Currency = "TND",
            CustomerId = customerId,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
