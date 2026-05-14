using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using RentTracker.Web.Models;
using RentTracker.Web.Data;

namespace RentTracker.Tests.Integration;

public class PropertyRoundtripTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PropertyRoundtripTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateProperty_WithAllFields_PersistsCorrectly()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        // Fetch create page to get anti-forgery token
        var createPage = await client.GetAsync("/Properties/Create");
        var createPageContent = await createPage.Content.ReadAsStringAsync();
        var token = CustomWebApplicationFactory.ExtractAntiForgeryToken(createPageContent);

        // Create a property
        var createResponse = await client.PostAsync("/Properties/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Property.Name"] = "Test Property",
            ["Property.Location"] = "Test Location",
            ["Property.SurfaceSquareMeters"] = "120.50",
            ["Property.NumberOfRooms"] = "3",
            ["Property.CurrentPrice"] = "2500.00",
            ["Property.CurrentWarranty"] = "5000.00",
            ["Property.HasBathroom"] = "true",
            ["Property.HasKitchen"] = "true",
            ["Property.HasGarage"] = "false",
            ["Property.HasHotWater"] = "true",
            ["Property.HasAirConditioning"] = "false",
            ["Property.HasBackyard"] = "false",
            ["Property.HasSecurity"] = "true",
            ["Property.HasDoorbell"] = "false",
            ["Property.CanBeLeasedByUnits"] = "false",
            ["Property.IsPrivate"] = "true"
        }));

        // Should redirect after creation
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        // Verify in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        var property = await dbContext.Properties.FirstOrDefaultAsync(p => p.Name == "Test Property");

        Assert.NotNull(property);
        Assert.Equal("Test Location", property.Location);
        Assert.Equal(120.50, property.SurfaceSquareMeters);
        Assert.Equal(3, property.NumberOfRooms);
        Assert.Equal(2500.00m, property.CurrentPrice);
        Assert.Equal(5000.00m, property.CurrentWarranty);
        Assert.True(property.HasBathroom);
        Assert.True(property.HasKitchen);
        Assert.False(property.HasGarage);
        Assert.True(property.HasHotWater);
        Assert.False(property.HasAirConditioning);
        Assert.False(property.HasBackyard);
        Assert.True(property.HasSecurity);
        Assert.False(property.HasDoorbell);
        Assert.False(property.CanBeLeasedByUnits);
        Assert.True(property.IsPrivate);
    }

    [Fact]
    public async Task EditProperty_UpdatesAllFieldsWithoutSilentLoss()
    {
        var client = await _factory.CreateAuthenticatedClientAsync(UserRoles.Owner);

        // Seed a property
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var user = dbContext.Users.First(u => u.Username.StartsWith("test-"));
            var property = new Property
            {
                Name = "Original Name",
                Location = "Original Location",
                SurfaceSquareMeters = 100,
                NumberOfRooms = 2,
                CurrentPrice = 1000m,
                CurrentWarranty = 2000m,
                HasBathroom = false,
                HasKitchen = false,
                HasGarage = false,
                HasHotWater = false,
                HasAirConditioning = false,
                HasBackyard = false,
                HasSecurity = false,
                HasDoorbell = false,
                CanBeLeasedByUnits = false,
                IsPrivate = false,
                IsEnabled = true,
                LastEditedById = user.Id,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Properties.Add(property);
            await dbContext.SaveChangesAsync();

            // Fetch edit page to get anti-forgery token
            var editPage = await client.GetAsync($"/Properties/Edit/{property.Id}");
            var editPageContent = await editPage.Content.ReadAsStringAsync();
            var editToken = CustomWebApplicationFactory.ExtractAntiForgeryToken(editPageContent);

            // Edit the property
            var editResponse = await client.PostAsync($"/Properties/Edit/{property.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = editToken,
                ["Property.Id"] = property.Id.ToString(),
                ["Property.Name"] = "Updated Name",
                ["Property.Location"] = "Updated Location",
                ["Property.SurfaceSquareMeters"] = "150.00",
                ["Property.NumberOfRooms"] = "4",
                ["Property.CurrentPrice"] = "3000.00",
                ["Property.CurrentWarranty"] = "6000.00",
                ["Property.HasBathroom"] = "true",
                ["Property.HasKitchen"] = "true",
                ["Property.HasGarage"] = "true",
                ["Property.HasHotWater"] = "true",
                ["Property.HasAirConditioning"] = "true",
                ["Property.HasBackyard"] = "true",
                ["Property.HasSecurity"] = "true",
                ["Property.HasDoorbell"] = "true",
                ["Property.CanBeLeasedByUnits"] = "true",
                ["Property.IsPrivate"] = "true",
                ["Property.CreatedAt"] = property.CreatedAt.ToString("O")
            }));

            Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

            // Reload from a fresh context to avoid change tracking returning stale values
            dbContext.ChangeTracker.Clear();
            var updatedProperty = await dbContext.Properties.FindAsync(property.Id);
            Assert.NotNull(updatedProperty);
            Assert.Equal("Updated Name", updatedProperty.Name);
            Assert.Equal("Updated Location", updatedProperty.Location);
            Assert.Equal(150.00, updatedProperty.SurfaceSquareMeters);
            Assert.Equal(4, updatedProperty.NumberOfRooms);
            Assert.Equal(3000.00m, updatedProperty.CurrentPrice);
            Assert.Equal(6000.00m, updatedProperty.CurrentWarranty);
            Assert.True(updatedProperty.HasBathroom);
            Assert.True(updatedProperty.HasKitchen);
            Assert.True(updatedProperty.HasGarage);
            Assert.True(updatedProperty.HasHotWater);
            Assert.True(updatedProperty.HasAirConditioning);
            Assert.True(updatedProperty.HasBackyard);
            Assert.True(updatedProperty.HasSecurity);
            Assert.True(updatedProperty.HasDoorbell);
            Assert.True(updatedProperty.CanBeLeasedByUnits);
            Assert.True(updatedProperty.IsPrivate);
        }
    }
}
