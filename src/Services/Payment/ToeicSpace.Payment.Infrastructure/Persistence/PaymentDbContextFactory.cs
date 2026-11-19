using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ToeicSpace.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__PaymentDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__PaymentDatabase for EF tooling.");
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))).Options;
        return new PaymentDbContext(options);
    }
}
