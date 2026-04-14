# AGENTS.md - RentTracker Maintenance Guide

> This file provides guidance for AI agents and future maintainers working on the RentTracker application.

## Project Overview

RentTracker is a web application for managing rental properties, leases, and payments.
Built for single-developer maintenance over 5-10 years with minimal dependencies.

**Core Tech Stack:**
- .NET 9 LTS (Razor Pages) - single project at root
- SQLite (supported until 2050)
- Entity Framework Core
- Vanilla CSS (no frameworks)
- Vanilla JavaScript + Chart.js (via LibMan)
- Simple Cookie Authentication (custom, not Identity)

## Architecture Principles

### 1. Simplicity First
- Single project at repository root (no solution file, no src/ folder)
- No complex patterns (Repository, Unit of Work, CQRS)
- Direct EF Core usage is fine for this scale
- Code should be readable by someone who hasn't touched it in 3 years

### 2. Minimal Dependencies
- Only Microsoft packages
- Chart.js is the only JS library (managed via LibMan)
- No npm, no webpack, no build pipeline
- Every dependency is a future liability

### 3. Explicit Over Clever
- Use clear, verbose variable names
- Avoid LINQ chains that require mental unpacking
- Prefer straightforward if/else over ternary operators
- Comments explain WHY, not WHAT

### 4. Longevity Considerations
- All dates use `DateTimeOffset` (timezone-safe)
- Money uses `decimal` (never float/double)
- IDs use `Guid` (no identity column issues)
- File paths use `Path.Combine` (cross-platform)

## Project Structure

```
RentTracker/                     # Repository root = project root
├── RentTracker.csproj           # Single project file
├── Program.cs                   # App entry point
├── appsettings.json             # Production config
├── appsettings.Development.json # Development config
├── libman.json                  # Client-side libraries
├── mise.toml                    # Tool version management
├── Dockerfile                   # Container build
├── docker-compose.yml           # Local deployment
├── AGENTS.md                    # This file
│
├── Models/                      # Domain entities
│   ├── User.cs
│   ├── Property.cs
│   ├── PropertyUnit.cs
│   ├── Lease.cs
│   ├── Payment.cs
│   └── PropertyPriceHistory.cs
│
├── Data/                        # Database context, migrations
│   ├── RentTrackerDbContext.cs
│   └── Migrations/
│
├── Pages/                       # Razor Pages
│   ├── _ViewImports.cshtml
│   ├── _ViewStart.cshtml
│   ├── Index.cshtml             # Dashboard
│   ├── Error.cshtml
│   ├── Account/                 # Login, logout, change password
│   │   ├── Login.cshtml
│   │   ├── Login.cshtml.cs
│   │   ├── Logout.cshtml.cs
│   │   └── ChangePassword.cshtml
│   ├── Properties/              # Property CRUD
│   ├── Leases/                  # Lease management
│   ├── Payments/                # Payment tracking
│   └── Reports/                 # Dashboard, charts
│
├── wwwroot/                     # Static files
│   ├── css/                    # Vanilla CSS
│   │   ├── site.css            # Main styles
│   │   └── auth.css            # Auth page styles
│   ├── js/                     # Minimal vanilla JS
│   │   └── site.js
│   └── lib/                    # LibMan libraries
│       ├── chartjs/
│       ├── jquery/
│       ├── jquery-validate/
│       └── jquery-validation-unobtrusive/
│
└── .vscode/                     # VS Code configuration
    ├── settings.json
    ├── launch.json
    └── tasks.json
```

## Development Workflow (VS Code)

### Common Commands (No --project flag needed!)

```bash
# Restore packages
dotnet restore
libman restore

# Build
dotnet build

# Run with hot reload
dotnet watch run

# Run without watch
dotnet run

# Database migrations
dotnet ef migrations add MigrationName
dotnet ef database update

# Docker
docker-compose up -d
```

### VS Code Tasks (Ctrl+Shift+P → Tasks: Run Task)

- **build**: Build the project
- **restore**: Restore NuGet packages
- **restore-libman**: Restore LibMan libraries  
- **run**: Run the application
- **watch**: Run with hot reload
- **ef-migrations-add**: Add a migration
- **ef-database-update**: Apply migrations
- **docker-build**: Build Docker image
- **docker-compose-up**: Run with Docker Compose

