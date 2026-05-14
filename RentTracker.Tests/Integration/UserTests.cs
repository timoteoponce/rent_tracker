using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;
using RentTracker.Web.Models;
using RentTracker.Web.Data;

namespace RentTracker.Tests.Integration;

public class UserTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    [Fact]
    public async Task CreateUser_WithRole_PersistsCorrectly()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        var createPage = await client.GetAsync("/Users/Create");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await createPage.Content.ReadAsStringAsync());

        var username = Unique("newtenant");
        var response = await client.PostAsync("/Users/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["User.Username"] = username,
            ["User.Email"] = $"{username}@test.ch",
            ["User.FullName"] = "New Tenant",
            ["User.Role"] = UserRoles.Tenant
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        Assert.NotNull(user);
        Assert.Equal("New Tenant", user.FullName);
        Assert.Equal(UserRoles.Tenant, user.Role);
        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
    }

    [Fact]
    public async Task EditUser_UpdatesEmailAndFullName()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = Unique("editable"),
                Email = "old@test.ch",
                FullName = "Old Name",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var editPage = await client.GetAsync($"/Users/Edit/{user.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Users/Edit/{user.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["User.Id"] = user.Id.ToString(),
                ["User.Username"] = user.Username,
                ["User.Email"] = "new@test.ch",
                ["User.FullName"] = "New Name",
                ["User.Role"] = UserRoles.Owner
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var updated = await db.Users.FindAsync(user.Id);
            Assert.NotNull(updated);
            Assert.Equal("new@test.ch", updated.Email);
            Assert.Equal("New Name", updated.FullName);
            Assert.Equal(UserRoles.Owner, updated.Role);
        }
    }

    [Fact]
    public async Task ToggleStatus_DeactivatesAndReactivatesUser()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = Unique("toggleme"),
                Email = "toggle@test.ch",
                FullName = "Toggle User",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                IsSystemUser = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Get token from Users index page which contains the toggle form
            var indexPage = await client.GetAsync("/Users");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            // Deactivate
            var deactivateResponse = await client.PostAsync($"/Users/ToggleStatus?id={user.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = user.Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, deactivateResponse.StatusCode);

            db.ChangeTracker.Clear();
            var deactivated = await db.Users.FindAsync(user.Id);
            Assert.NotNull(deactivated);
            Assert.False(deactivated.IsActive);

            // Re-fetch token for reactivate
            indexPage = await client.GetAsync("/Users");
            token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            // Reactivate
            var activateResponse = await client.PostAsync($"/Users/ToggleStatus?id={user.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = user.Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, activateResponse.StatusCode);

            db.ChangeTracker.Clear();
            var activated = await db.Users.FindAsync(user.Id);
            Assert.NotNull(activated);
            Assert.True(activated.IsActive);
        }
    }

    [Fact]
    public async Task ResetPassword_SetsMustChangePasswordFlag()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = Unique("resetme"),
                Email = "reset@test.ch",
                FullName = "Reset User",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("oldpassword"),
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Get token from Users index page which contains the reset form
            var indexPage = await client.GetAsync("/Users");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Users/ResetPassword?id={user.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = user.Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var reset = await db.Users.FindAsync(user.Id);
            Assert.NotNull(reset);
            Assert.True(reset.MustChangePassword);
            Assert.Equal(Web.Program.HashPassword("password123"), reset.PasswordHash);
        }
    }

    [Fact]
    public async Task IndexPage_RendersWithoutNRE()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = Unique("index-test"),
                Email = "index@test.ch",
                FullName = "Index Test",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var indexPage = await client.GetAsync("/Users");
        Assert.Equal(HttpStatusCode.OK, indexPage.StatusCode);

        var content = await indexPage.Content.ReadAsStringAsync();
        Assert.Contains("User Management", content);
    }

    [Fact]
    public async Task DeleteUser_BlockedWhenLinkedToLease()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var tenant = new User
            {
                Username = Unique("linkedtenant"),
                Email = "linked@test.ch",
                FullName = "Linked Tenant",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(tenant);

            var owner = db.Users.First(u => u.Username.StartsWith("test-"));
            var property = new Property
            {
                Name = Unique("LinkedProp"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = owner.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            var lease = new Lease
            {
                PropertyId = property.Id,
                TenantId = tenant.Id,
                AgreedPrice = 1000m,
                AgreedWarranty = 2000m,
                StartDate = DateTimeOffset.UtcNow,
                Status = LeaseStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Leases.Add(lease);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Users");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Users?handler=Delete&id={tenant.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = tenant.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var stillExists = await db.Users.FindAsync(tenant.Id);
            Assert.NotNull(stillExists);
        }
    }

    [Fact]
    public async Task DeleteUser_SucceedsWhenNoLeases()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = Unique("orphanuser"),
                Email = "orphan@test.ch",
                FullName = "Orphan User",
                Role = UserRoles.Tenant,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Users");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Users?handler=Delete&id={user.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = user.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var deleted = await db.Users.FindAsync(user.Id);
            Assert.Null(deleted);
        }
    }

    [Fact]
    public async Task DeleteAdminUser_Blocked()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Administrator);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();

            // Seed an admin user
            var admin = new User
            {
                Username = Unique("adminuser"),
                Email = "admin@test.ch",
                FullName = "Test Admin",
                Role = UserRoles.Administrator,
                PasswordHash = Web.Program.HashPassword("password123"),
                IsActive = true,
                IsSystemUser = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Users");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Users?handler=Delete&id={admin.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = admin.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var stillExists = await db.Users.FindAsync(admin.Id);
            Assert.NotNull(stillExists);
        }
    }
}
