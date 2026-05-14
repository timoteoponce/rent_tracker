using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentTracker.Web;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.Net;
using System.Net.Http.Headers;

namespace RentTracker.Tests;

/// <summary>
/// Factory that spins up the RentTracker application with an in-memory SQLite database.
/// All tests share this factory for performance; each test gets a fresh database transaction.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        // Open a shared in-memory connection. The connection must stay open for the lifetime of the factory.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RentTrackerDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory SQLite context
            services.AddDbContext<RentTrackerDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    /// <summary>
    /// Seeds the database with a default admin user and returns an authenticated HttpClient.
    /// Handles Razor Pages anti-forgery token automatically.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string role = UserRoles.Administrator)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Seed a user directly into the database
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var userId = Guid.NewGuid();
        var username = $"test-{role.ToLowerInvariant()}";
        var user = new User
        {
            Id = userId,
            Username = username,
            Email = $"{username}@test.ch",
            FullName = $"Test {role}",
            Role = role,
            PasswordHash = Web.Program.HashPassword("password123"),
            MustChangePassword = false,
            IsActive = true,
            IsSystemUser = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (!dbContext.Users.Any(u => u.Username == username))
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        // Razor Pages requires anti-forgery token. Fetch the login page first to get it.
        var loginPage = await client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(loginContent);

        // Login to get the auth cookie
        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.LoginIdentifier"] = username,
            ["Input.Password"] = "password123",
            ["Input.RememberMe"] = "false"
        }));

        if (loginResponse.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Login failed for test user. Status: {loginResponse.StatusCode}");
        }

        return client;
    }

    /// <summary>
    /// Extracts the Razor Pages anti-forgery token from HTML response.
    /// </summary>
    public static string ExtractAntiForgeryToken(string html)
    {
        // Simple string-based extraction for the __RequestVerificationToken hidden input
        const string tokenName = "__RequestVerificationToken";
        var tokenIndex = html.IndexOf($"name=\"{tokenName}\"", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex < 0)
        {
            // Try single-quoted variant
            tokenIndex = html.IndexOf($"name='{tokenName}'", StringComparison.OrdinalIgnoreCase);
        }

        if (tokenIndex < 0)
        {
            throw new InvalidOperationException("Anti-forgery token not found in login page HTML.");
        }

        // Find the value attribute after the token name
        var valueStart = html.IndexOf("value=\"", tokenIndex);
        if (valueStart < 0)
        {
            valueStart = html.IndexOf("value='", tokenIndex);
            if (valueStart < 0)
            {
                throw new InvalidOperationException("Anti-forgery token value not found.");
            }

            var valueEnd = html.IndexOf("'", valueStart + 7);
            return html.Substring(valueStart + 7, valueEnd - valueStart - 7);
        }

        var quoteEnd = html.IndexOf("\"", valueStart + 7);
        return html.Substring(valueStart + 7, quoteEnd - valueStart - 7);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}