### VS Code Debugging (F5)

Press F5 to launch with debugging. Configuration is in `.vscode/launch.json`.

## Development Guidelines

### Adding a New Feature

1. **Database Changes:**
   - Add/modify entity in `Models/`
   - Add migration: `dotnet ef migrations add FeatureName`
   - Test locally: `dotnet ef database update`

2. **Pages:**
   - Create folder in `Pages/FeatureName/`
   - Use `PageModel` with `OnGetAsync` / `OnPostAsync`
   - Validate input with Data Annotations
   - Return `Page()` or `RedirectToPage()`

3. **Styling:**
   - Add styles to `wwwroot/css/site.css`
   - Use CSS custom properties for colors
   - Mobile-first responsive design
   - No !important, no inline styles

4. **JavaScript:**
   - Keep it minimal
   - Use vanilla JS (no frameworks)
   - Place in `wwwroot/js/` and include in page

### Authentication System

We use simple cookie authentication (NOT ASP.NET Identity):

```csharp
// In Program.cs
builder.Services.AddAuthentication(...).AddCookie(...);

// Protect pages with [Authorize]
// Check roles with User.IsInRole("Administrator")
// Default admin: admin/admin (must change on first login)
```

**Why not Identity?**
- Adds 10+ tables we don't need
- Requires email for password reset
- More complex to maintain long-term
- Our simple version is 50 lines, fully understood

### Database Patterns

```csharp
// Good: Direct EF Core usage
public async Task<IActionResult> OnPostAsync()
{
    _context.Properties.Add(Property);
    await _context.SaveChangesAsync();
    return RedirectToPage("./Index");
}

// Good: Include related data when needed
var property = await _context.Properties
    .Include(p => p.Leases)
    .ThenInclude(l => l.Tenant)
    .FirstOrDefaultAsync(p => p.Id == id);

// Bad: Repository pattern (unnecessary complexity)
// Bad: Unit of Work (EF Core already handles this)
```

### CSS Guidelines

```css
/* Use CSS custom properties for consistency */
:root {
    --primary-color: #2563eb;
    --danger-color: #dc2626;
    --success-color: #16a34a;
    --text-color: #1f2937;
    --bg-color: #f9fafb;
}

/* Mobile-first responsive design */
.container {
    padding: 1rem;
}

@media (min-width: 768px) {
    .container {
        padding: 2rem;
        max-width: 1200px;
        margin: 0 auto;
    }
}

/* Utility classes for common patterns */
.btn { /* base button styles */ }
.btn-primary { background: var(--primary-color); }
.btn-danger { background: var(--danger-color); }
```

## Maintenance Checklists

### .NET LTS Upgrade (2026, 2028, 2031...)

```bash
# 1. Check current EOL dates
# https://dotnet.microsoft.com/en-us/platform/support/policy

# 2. Update mise.toml with new SDK version
# [tools]
# dotnet = "NEW_VERSION"

# 3. Update package references in RentTracker.csproj
# Change all 9.x to NEW_VERSION

# 4. Update Dockerfile base images
# FROM mcr.microsoft.com/dotnet/sdk:NEW_VERSION
# FROM mcr.microsoft.com/dotnet/aspnet:NEW_VERSION

# 5. Build and test
dotnet build
dotnet test  # If we have tests

# 6. Check for breaking changes
# https://learn.microsoft.com/en-us/dotnet/core/compatibility/

# 7. Update AGENTS.md with new EOL date
```

### Yearly SQLite Maintenance

```bash
# 1. Backup database (before any changes)
cp data/renttracker.db data/renttracker-backup-$(date +%Y%m%d).db

# 2. Optional: Run VACUUM for optimization
# sqlite3 data/renttracker.db "VACUUM;"

# 3. Check for corruption
# sqlite3 data/renttracker.db "PRAGMA integrity_check;"

# 4. Verify backup can be restored
# cp data/renttracker-backup-YYYYMMDD.db test-restore.db
# sqlite3 test-restore.db ".tables"
# rm test-restore.db
```

### Security Review (Quarterly)

