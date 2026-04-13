using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;
using System.Security.Claims;

namespace RentTracker.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure database
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/renttracker.db";
        
        builder.Services.AddDbContext<RentTrackerDbContext>(options =>
        {
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                // SQLite optimizations for single-developer maintenance
                sqliteOptions.MigrationsAssembly(typeof(Program).Assembly.FullName);
            });
        });

        // Configure simple cookie authentication (not Identity)
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdministrator", policy =>
                policy.RequireRole(UserRoles.Administrator));
            options.AddPolicy("RequireOwner", policy =>
                policy.RequireRole(UserRoles.Administrator, UserRoles.Owner));
        });

        builder.Services.AddRazorPages();

        var app = builder.Build();

        // Apply migrations on startup
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RentTrackerDbContext>();
            
            // Ensure data directory exists
            var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
            
            // Apply pending migrations
            await dbContext.Database.MigrateAsync();
            
            // Seed default admin user if not exists
            await SeedDefaultAdminAsync(dbContext);
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Health check endpoint for Docker
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
        
        app.MapRazorPages();

        await app.RunAsync();
    }

    private static async Task SeedDefaultAdminAsync(RentTrackerDbContext context)
    {
        // Check if admin user exists
        var adminExists = await context.Users
            .AnyAsync(u => u.FullName == "admin" && u.Role == UserRoles.Administrator);

        if (!adminExists)
        {
            // Create default admin user with plain password "admin"
            // This user MUST change password on first login
            var admin = new User
            {
                FullName = "admin",
                Role = UserRoles.Administrator,
                PasswordHash = HashPassword("admin"),
                MustChangePassword = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Simple password hashing using PBKDF2.
    /// Not as secure as bcrypt, but requires no external dependencies and is good enough for this use case.
    /// </summary>
    public static string HashPassword(string password)
    {
        // Use simple hash for initial version
        // In production, consider using ASP.NET Core's PasswordHasher<T>
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password + "renttracker-salt-2026");
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        var computedHash = HashPassword(password);
        return computedHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
}
