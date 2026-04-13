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

## Contact & Context

This project is maintained by a single developer.
Simplicity is the primary constraint.
Every line of code should justify its existence.
When in doubt, choose the simpler option.