1. Check for EF Core SQL injection risks (use parameterized queries)
2. Verify cookie security settings (HttpOnly, Secure, SameSite)
3. Review user input validation (all inputs should be validated)
4. Check file upload restrictions (if applicable)
5. Review role-based access (no hardcoded role checks in views)

## Common Tasks

### Adding a New Page

```bash
# 1. Create folder
mkdir -p Pages/NewFeature

# 2. Create page files
touch Pages/NewFeature/Index.cshtml
touch Pages/NewFeature/Index.cshtml.cs
touch Pages/NewFeature/Create.cshtml
touch Pages/NewFeature/Create.cshtml.cs

# 3. Add to navigation in _Layout.cshtml

# 4. Add styles to site.css
```

### Adding a Database Migration

```bash
# 1. Make model changes

# 2. Create migration (no --project flag needed!)
dotnet ef migrations add MigrationName

# 3. Review generated migration file in Data/Migrations/

# 4. Apply locally
dotnet ef database update

# 5. Test the changes
```

### Updating Chart.js

```bash
# 1. Check latest version on cdnjs.com

# 2. Update libman.json
# {
#   "library": "Chart.js@NEW_VERSION",
#   "destination": "wwwroot/lib/chartjs/"
# }

# 3. Restore libraries
libman restore

# 4. Test charts still work
```

## Troubleshooting

### "Database locked" error
- Check if another process has the DB open
- SQLite doesn't support concurrent writes well
- Consider using WAL mode (already configured)

### Migrations fail
- Ensure no pending model changes
- Check migration history in __EFMigrationsHistory table
- May need to manually fix if deployment is stuck

### LibMan restore fails
- Check internet connection
- Try different provider (cdnjs, jsdelivr, unpkg)
- Verify library name and version exist

### Docker build fails
- Check .dockerignore includes bin/, obj/, data/
- Ensure Dockerfile uses correct .NET version
- Verify libman.json is copied before restore

### VS Code build fails
- Check that .vscode/tasks.json doesn't have stale paths
- Ensure dotnet SDK version matches mise.toml
- Try: Ctrl+Shift+P → ".NET: Restart OmniSharp"

### "SQLite does not support DateTimeOffset in ORDER BY" error
SQLite doesn't support ordering by DateTimeOffset columns directly. 
**Solution:** Fetch data with `.ToList()` first, then use LINQ to Objects for ordering:
```csharp
// BAD - will throw exception:
var data = await _context.Payments.OrderBy(p => p.CreatedAt).ToListAsync();

// GOOD - fetch then sort in memory:
var data = await _context.Payments.Take(100).ToListAsync();
var sorted = data.OrderBy(p => p.CreatedAt).ToList();
```

### "SQLite does not support DateTimeOffset.Year/Month in WHERE" error
SQLite doesn't support accessing `.Year`, `.Month`, `.Day` properties on DateTimeOffset in LINQ WHERE clauses.
**Solution:** Filter client-side after fetching data:
```csharp
// BAD - will throw exception:
var payments = await _context.Payments
    .Where(p => p.ForPeriod.Year == 2024 && p.ForPeriod.Month == 6)
    .ToListAsync();

// GOOD - filter in memory:
var allPayments = await _context.Payments.ToListAsync();
var payments = allPayments
    .Where(p => p.ForPeriod.Year == 2024 && p.ForPeriod.Month == 6)
    .ToList();
```

### "The LINQ expression could not be translated. DateTimeOffset comparison" error
SQLite doesn't support comparing DateTimeOffset values directly in WHERE clauses (e.g., `>=`, `<=`, `==`).
**Solution:** Use `.AsEnumerable()` to force client-side evaluation before filtering:
```csharp
// BAD - will throw exception:
var payments = await _context.Payments
    .Where(p => p.ForPeriod >= startDate && p.ForPeriod <= endDate)
    .ToListAsync();

// GOOD - fetch then filter in memory:
var payments = _context.Payments
    .Include(p => p.Lease)
    .AsEnumerable()  // Switch to client-side before DateTimeOffset comparison
    .Where(p => p.ForPeriod >= startDate && p.ForPeriod <= endDate)
    .ToList();
```

