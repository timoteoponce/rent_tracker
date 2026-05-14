using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;
using RentTracker.Web.Models;
using RentTracker.Web.Data;

namespace RentTracker.Tests.Integration;

public class LeaseTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LeaseTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<(Property Property, User Tenant, HttpClient Client)> SetupPropertyAndTenantAsync()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        await db.Database.EnsureCreatedAsync();

        var owner = db.Users.First(u => u.Username.StartsWith("test-"));

        var property = new Property
        {
            Name = Unique("LeaseProp"),
            CurrentPrice = 2000m,
            CurrentWarranty = 4000m,
            IsEnabled = true,
            LastEditedById = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Properties.Add(property);

        var tenant = new User
        {
            Username = Unique("tenant"),
            Email = $"{Unique("tenant")}@test.ch",
            FullName = "Test Tenant",
            Role = UserRoles.Tenant,
            PasswordHash = Web.Program.HashPassword("password123"),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(tenant);

        await db.SaveChangesAsync();

        return (property, tenant, client);
    }

    [Fact]
    public async Task CreateLease_ForWholeProperty_PersistsCorrectly()
    {
        var (property, tenant, client) = await SetupPropertyAndTenantAsync();

        var createPage = await client.GetAsync("/Leases/Create");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await createPage.Content.ReadAsStringAsync());

        var response = await client.PostAsync("/Leases/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Lease.PropertyId"] = property.Id.ToString(),
            ["Lease.TenantId"] = tenant.Id.ToString(),
            ["Lease.AgreedPrice"] = "2000",
            ["Lease.AgreedWarranty"] = "4000",
            ["Lease.StartDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.PropertyId == property.Id);
        Assert.NotNull(lease);
        Assert.Equal(tenant.Id, lease.TenantId);
        Assert.Equal(LeaseStatus.Active, lease.Status);
        Assert.Equal(2000m, lease.AgreedPrice);
    }

    [Fact]
    public async Task CloseLease_ChangesStatusToClosed()
    {
        var (property, tenant, client) = await SetupPropertyAndTenantAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var lease = new Lease
            {
                PropertyId = property.Id,
                TenantId = tenant.Id,
                AgreedPrice = 2000m,
                AgreedWarranty = 4000m,
                StartDate = DateTimeOffset.UtcNow.AddMonths(-6),
                Status = LeaseStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();

            var closePage = await client.GetAsync($"/Leases/Close/{lease.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await closePage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Leases/Close/{lease.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["EndDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var closed = await db.Leases.FindAsync(lease.Id);
            Assert.NotNull(closed);
            Assert.Equal(LeaseStatus.Closed, closed.Status);
            Assert.NotNull(closed.EndDate);
        }
    }

    [Fact]
    public async Task TerminateLease_ChangesStatusToTerminated()
    {
        var (property, tenant, client) = await SetupPropertyAndTenantAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var lease = new Lease
            {
                PropertyId = property.Id,
                TenantId = tenant.Id,
                AgreedPrice = 2000m,
                AgreedWarranty = 4000m,
                StartDate = DateTimeOffset.UtcNow.AddMonths(-3),
                Status = LeaseStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();

            var terminatePage = await client.GetAsync($"/Leases/Terminate/{lease.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await terminatePage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Leases/Terminate/{lease.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["TerminationReason"] = "Tenant broke contract",
                ["EndDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var terminated = await db.Leases.FindAsync(lease.Id);
            Assert.NotNull(terminated);
            Assert.Equal(LeaseStatus.Terminated, terminated.Status);
            Assert.Equal("Tenant broke contract", terminated.TerminationReason);
        }
    }

    [Fact]
    public async Task DetailsPage_RendersWithPayments()
    {
        var (property, tenant, client) = await SetupPropertyAndTenantAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
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

            var detailsPage = await client.GetAsync($"/Leases/Details/{lease.Id}");
            Assert.Equal(HttpStatusCode.OK, detailsPage.StatusCode);

            var content = await detailsPage.Content.ReadAsStringAsync();
            Assert.Contains(tenant.FullName, content);
            Assert.Contains("Bs. 2,000.00", content);
        }
    }

    [Fact]
    public async Task EditLease_UpdatesAgreedPrice()
    {
        var (property, tenant, client) = await SetupPropertyAndTenantAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            var lease = new Lease
            {
                PropertyId = property.Id,
                TenantId = tenant.Id,
                AgreedPrice = 2000m,
                AgreedWarranty = 4000m,
                StartDate = DateTimeOffset.UtcNow.AddMonths(-1),
                Status = LeaseStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();

            var editPage = await client.GetAsync($"/Leases/Edit/{lease.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Leases/Edit/{lease.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Lease.Id"] = lease.Id.ToString(),
                ["Lease.PropertyId"] = property.Id.ToString(),
                ["Lease.TenantId"] = tenant.Id.ToString(),
                ["Lease.AgreedPrice"] = "2500",
                ["Lease.AgreedWarranty"] = "5000",
                ["Lease.StartDate"] = lease.StartDate.ToString("yyyy-MM-dd"),
                ["Lease.EndDate"] = "",
                ["Lease.Status"] = LeaseStatus.Active
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var updated = await db.Leases.FindAsync(lease.Id);
            Assert.NotNull(updated);
            Assert.Equal(2500m, updated.AgreedPrice);
            Assert.Equal(5000m, updated.AgreedWarranty);
        }
    }
}
