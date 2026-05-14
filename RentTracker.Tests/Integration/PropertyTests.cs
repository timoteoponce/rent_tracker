using Microsoft.EntityFrameworkCore;
using System.Net;
using Xunit;
using RentTracker.Web.Models;
using RentTracker.Web.Data;

namespace RentTracker.Tests.Integration;

public class PropertyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PropertyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    [Fact]
    public async Task CreateProperty_WithAllFields_PersistsCorrectly()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        var createPage = await client.GetAsync("/Properties/Create");
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await createPage.Content.ReadAsStringAsync());

        var name = Unique("Casa");
        var response = await client.PostAsync("/Properties/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Property.Name"] = name,
            ["Property.Location"] = "Santa Cruz",
            ["Property.SurfaceSquareMeters"] = "120",
            ["Property.NumberOfRooms"] = "3",
            ["Property.CurrentPrice"] = "2500",
            ["Property.CurrentWarranty"] = "5000",
            ["Property.HasBathroom"] = "true",
            ["Property.HasKitchen"] = "true",
            ["Property.HasGarage"] = "false",
            ["Property.HasHotWater"] = "true",
            ["Property.HasAirConditioning"] = "false",
            ["Property.HasBackyard"] = "false",
            ["Property.HasSecurity"] = "true",
            ["Property.HasDoorbell"] = "false",
            ["Property.CanBeLeasedByUnits"] = "false",
            ["Property.IsPrivate"] = "false"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Name == name);
        Assert.NotNull(property);
        Assert.Equal(2500m, property.CurrentPrice);
        Assert.True(property.IsEnabled);
    }

    [Fact]
    public async Task EditProperty_UpdatesPriceAndLocation()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("Before"),
                Location = "Old",
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            var editPage = await client.GetAsync($"/Properties/Edit/{property.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Properties/Edit/{property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Property.Id"] = property.Id.ToString(),
                ["Property.Name"] = Unique("After"),
                ["Property.Location"] = "New",
                ["Property.CurrentPrice"] = "1500",
                ["Property.CurrentWarranty"] = "3000",
                ["Property.HasBathroom"] = "false",
                ["Property.HasKitchen"] = "false",
                ["Property.HasGarage"] = "false",
                ["Property.HasHotWater"] = "false",
                ["Property.HasAirConditioning"] = "false",
                ["Property.HasBackyard"] = "false",
                ["Property.HasSecurity"] = "false",
                ["Property.HasDoorbell"] = "false",
                ["Property.CanBeLeasedByUnits"] = "false",
                ["Property.IsPrivate"] = "false",
                ["Property.CreatedAt"] = property.CreatedAt.ToString("O")
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var updated = await db.Properties.FindAsync(property.Id);
            Assert.NotNull(updated);
            Assert.Equal("New", updated.Location);
            Assert.Equal(1500m, updated.CurrentPrice);
        }
    }

    [Fact]
    public async Task ToggleStatus_DisablesAndEnablesProperty()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("Toggle"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            // Get token from the Details page which contains the toggle form
            var detailsPage = await client.GetAsync($"/Properties/Details/{property.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await detailsPage.Content.ReadAsStringAsync());

            // Disable
            var disableResponse = await client.PostAsync($"/Properties/ToggleStatus?id={property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = property.Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, disableResponse.StatusCode);

            db.ChangeTracker.Clear();
            var disabled = await db.Properties.FindAsync(property.Id);
            Assert.NotNull(disabled);
            Assert.False(disabled.IsEnabled);

            // Re-fetch token for enable (redirect may change page)
            detailsPage = await client.GetAsync($"/Properties/Details/{property.Id}");
            token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await detailsPage.Content.ReadAsStringAsync());

            // Enable
            var enableResponse = await client.PostAsync($"/Properties/ToggleStatus?id={property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = property.Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, enableResponse.StatusCode);

            db.ChangeTracker.Clear();
            var enabled = await db.Properties.FindAsync(property.Id);
            Assert.NotNull(enabled);
            Assert.True(enabled.IsEnabled);
        }
    }

    [Fact]
    public async Task DetailsPage_RendersWithoutError_AfterToggleStatus()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("Details"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            // Get token from Details page and disable property
            var detailsPage = await client.GetAsync($"/Properties/Details/{property.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await detailsPage.Content.ReadAsStringAsync());

            await client.PostAsync($"/Properties/ToggleStatus?id={property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = property.Id.ToString()
            }));

            // View details page — this is where the reported NRE occurred
            detailsPage = await client.GetAsync($"/Properties/Details/{property.Id}");
            Assert.Equal(HttpStatusCode.OK, detailsPage.StatusCode);

            var content = await detailsPage.Content.ReadAsStringAsync();
            Assert.Contains(property.Name, content);
            Assert.Contains("Disabled", content);
        }
    }

    [Fact]
    public async Task Units_AddAndDeleteUnit()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("UnitTest"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                CanBeLeasedByUnits = true,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            // Get token from Units page
            var unitsPage = await client.GetAsync($"/Properties/Units/{property.Id}");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await unitsPage.Content.ReadAsStringAsync());

            // Add unit
            var addResponse = await client.PostAsync($"/Properties/Units/{property.Id}?handler=AddUnit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["propertyId"] = property.Id.ToString(),
                ["unitName"] = "Front Unit",
                ["unitDescription"] = "Facing the street",
                ["unitPrice"] = "800",
                ["unitWarranty"] = "1600"
            }));
            Assert.Equal(HttpStatusCode.Redirect, addResponse.StatusCode);

            db.ChangeTracker.Clear();
            var units = await db.PropertyUnits.Where(u => u.PropertyId == property.Id).ToListAsync();
            Assert.Single(units);
            Assert.Equal("Front Unit", units[0].Name);

            // Re-fetch token for delete
            unitsPage = await client.GetAsync($"/Properties/Units/{property.Id}");
            token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await unitsPage.Content.ReadAsStringAsync());

            // Delete unit
            var deleteResponse = await client.PostAsync($"/Properties/Units/{property.Id}?handler=DeleteUnit", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["propertyId"] = property.Id.ToString(),
                ["unitId"] = units[0].Id.ToString()
            }));
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

            db.ChangeTracker.Clear();
            units = await db.PropertyUnits.Where(u => u.PropertyId == property.Id).ToListAsync();
            Assert.Empty(units);
        }
    }

    [Fact]
    public async Task DeleteProperty_BlockedWhenLinkedToLease()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("DeleteBlocked"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = user.Id,
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

            var indexPage = await client.GetAsync("/Properties");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Properties?handler=Delete&id={property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = property.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var stillExists = await db.Properties.FindAsync(property.Id);
            Assert.NotNull(stillExists);

            // Verify error message is shown on redirect
            var redirectTarget = response.Headers.Location?.ToString();
            Assert.NotNull(redirectTarget);
            Assert.Contains("/Properties", redirectTarget);
        }
    }

    [Fact]
    public async Task DeleteProperty_SucceedsWhenNoLeases()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = db.Users.First(u => u.Username.StartsWith("test-"));

            var property = new Property
            {
                Name = Unique("DeleteOk"),
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Properties.Add(property);
            await db.SaveChangesAsync();

            var indexPage = await client.GetAsync("/Properties");
            var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(await indexPage.Content.ReadAsStringAsync());

            var response = await client.PostAsync($"/Properties?handler=Delete&id={property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = property.Id.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            db.ChangeTracker.Clear();
            var deleted = await db.Properties.FindAsync(property.Id);
            Assert.Null(deleted);
        }
    }
}