## Important Decisions Log

| Date | Decision | Reason |
|------|----------|--------|
| 2026-04-13 | SQLite over PostgreSQL/MSSQL | Zero maintenance, supported until 2050 |
| 2026-04-13 | Single project at root (no solution) | Maximum simplicity for single developer |
| 2026-04-13 | Simple cookie auth vs Identity | No need for password reset/email, simpler code |
| 2026-04-13 | Razor Pages vs Blazor | Simpler mental model, no SignalR dependency |
| 2026-04-13 | LibMan vs npm | No Node.js required, simpler toolchain |
| 2026-04-13 | Chart.js vs SVG charts | Need some interactivity, LibMan manages it |
| 2026-04-13 | Skip tests for now | Single developer, can add later if needed |
| 2026-04-13 | Vanilla CSS vs Bootstrap | No dependency, CSS is now capable enough |
| 2026-04-13 | VS Code as primary dev environment | Simple, cross-platform, good .NET support |

## Resources

- .NET 9 LTS EOL: May 2026
- Next LTS (.NET 10): November 2028 (plan upgrade in 2026)
- SQLite Support: Until 2050
- Chart.js: https://www.chartjs.org/docs/
- EF Core: https://learn.microsoft.com/en-us/ef/core/
- Razor Pages: https://learn.microsoft.com/en-us/aspnet/core/razor-pages/
- VS Code C# Dev Kit: https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit

## For AI Agents: How to Build and Extend This Application

> This section provides explicit instructions for AI assistants (Kimi, Copilot, Claude, etc.) working on this codebase. It includes templates, patterns, and step-by-step guides.

### AI Quick Reference

**When asked to add a feature, follow this checklist:**
1. ✅ Does it need database changes? → Update Models/ → Create migration
2. ✅ Does it need UI? → Create Pages/FeatureName/ folder with Index.cshtml
3. ✅ Does it need authorization? → Add [Authorize] or [Authorize(Roles = "...")]
4. ✅ Does it use DateTimeOffset? → Remember SQLite limitations (see below)
5. ✅ Did you add navigation link? → Update _Layout.cshtml
6. ✅ Did you run `dotnet build` to verify?

**Critical SQLite Rules (Will Break if Ignored):**
- ❌ Cannot use `.OrderBy(p => p.DateProperty)` on DateTimeOffset
- ❌ Cannot use `.Where(p => p.Date.Year == 2024)` on DateTimeOffset
- ❌ Cannot use `.Where(p => p.Date.Month == 6)` on DateTimeOffset
- ✅ Solution: Fetch with `.ToListAsync()` first, then use LINQ to Objects

---

### Architecture Overview for AI

**This is NOT MVC. This is Razor Pages.**

```
MVC Pattern:                    Razor Pages Pattern:
Controllers/                    Pages/
  ├── PropertiesController.cs     ├── Properties/
  └── Index()                      ├── Index.cshtml     ← View
Views/                              └── Index.cshtml.cs ← Controller
  ├── Properties/                   ├── Create.cshtml
  │   └── Index.cshtml              └── Create.cshtml.cs
```

**Key Differences:**
- No `Controllers/` folder
- No `Views/` folder
- Logic lives in `.cshtml.cs` files (PageModels)
- Routing is automatic based on folder structure
- Each page has its own URL (e.g., `/Properties/Create`)

---

### Template: Adding a New CRUD Feature

Use this template when asked to create a new feature (e.g., "Add maintenance requests"):

#### Step 1: Create the Model

```csharp
// Models/MaintenanceRequest.cs
using System.ComponentModel.DataAnnotations;

namespace RentTracker.Web.Models;

public class MaintenanceRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public string Status { get; set; } = MaintenanceStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    // Foreign keys
    public Guid PropertyId { get; set; }
    public Guid? ReportedById { get; set; }

    // Navigation properties
    public Property Property { get; set; } = null!;
    public User? ReportedBy { get; set; }
}

public static class MaintenanceStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
}
```

#### Step 2: Add to DbContext

