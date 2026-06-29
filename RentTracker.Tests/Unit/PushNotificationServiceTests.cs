using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RentTracker.Web.Data;
using RentTracker.Web.Helpers;
using RentTracker.Web.Models;
using RentTracker.Web.Services;
using WebPush;
using Xunit;

namespace RentTracker.Tests.Unit;

public class PushNotificationServiceTests
{
    private RentTrackerDbContext GetInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<RentTrackerDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new RentTrackerDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        return context;
    }

    private static VapidKeys GetTestKeys()
    {
        var generated = VapidHelper.GenerateVapidKeys();
        return new VapidKeys(generated.PublicKey, generated.PrivateKey, "mailto:test@example.com");
    }

    [Fact]
    public void SubscribeAsync_StoresSubscription()
    {
        using var context = GetInMemoryContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Username = "pushuser",
            Email = "pushuser@test.ch",
            FullName = "Push User",
            Role = UserRoles.Owner,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new PushNotificationService(context, GetTestKeys(), NullLogger<PushNotificationService>.Instance);
        var dto = new PushSubscriptionDto
        {
            Endpoint = "https://push.example/endpoint/1",
            P256dh = "dGVzdHBhZGRpbmd0ZXN0cGFkZGluZw==",
            Auth = "dGVzdGF1dGg="
        };

        var result = service.SubscribeAsync(userId, dto).GetAwaiter().GetResult();

        Assert.True(result);
        Assert.Single(context.PushSubscriptions);
    }

    [Fact]
    public void SubscribeAsync_UpdatesExistingSubscription()
    {
        using var context = GetInMemoryContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Username = "pushuser",
            Email = "pushuser@test.ch",
            FullName = "Push User",
            Role = UserRoles.Owner,
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.PushSubscriptions.Add(new RentTracker.Web.Models.PushSubscription
        {
            UserId = userId,
            Endpoint = "https://push.example/endpoint/1",
            P256dh = "old",
            Auth = "old",
            CreatedAt = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        var service = new PushNotificationService(context, GetTestKeys(), NullLogger<PushNotificationService>.Instance);
        var dto = new PushSubscriptionDto
        {
            Endpoint = "https://push.example/endpoint/1",
            P256dh = "dGVzdHBhZGRpbmd0ZXN0cGFkZGluZw==",
            Auth = "dGVzdGF1dGg="
        };

        var result = service.SubscribeAsync(userId, dto).GetAwaiter().GetResult();

        Assert.True(result);
        Assert.Single(context.PushSubscriptions);
        Assert.Equal("dGVzdGF1dGg=", context.PushSubscriptions.First().Auth);
    }
}
