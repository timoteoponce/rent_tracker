using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.Net;
using Xunit;

namespace RentTracker.Tests.Characterization;

/// <summary>
/// Documents the "duplicate payment rows per lease + period" bug that the
/// auto-generated Pending feature exposed.
///
/// BEFORE the fix: <c>Pages/Payments/Edit.cshtml.cs</c> inserted a brand new
/// Payment row on almost every edit (only auto-generated Pending -> posted
/// updated in place). Two consecutive edits of one payment left THREE rows for
/// the same lease + period, all counted by lists, the dashboard, and the
/// revenue report.
///
/// AFTER the fix: edits update the row in place and snapshot the previous
/// values into PaymentAudits. The assertions below reflect the fixed behavior.
/// </summary>
public class PaymentEditDuplicateBugTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PaymentEditDuplicateBugTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    [Fact]
    public async Task TwoConsecutiveEdits_KeepOneRow_AndTwoAuditEntries()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        Guid leaseId;
        Guid paymentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var owner = db.Users.First(u => u.Username.StartsWith("test-"));
            var property = new Property
            {
                Name = Unique("DupProp"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var tenant = new User
            {
                Username = Unique("duptenant"),
                Email = $"{Unique("duptenant")}@test.ch",
                FullName = "Dup Tenant",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            db.Users.Add(tenant);
            await db.SaveChangesAsync();

            var lease = new Lease
            {
                PropertyId = property.Id,
                TenantId = tenant.Id,
                AgreedPrice = 1000m,
                AgreedWarranty = 2000m,
                StartDate = DateTimeOffset.UtcNow.AddMonths(-1),
                Status = LeaseStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();
            leaseId = lease.Id;

            var payment = new Payment
            {
                LeaseId = lease.Id,
                Amount = 1000m,
                Currency = "BOB",
                ForPeriod = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PaymentDate = DateTimeOffset.UtcNow,
                Status = PaymentStatus.Pending,
                Notes = "v0",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            paymentId = payment.Id;
        }

        await EditAsync(client, paymentId, leaseId, "1100", PaymentStatus.Pending, "v1");
        await EditAsync(client, paymentId, leaseId, "1000", PaymentStatus.Received, "v2");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();

            var payments = await db.Payments.Where(p => p.LeaseId == leaseId).ToListAsync();
            Assert.Single(payments);                              // was: 3 rows
            Assert.Equal(paymentId, payments[0].Id);
            Assert.Equal(1000m, payments[0].Amount);
            Assert.Equal(PaymentStatus.Received, payments[0].Status);
            Assert.Equal("v2", payments[0].Notes);

            var audits = await db.PaymentAudits
                .Where(a => a.PaymentId == paymentId)
                .ToListAsync();
            Assert.Equal(2, audits.Count);                        // one snapshot per edit
            Assert.Contains(audits, a => a.Notes == "v0");
            Assert.Contains(audits, a => a.Notes == "v1");
        }
    }

    private static async Task EditAsync(HttpClient client, Guid paymentId, Guid leaseId, string amount, string status, string notes)
    {
        var editPage = await client.GetAsync($"/Payments/Edit/{paymentId}");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());
        var period = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var response = await client.PostAsync($"/Payments/Edit/{paymentId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["OriginalPaymentId"] = paymentId.ToString(),
            ["Payment.LeaseId"] = leaseId.ToString(),
            ["Payment.ForPeriod"] = period.ToString("yyyy-MM"),
            ["Payment.Amount"] = amount,
            ["Payment.Currency"] = "BOB",
            ["Payment.Status"] = status,
            ["Payment.PaymentDate"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            ["Payment.Notes"] = notes
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