```csharp
// Data/RentTrackerDbContext.cs
public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

// In OnModelCreating:
modelBuilder.Entity<MaintenanceRequest>(entity =>
{
    entity.Property(m => m.Title).HasMaxLength(200);
    entity.Property(m => m.Description).HasMaxLength(1000);
    entity.Property(m => m.Status).HasMaxLength(50);
    
    entity.HasOne(m => m.Property)
        .WithMany()
        .HasForeignKey(m => m.PropertyId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

#### Step 3: Create Migration

```bash
dotnet ef migrations add AddMaintenanceRequests
```

#### Step 4: Create Index Page (List View)

```csharp
// Pages/Maintenance/Index.cshtml.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Maintenance;

[Authorize(Roles = "Administrator,Owner")]  // Adjust roles as needed
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public IndexModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    public List<MaintenanceRequest> Requests { get; set; } = new();

    public async Task OnGetAsync()
    {
        // NOTE: Client-side ordering for DateTimeOffset (SQLite-safe)
        var requestsList = await _context.MaintenanceRequests
            .Include(r => r.Property)
            .ToListAsync();
            
        Requests = requestsList
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
}
```

```html
<!-- Pages/Maintenance/Index.cshtml -->
@page
@model RentTracker.Web.Pages.Maintenance.IndexModel
@{
    ViewData["Title"] = "Maintenance Requests";
}

<div class="page-header">
    <h2>Maintenance Requests</h2>
    <a asp-page="./Create" class="btn btn-primary">New Request</a>
</div>

