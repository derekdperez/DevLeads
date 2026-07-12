# Dependencies

## Solutions

- `DevLeads.slnx`

## Projects

### `src/DevLeads.Core/DevLeads.Core.csproj`

- Frameworks: net10.0

### `src/DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`

- Frameworks: net10.0
- Project references: `../DevLeads.Core/DevLeads.Core.csproj`
- Packages: `Anthropic 12.35.1`, `Microsoft.EntityFrameworkCore.Sqlite 10.0.9`, `Microsoft.Extensions.Hosting.Abstractions 10.0.9`, `Microsoft.Extensions.Http 10.0.9`

### `src/DevLeads.Web/DevLeads.Web.csproj`

- Frameworks: net10.0
- Project references: `../DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`
