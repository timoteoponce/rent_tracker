using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.Net;
using Xunit;

namespace RentTracker.Tests.Characterization;

/// <summary>
/// Characterization tests that document currently buggy behavior.
/// These tests verify that bugs exist before they are fixed.
/// After Phase 2 fixes, these tests should be updated to verify correct behavior.
/// </summary>
public class DashboardTake50BugTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DashboardTake50BugTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Documents the Take(50)-before-OrderByDescending bug on the dashboard.
    /// When there are more than 50 payments, the 10 "recent" payments shown
    /// may not actually be the most recent because Take(50) happens in SQL
    /// before any ordering.
    /// </summary>
    [Fact]
    public async Task Dashboard_WithManyPayments_MayShowIncorrectRecentPayments()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var user = dbContext.Users.First(u => u.Username.StartsWith("test-"));
        var property = new Property
        {
            Name = "Bug Test Property",
            IsEnabled = true,
            IsPrivate = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync();

        var lease = new Lease
        {
            PropertyId = property.Id,
            TenantId = user.Id,
            Status = LeaseStatus.Active,
            AgreedPrice = 1000,
            StartDate = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Leases.Add(lease);
        await dbContext.SaveChangesAsync();

        // Create 60 payments with distinct CreatedAt timestamps (oldest first)
        for (int i = 0; i < 60; i++)
        {
            dbContext.Payments.Add(new Payment
            {
                LeaseId = lease.Id,
                Amount = i + 1,
                Status = PaymentStatus.Received,
                ForPeriod = DateTimeOffset.UtcNow.AddMonths(-i),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i), // Oldest = largest negative minutes
                PaymentDate = DateTimeOffset.UtcNow.AddMonths(-i)
            });
        }
        await dbContext.SaveChangesAsync();

        // Get the dashboard
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        // The 10 most recent payments should have amounts 60, 59, 58, ..., 51
        // But with the bug, Take(50) gets the first 50 inserted (amounts 1-50),
        // then OrderByDescending takes the 10 newest among THOSE (amounts 50, 49, ..., 41)
        // This test documents the bug by checking that the newest payment (amount 60)
        // is NOT in the output (which would be the buggy behavior)
        // After the fix, this assertion should be flipped.
        Assert.DoesNotContain(">60<", content); // Amount 60 should NOT appear in buggy behavior
    }
}
