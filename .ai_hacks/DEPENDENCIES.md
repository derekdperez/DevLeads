# Dependencies

## Solutions

_None found._

## .NET projects

### `src/DevLeads.Core/DevLeads.Core.csproj`
- SDK: `Microsoft.NET.Sdk`
- Target frameworks: `net10.0`

### `src/DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`
- SDK: `Microsoft.NET.Sdk`
- Target frameworks: `net10.0`
- Packages:
  - `Anthropic 12.35.1`
  - `Microsoft.EntityFrameworkCore.Sqlite 10.0.9`
  - `Microsoft.Extensions.Hosting.Abstractions 10.0.9`
  - `Microsoft.Extensions.Http 10.0.9`
- Project references:
  - `../DevLeads.Core/DevLeads.Core.csproj`

### `src/DevLeads.Web/DevLeads.Web.csproj`
- SDK: `Microsoft.NET.Sdk.Web`
- Target frameworks: `net10.0`
- Project references:
  - `../DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`


## package.json

_None found._
