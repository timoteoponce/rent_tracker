using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;
using RentTracker.Web.Models;
using RentTracker.Web.Data;

namespace RentTracker.Tests.Integration;

public class PaymentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<(Property Property, User Tenant, Lease Lease, HttpClient Client)> SetupAsync()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        await db.Database.EnsureCreatedAsync();

        var owner = db.Users.First(u => u.Username.StartsWith("test-"));

        var property = new Property
        {
            Name = Unique("PaymentProp"),
            CurrentPrice = 2000m,
            CurrentWarranty = 4000m,
            IsEnabled = true,
            LastEditedById = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Properties.Add(property);

        var tenant = new User
        {
            Username = Unique("paytenant"),
            Email = $"{Unique("paytenant")}@test.ch",
            FullName = "Payment Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = Web.Program.HashPassword("password123"),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(tenant);
        await db.SaveChangesAsync();

        var lease = new Lease
        {
            PropertyId = property.Id,
            TenantId = tenant.Id,
            AgreedPrice = 2000m,
            AgreedWarranty = 4000m,
            StartDate = DateTimeOffset.UtcNow.AddMonths(-2),
            Status = LeaseStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Leases.Add(lease);
        await db.SaveChangesAsync();

        return (property, tenant, lease, client);
    }

    [Fact]
    public async Task CreatePayment_ForLease_PersistsCorrectly()
    {
        var (property, tenant, lease, client) = await SetupAsync();

        var createPage = await client.GetAsync("/Payments/Create");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await createPage.Content.ReadAsStringAsync());

        var period = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var response = await client.PostAsync("/Payments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Payment.LeaseId"] = lease.Id.ToString(),
            ["Payment.ForPeriod"] = period.ToString("yyyy-MM"),
            ["Payment.PaymentDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            ["Payment.Amount"] = "2000",
            ["Payment.Currency"] = "BOB",
            ["Payment.Status"] = PaymentStatus.Received,
            ["Payment.Notes"] = "First month rent"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.LeaseId == lease.Id);
        Assert.NotNull(payment);
        Assert.Equal(2000m, payment.Amount);
        Assert.Equal("BOB", payment.Currency);
        Assert.Equal(PaymentStatus.Received, payment.Status);
        Assert.Equal("First month rent", payment.Notes);
    }

    [Fact]
    public async Task EditPayment_CreatesNewRecordAndPreservesOriginal()
    {
        var (property, tenant, lease, client) = await SetupAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var original = new Payment
            {
                LeaseId = lease.Id,
                Amount = 2000m,
                Currency = "BOB",
                ForPeriod = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PaymentDate = DateTimeOffset.UtcNow.AddDays(-5),
                Status = PaymentStatus.Pending,
                Notes = "Original",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(original);
            await db.SaveChangesAsync();

            var editPage = await client.GetAsync($"/Payments/Edit/{original.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Payments/Edit/{original.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["OriginalPaymentId"] = original.Id.ToString(),
                ["Payment.LeaseId"] = lease.Id.ToString(),
                ["Payment.ForPeriod"] = original.ForPeriod.ToString("yyyy-MM"),
                ["Payment.Amount"] = "2500",
                ["Payment.Currency"] = "BOB",
                ["Payment.Status"] = PaymentStatus.Received,
                ["Payment.PaymentDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                ["Payment.Notes"] = "Updated amount"
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var payments = await db.Payments.Where(p => p.LeaseId == lease.Id).ToListAsync();
            Assert.Equal(2, payments.Count);

            var updated = payments.FirstOrDefault(p => p.PreviousPaymentId == original.Id);
            Assert.NotNull(updated);
            Assert.Equal(2500m, updated.Amount);
            Assert.Equal(PaymentStatus.Received, updated.Status);
            Assert.Equal("Updated amount", updated.Notes);
        }
    }

    [Fact]
    public async Task IndexPage_RendersPaymentsWithoutNRE()
    {
        var (property, tenant, lease, client) = await SetupAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var payment = new Payment
            {
                LeaseId = lease.Id,
                Amount = 2000m,
                Currency = "BOB",
                ForPeriod = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PaymentDate = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Received,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Payments");
            Assert.Equal(HttpStatusCode.OK, indexPage.StatusCode);

            var content = await indexPage.Content.ReadAsStringAsync();
            Assert.Contains("Payment Tenant", content);
            Assert.Contains("Bs. 2,000.00", content);
        }
    }

    [Fact]
    public async Task DeletePayment_Succeeds()
    {
        var (property, tenant, lease, client) = await SetupAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var payment = new Payment
            {
                LeaseId = lease.Id,
                Amount = 2000m,
                Currency = "BOB",
                ForPeriod = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PaymentDate = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Received,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Payments");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Payments?handler=Delete&id={payment.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = payment.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var deleted = await db.Payments.FindAsync(payment.Id);
            Assert.Null(deleted);
        }
    }
}