<div class="page-content">
    @if (Model.Requests.Any())
    {
        <div class="table-container">
            <table class="table">
                <thead>
                    <tr>
                        <th>Property</th>
                        <th>Title</th>
                        <th>Status</th>
                        <th>Created</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var request in Model.Requests)
                    {
                        <tr>
                            <td>@request.Property.Name</td>
                            <td>@request.Title</td>
                            <td>
                                <span class="badge @(request.Status == "Completed" ? "badge-success" : request.Status == "In Progress" ? "badge-warning" : "badge-info")">
                                    @request.Status
                                </span>
                            </td>
                            <td>@request.CreatedAt.ToString("dd MMM yyyy")</td>
                            <td>
                                <a asp-page="./Details" asp-route-id="@request.Id" class="btn btn-sm btn-secondary">View</a>
                                <a asp-page="./Edit" asp-route-id="@request.Id" class="btn btn-sm btn-secondary">Edit</a>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
    else
    {
        <p class="text-muted">No maintenance requests found.</p>
    }
</div>
```

#### Step 5: Create Page (Form)

```csharp
// Pages/Maintenance/Create.cshtml.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Pages.Maintenance;

[Authorize(Roles = "Administrator,Owner")]
public class CreateModel : PageModel
{
    private readonly RentTrackerDbContext _context;

    public CreateModel(RentTrackerDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public MaintenanceRequest Request { get; set; } = new();

    public SelectList Properties { get; set; } = new(Enumerable.Empty<object>());

    public async Task OnGetAsync()
    {
        var properties = await _context.Properties
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync();
        Properties = new SelectList(properties, "Id", "Name");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        _context.MaintenanceRequests.Add(Request);
        await _context.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
```

```html
<!-- Pages/Maintenance/Create.cshtml -->
@page
@model RentTracker.Web.Pages.Maintenance.CreateModel
@{
    ViewData["Title"] = "New Maintenance Request";
}

<div class="page-header">
    <h2>New Maintenance Request</h2>
    <a asp-page="./Index" class="btn btn-secondary">Back</a>
</div>

<div class="page-content">
    <form method="post" class="form-horizontal">
        <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>

        <div class="form-group">
            <label asp-for="Request.PropertyId"></label>
            <select asp-for="Request.PropertyId" asp-items="Model.Properties" class="form-control">
                <option value="">-- Select Property --</option>
            </select>
            <span asp-validation-for="Request.PropertyId" class="text-danger"></span>
        </div>

        <div class="form-group">
            <label asp-for="Request.Title"></label>
            <input asp-for="Request.Title" class="form-control" />
            <span asp-validation-for="Request.Title" class="text-danger"></span>
        </div>

        <div class="form-group">
            <label asp-for="Request.Description"></label>
            <textarea asp-for="Request.Description" class="form-control" rows="4"></textarea>
            <span asp-validation-for="Request.Description" class="text-danger"></span>
        </div>

        <div class="form-group mt-4">
            <button type="submit" class="btn btn-primary">Create Request</button>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

#### Step 6: Update Navigation

```html
<!-- Add to Pages/Shared/_Layout.cshtml in the nav section -->
<a asp-page="/Maintenance/Index">Maintenance</a>
```

#### Step 7: Verify Build

```bash
dotnet build
# Should succeed with no errors
```

---

### Template: Creating a Service Layer

When business logic becomes complex, extract it into a service:

#### Step 1: Create Interface

```csharp
// Services/IPaymentCalculationService.cs
namespace RentTracker.Web.Services;

public interface IPaymentCalculationService
{
    decimal CalculateLateFee(Payment payment, DateTimeOffset dueDate);
    decimal GetTotalRevenueForPeriod(DateTimeOffset start, DateTimeOffset end);
    decimal GetOutstandingBalance(Guid leaseId);
}
```

#### Step 2: Implement Service

```csharp
// Services/PaymentCalculationService.cs
using RentTracker.Web.Data;
using RentTracker.Web.Models;

namespace RentTracker.Web.Services;

public class PaymentCalculationService : IPaymentCalculationService
{
    private readonly RentTrackerDbContext _context;

    // Constructor injection - DbContext is automatically provided
    public PaymentCalculationService(RentTrackerDbContext context)
    {
        _context = context;
    }

    public decimal CalculateLateFee(Payment payment, DateTimeOffset dueDate)
    {
        if (payment.PaymentDate > dueDate)
        {
            var daysLate = (payment.PaymentDate - dueDate).Days;
            return payment.Amount * 0.01m * daysLate; // 1% per day
        }
        return 0;
    }

    public decimal GetTotalRevenueForPeriod(DateTimeOffset start, DateTimeOffset end)
    {
        // NOTE: Fetch first, then filter (SQLite-safe)
        var payments = _context.Payments
            .Where(p => p.Status == PaymentStatus.Received)
            .ToList();

        return payments
            .Where(p => p.ForPeriod >= start && p.ForPeriod <= end)
            .Sum(p => p.Amount);
    }

    public decimal GetOutstandingBalance(Guid leaseId)
    {
        var lease = _context.Leases.Find(leaseId);
        if (lease == null) return 0;

        var totalAgreed = lease.AgreedPrice * 12; // Simplified - actual logic would be more complex
        
        var totalPaid = _context.Payments
            .Where(p => p.LeaseId == leaseId && p.Status == PaymentStatus.Received)
            .Sum(p => p.Amount);

        return totalAgreed - totalPaid;
    }
}
```

#### Step 3: Register Service

```csharp
// Program.cs
// Add this line in the services configuration section:
builder.Services.AddScoped<IPaymentCalculationService, PaymentCalculationService>();

// Other services...
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
```

#### Step 4: Use in PageModel

```csharp
// Pages/Reports/Index.cshtml.cs
public class IndexModel : PageModel
{
    private readonly RentTrackerDbContext _context;
    private readonly IPaymentCalculationService _paymentService;  // Injected!

    public IndexModel(
        RentTrackerDbContext context,
        IPaymentCalculationService paymentService)
    {
        _context = context;
        _paymentService = paymentService;
    }

    public async Task OnGetAsync()
    {
        // Use the service
        var startOfMonth = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        
        ThisMonthRevenue = _paymentService.GetTotalRevenueForPeriod(startOfMonth, endOfMonth);
        
        // ... rest of the code
    }
}
```

---

### SQLite DateTimeOffset: Complete Reference for AI

**The Problem:**
SQLite doesn't support DateTimeOffset in LINQ translations. These will FAIL:

```csharp
// ❌ BROKEN - Don't use these patterns:
await _context.Payments.OrderBy(p => p.CreatedAt).ToListAsync();
await _context.Payments.Where(p => p.ForPeriod.Year == 2024).ToListAsync();
await _context.Payments.Where(p => p.ForPeriod.Month == 6).ToListAsync();
await _context.Payments.Where(p => p.ForPeriod.Day == 15).ToListAsync();
```

**The Solution - Pattern for AI:**

```csharp
// ✅ CORRECT - Fetch first, then process in memory

// For ordering:
var items = await _context.Payments
    .Where(p => p.Status == "Received")  // SQL-compatible filter
    .ToListAsync();                       // Execute query

var sorted = items.OrderBy(p => p.CreatedAt).ToList();  // Client-side sort

// For filtering by year/month:
var allPayments = await _context.Payments.ToListAsync();
var thisYear = allPayments.Where(p => p.ForPeriod.Year == 2024).ToList();
var thisMonth = allPayments.Where(p => p.ForPeriod.Month == 6).ToList();

// For complex filters:
var filtered = await _context.Payments
    .Where(p => p.Status == "Pending")  // SQL-compatible
    .ToListAsync();

var result = filtered
    .Where(p => p.ForPeriod.Year == 2024 && p.ForPeriod.Month >= 1)
    .OrderBy(p => p.ForPeriod)
    .ToList();
```

**Memory/Performance Note:**
This pattern loads all data into memory. For large datasets, add SQL-compatible filters first:

```csharp
// Efficient: Filter in SQL first, then in memory
var recentPayments = await _context.Payments
    .Where(p => p.CreatedAt > DateTimeOffset.UtcNow.AddYears(-1))  // SQL-compatible
    .ToListAsync();

var sorted = recentPayments.OrderBy(p => p.CreatedAt).ToList();  // Client-side
```

---

### Authorization Patterns for AI

**Role-Based Access:**

```csharp
// Only logged-in users
[Authorize]
public class DetailsModel : PageModel { }

// Only Administrators
[Authorize(Roles = "Administrator")]
public class UserManagementModel : PageModel { }

// Administrator OR Owner
[Authorize(Roles = "Administrator,Owner")]
public class PropertyManagementModel : PageModel { }

// Check role in code
public async Task<IActionResult> OnGetAsync()
{
    if (User.IsInRole("Administrator"))
    {
        // Show admin features
    }
    else if (User.IsInRole("Owner"))
    {
        // Show owner features
    }
    else
    {
        // Regular user
    }
}
```

---

### Common Errors and Solutions for AI

**Error: "Cannot use 'DateTimeOffset' in ORDER BY"**
- Cause: Using `.OrderBy(p => p.DateTimeOffsetProperty)`
- Fix: Fetch with `.ToListAsync()` first, then `.OrderBy()`

**Error: "Cannot use 'DateTimeOffset.Year' in WHERE"**
- Cause: Using `.Where(p => p.Date.Year == 2024)`
- Fix: Fetch all, then filter in memory

**Error: "The LINQ expression could not be translated. DateTimeOffset comparison"**
- Cause: Using `.Where(p => p.Date >= startDate && p.Date <= endDate)` with DateTimeOffset
- Fix: Use `.AsEnumerable()` before the comparison to force client-side evaluation

**Error: "The LINQ expression could not be translated"**
- Cause: Using complex LINQ that SQLite doesn't support
- Fix: Simplify query or use client-side evaluation

**Error: "An error was generated for warning 'PendingModelChangesWarning'"**
- Cause: Model changed but no migration created
- Fix: Run `dotnet ef migrations add MigrationName`

---

### Learning Resources for Developers

**Understanding Razor Pages (vs MVC):**
- Official docs: https://learn.microsoft.com/en-us/aspnet/core/razor-pages/
- Key concept: Each page is self-contained (View + Controller logic)

**EF Core with SQLite:**
- Limitations: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- DateTimeOffset workaround: See above

**Dependency Injection:**
- Razor Pages use constructor injection just like MVC
- Services registered in Program.cs are available in all PageModels
- Scoped lifetime = one instance per HTTP request

**CSS without Bootstrap:**
- Use CSS custom properties (already defined in site.css)
- Mobile-first approach (media queries for larger screens)
- Flexbox/Grid for layouts

---

## Contact & Context

This project is maintained by a single developer.
Simplicity is the primary constraint.
Every line of code should justify its existence.
When in doubt, choose the simpler option.

**For AI agents:** When working on this codebase, prioritize working code over perfect abstractions. The goal is maintainability for a single developer over 5-10 years.
