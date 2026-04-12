# AGENTS.md - Rent Tracker

## Project Overview

This is a rent tracking web application built with:
- .NET 8 LTS (Blazor WebAssembly with ASP.NET Core hosted)
- SQLite database with Entity Framework Core
- ASP.NET Core Identity for authentication
- Pure CSS for responsive design
- Chart.js for reports

## Architecture

### Project Structure
Single-project structure at root level:
```
/rent_tracker/
├── Data/           - DbContext and migrations
├── Models/         - Entity classes
├── Components/     - Shared Blazor components
├── Pages/          - Blazor pages (routable)
└── wwwroot/css/    - Stylesheets
```

### Key Technical Decisions

1. **Database**: SQLite with automatic migrations on startup
   - Connection string: `Data Source=/data/renttracker.db` (Docker volume)
   - Pragmas set: WAL mode, Foreign Keys, Synchronous=NORMAL

2. **Authentication**: ASP.NET Core Identity
   - Roles: SystemAdministrator, Owner, Tenant
   - Default admin: `admin/admin` (must change password on first login)

3. **Frontend**: Blazor WebAssembly
   - Hosted by ASP.NET Core backend
   - Chart.js loaded from CDN

4. **Currency**: Decimal storage with "BOB" display (Bolivianos)

## Coding Standards

### General
- Use C# 12 features (primary constructors, collection expressions where appropriate)
- Use nullable reference types (enabled by default in .NET 8)
- Prefer async/await for I/O operations
- Keep methods small and focused

### Models
- Use required properties for non-nullable fields
- Use init-only setters for immutable data
- Navigation properties should be virtual for EF Core lazy loading

### Database
- All entities must have a primary key named `Id`
- Use decimal(18,2) for monetary values
- Use DateTime with UTC
- Enable soft delete via `IsEnabled` flag (never hard delete)

### UI/UX
- Pure CSS, no frameworks
- Mobile-first responsive design
- Semantic HTML elements
- Accessible forms (labels, aria attributes)

## Common Patterns

### Adding a Migration
```bash
dotnet ef migrations add <Name> --output-dir Data/Migrations
```

### Creating a Service
```csharp
public interface IPropertyService
{
    Task<Property> GetByIdAsync(int id);
    Task<Property> CreateAsync(PropertyInput input);
}

public class PropertyService : IPropertyService
{
    private readonly ApplicationDbContext _context;
    public PropertyService(ApplicationDbContext context) => _context = context;
    // Implementation...
}
```

### Blazor Component Structure
```razor
@page "/properties"
@inject IPropertyService PropertyService
@inject NavigationManager Navigation
@attribute [Authorize(Roles = "SystemAdministrator,Owner")]

<PageTitle>Properties - Rent Tracker</PageTitle>

@if (_loading)
{
    <p>Loading...</p>
}
else
{
    <!-- Content -->
}

@code {
    private bool _loading = true;
    
    protected override async Task OnInitializedAsync()
    {
        // Load data
        _loading = false;
    }
}
```

## Docker Commands

### Build
```bash
docker build -t rent-tracker .
```

### Run
```bash
docker-compose up -d
```

### Stop
```bash
docker-compose down
```

## Database Schema

### ApplicationUser
- Id, UserName, NormalizedUserName, Email, etc. (Identity defaults)
- FullName (string, unique, required)
- RequiresPasswordChange (bool)

### Property
- Id (int, PK)
- LocationLatitude (decimal)
- LocationLongitude (decimal)
- SurfaceSquareMeters (int)
- NumberOfRooms (int)
- HasBathroom, HasKitchen, HasGarage, HasHotWater, HasAC, HasBackyard, HasSecurity, HasDoorbell (bool)
- CurrentPrice (decimal)
- CurrentWarranty (decimal)
- IsEnabled (bool)
- CreatedAt (DateTime)

### PropertyOwner
- PropertyId (int, FK)
- OwnerId (string, FK to ApplicationUser)
- Since (DateTime)

### PropertyPriceHistory
- Id (int, PK)
- PropertyId (int, FK)
- Price (decimal)
- Warranty (decimal)
- EffectiveFrom (DateTime)
- ChangedByUserId (string, FK)

### Rental
- Id (int, PK)
- PropertyId (int, FK)
- TenantId (string, FK to ApplicationUser)
- StartDate (DateTime)
- EndDate (DateTime?)
- MonthlyRent (decimal)
- WarrantyAmount (decimal)
- Status (RentalStatus: Active, Closed, Terminated)
- TerminationReason (string?)

### RentalPayment
- Id (int, PK)
- RentalId (int, FK)
- Amount (decimal)
- PaymentDate (DateTime)
- PeriodStart (DateTime)
- PeriodEnd (DateTime)
- Version (int) - for audit trail
- PreviousPaymentId (int?) - for versioning chain

## Build & Run

### Local Development
```bash
dotnet run
# Access at https://localhost:5001 or http://localhost:5000
```

### Docker
```bash
docker-compose up -d
# Access at http://localhost:8080
```

## Testing

After first run, login with:
- Username: `admin`
- Password: `admin`
- You'll be redirected to password change page

## CI/CD

GitHub Actions workflow triggers on every push to `main`:
- Builds the project
- Creates Docker image
- Uploads as artifact (no registry push yet)
