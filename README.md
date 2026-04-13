# Rent tracker

Web application that allows to register multiple rent locations to keep track of the payments

![index](https://user-images.githubusercontent.com/248934/199085783-d9e49d36-cdc7-41f7-9331-db51b118272f.jpg)

## Expected functionality

- Web application that we can access from a computer or mobile device
- A property represents a house, land, room or department
- Allows to track multiple properties
- Each property can have multiple owners
- Each property can have a tenant at a time, but multiple over its lifetime
- Data should be stored in a centralized place, using the most simple technology (SQLite)
- It should be functional and not fancy
- It should be fast and responsive
- Ideally, we should provide reports over year, month regarding the rental payments

## Use cases / Phase - 1

1. Create an account with some role, initial roles are System Administrator, Owner, Tennant
2. Use the full-name as the account identity, validate uniqueness
3. Add a property, the data required for a property must include: name, location (GPS), surface in meters, nr of rooms, facilities included (bathroom, kitchen, garage, hot water, AC, backyard, security, doorbell), price and warranty
4. Update a property, all data can be updated including location (moving to a new address is allowed)
5. Disable property, the property will not be removed, only will be usable when enabled again (data will be kept always)
6. Enable property, (data will be kept always)
7. Rent a property to a tenant
8. Close rental of property, when the rental is finished
9. Terminate rental of property, when something unexpected happened and the rental must be forcibly finished
10. Add rental payment
11. Update rental payment, this will not overwrite the prior payment, a new one must be created and the payment logs must be kept
12. We need to keep an history of the prices, warranties, because those can change in the future
13. The prices/costs can be stored in Bolivianos for the moment, but we must consider that in the future we would like to support USD as well
14. Reports, we will need reports about the properties, the rentals, payments, etc. Something in real-time would be enough with some nice graphs
15. There will be a default admin/admin user that will require a password change after first login, this user can set other users
14. A property can be leased as a whole or in units perhaps, the front unit for a tennat, the back unit for a different tennat
15. Perhaps use 'lease' instead of rent? check
16. Ideally we would like the location to be pasted as a shared link from google maps, and rendered as open-in-map link to be opened in a new tab in google maps

## Technical specs

- Use dotnet latest release
- Use mise for the tooling, everything must be restorable from the project folder
- If additional js libs are needed, be sure to have them managed, nothing copy/pasted
- All resources must be self-hosted, shouldn't download nor require anything from the internet
- Use ORM tooling, native sql must be avoided if possible
- There's no need for additional services, just the sqlite db
- Use the standard stack, with the frontend no JS frameworks
- The sqlite DB must be optimized with some pragmas, check
- It's gonna be deployed as a docker image, the db and other uploaded files should be able to be mapped into a local folder
- Must be design-responsive, will need to work on desktop, mobile and tablet
- It must use standard CSS since it's quite capable now, no additional frameworks
- The design must be simple and clear, the users are gonna be regular
- Design the application for future changes, using migrations, good coding practices and keeping complexity controlled, the migrations must be automatically applied on startup
- Consider that AIs will be used extensively in this project, define proper build guidelines, proper documentation and instructions for AI agents (AGENTS.md, copilot instructions, etc.)
- The project will be hosted in GitHub and built with Github actions, there's no need to place it in a registry yet, the build must result in a docker image we can download and load
- There must be a default admin user with admin/admin credentials, it can do everything
- We can have Owners and Tennats, Owners can do pretty much everything but create users, Tennants can see pending payments and stats
- Reports and stats must be real-time, preferably shown in a chart
- Simplicity is a guideline, must be simple to read, write and maintain, set that guideline everywhere for the AI agents as well
- The development environment will be vscode, create the setup files and tasks for that

## Quick Start Guide

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download) (or use `mise install`)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

### First Time Setup
```bash
# 1. Clone/navigate to the repository
cd /Users/timoteo/projects/mine/rent_tracker

# 2. Restore dependencies
dotnet restore
libman restore

# 3. Build the project
dotnet build

# 4. Run the application
dotnet run

# 5. Open browser to:
# http://localhost:5000
# or the HTTPS URL shown in the console

# 6. Login with default credentials:
# Username: admin
# Password: admin
# (You will be forced to change password on first login)
```

### VS Code Development
```bash
# Open in VS Code
code .

# Use VS Code tasks (Ctrl+Shift+P → Tasks: Run Task):
# - build: Build the project
# - run: Run without debugging
# - watch: Run with hot reload
# - ef-migrations-add: Add database migration
# - ef-database-update: Apply migrations
```

### Docker Deployment
```bash
# Build and run with Docker Compose
docker-compose up -d

# Access at http://localhost:8080
# Database is persisted in ./data folder
```

### Default Credentials
- **Admin**: `admin` / `admin` (must change on first login)
- **New users** created by admin: `password123` (must change on first login)

### Project Structure
```
RentTracker/                    # Project root (single project, no solution)
├── RentTracker.csproj          # Project file
├── Program.cs                  # Application entry point
├── Pages/                      # Razor Pages (UI + Controllers)
│   ├── Index.cshtml           # Dashboard
│   ├── Properties/              # Property CRUD
│   ├── Leases/                 # Lease management
│   ├── Payments/               # Payment tracking
│   ├── Reports/                # Charts and reports
│   ├── Users/                  # User management (Admin only)
│   └── Account/                # Login/logout
├── Models/                     # Domain entities
├── Data/                       # Database context & migrations
├── wwwroot/                    # Static files (CSS, JS, images)
└── .vscode/                    # VS Code configuration
```

### Key Technologies
- **.NET 9 LTS** with Razor Pages (not MVC)
- **SQLite** database (single file, zero maintenance)
- **Entity Framework Core** (ORM - no raw SQL)
- **Vanilla CSS** (no frameworks like Bootstrap)
- **Chart.js** (via LibMan for charts)
- **Simple cookie authentication** (not ASP.NET Identity)

### Documentation
- **[AGENTS.md](./AGENTS.md)** - Comprehensive development guide for AI agents and maintainers
- **[mise.toml](./mise.toml)** - Tool version management and task definitions
- **[.vscode/tasks.json](./.vscode/tasks.json)** - VS Code build tasks

### AI-Assisted Development
This project is designed for extensive AI assistance. See [AGENTS.md](./AGENTS.md) for:
- How to add new features step-by-step
- Code patterns and templates
- Common pitfalls (especially SQLite limitations)
- Dependency injection patterns
- Service layer guidelines

### Support & Maintenance
- **.NET 9 LTS** supported until May 2026
- **SQLite** supported until 2050
- Upgrade to .NET 10 LTS planned for 2026
- See [AGENTS.md](./AGENTS.md) for maintenance checklists
