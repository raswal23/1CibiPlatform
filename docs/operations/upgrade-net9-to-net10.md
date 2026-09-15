# Upgrading from .NET 9 to .NET 10

## Overview

This guide documents the steps taken to upgrade the **1CibiPlatform** solution from `.NET 9` to `.NET 10`. Follow these steps for any project in the solution that still targets `net9.0`.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed
- Visual Studio 2026 (18.6+) or later (with .NET 10 workload enabled)
- Git — commit or stash any pending changes before upgrading

---

## Step 1 — Update the Target Framework in `.csproj`

Open the project file (`.csproj`) and change the `<TargetFramework>` value:

```xml
<!-- Before -->
<TargetFramework>net9.0</TargetFramework>

<!-- After -->
<TargetFramework>net10.0</TargetFramework>
```

For projects that multi-target:

```xml
<!-- Before -->
<TargetFrameworks>net9.0;net8.0</TargetFrameworks>

<!-- After -->
<TargetFrameworks>net10.0;net8.0</TargetFrameworks>
```

---

## Step 2 — Update NuGet Packages

After changing the TFM, update all packages that have a version tied to the .NET version.

### Common packages to update

| Package | .NET 9 Version | .NET 10 Version |
|---|---|---|
| `Microsoft.AspNetCore.Mvc.Testing` | `9.0.x` | `10.0.0` |
| `Microsoft.EntityFrameworkCore` | `9.0.x` | `10.0.0` |
| `Microsoft.EntityFrameworkCore.Design` | `9.0.x` | `10.0.0` |
| `Microsoft.EntityFrameworkCore.Tools` | `9.0.x` | `10.0.0` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `9.0.x` | `10.0.0` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `9.0.x` | `10.0.x` |
| `Microsoft.Extensions.Configuration.Abstractions` | `9.0.x` | `10.0.x` |

### Update via CLI

```powershell
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 10.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.0
```

---

## Step 3 — Update Dockerfile(s)

If your project uses Docker, update the base images in the `Dockerfile`:

```dockerfile
# Before
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# After
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
```

---

## Step 4 — Clean All Build Artifacts

Stale `bin/` and `obj/` folders from the old TFM **must be deleted** before rebuilding, or you will get errors like:

```
CS0006: Metadata file '...\obj\Debug\net9.0\ref\Project.dll' could not be found
MSB3030: Could not copy the file '...\bin\Debug\net9.0\Project.dll' because it was not found.
```

### Clean via PowerShell

```powershell
Get-ChildItem -Path "D:\GitHub\1CibiPlatform" -Recurse -Directory -Include "bin","obj" |
	Remove-Item -Recurse -Force
```

### Clean via CLI

```powershell
dotnet clean "D:\GitHub\1CibiPlatform\1CibiPlatform.sln"
```

---

## Step 5 — Restore NuGet Packages

```powershell
dotnet restore "D:\GitHub\1CibiPlatform\1CibiPlatform.sln"
```

---

## Step 6 — Resolve Project Reference Conflicts

When multiple projects in the same test project reference assemblies that both define a `Program` class (e.g., `APIs` and `YarpApiGateway`), use the `Aliases` attribute to avoid `CS0433`:

```xml
<!-- In Test.csproj -->
<ProjectReference Include="..\..\ApiGateways\YarpApiGateway\YarpApiGateway.csproj" Aliases="YarpGateway" />
<ProjectReference Include="..\..\BackendAPI\API\APIs\APIs.csproj" />
```

Then reference the aliased assembly explicitly in code if needed:

```csharp
extern alias YarpGateway;
```

---

## Step 7 — Rebuild the Solution

```powershell
dotnet build "D:\GitHub\1CibiPlatform\1CibiPlatform.sln"
```

Expected output:

```
Build succeeded.
	0 Error(s)
```

---

## Step 8 — Reload Solution in Visual Studio

After upgrading, Visual Studio may still hold stale in-memory state from the old `net9.0` build. To fix this:

1. Go to **File → Close Solution**
2. Go to **File → Open → Project/Solution**
3. Reopen `1CibiPlatform.sln`

> ⚠️ **Important:** The VS IDE build and `dotnet build` CLI are independent. Always verify with `dotnet build` from the terminal to confirm the true build state.

---

## Step 9 — Clear Visual Studio Cache (if still failing)

```powershell
# Remove Visual Studio solution cache
Get-ChildItem -Path "D:\GitHub\1CibiPlatform" -Recurse -Directory -Filter ".vs" |
	Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
```

Then reopen the solution.

---

## Troubleshooting

| Error | Cause | Fix |
|---|---|---|
| `NU1201: Project X is not compatible with net9.0` | A referenced project targets `net10.0` but the referencing project is still on `net9.0` | Upgrade the referencing project's TFM to `net10.0` |
| `CS0006: Metadata file '...net9.0\...' could not be found` | Stale VS in-memory state or leftover `obj/` artifacts | Delete `bin/obj`, restore, and reload the solution in VS |
| `MSB3030: Could not copy '...net9.0\Project.dll'` | Same as above — VS using old TFM path | Delete `bin/obj`, clean, restore, rebuild |
| `CS0433: Type 'Program' exists in both 'X' and 'Y'` | Two referenced projects both expose a `Program` class | Add `Aliases="..."` to one of the `<ProjectReference>` entries |
| `NETSDK1005: Assets file doesn't have a target for 'net9.0'` | `obj/project.assets.json` was generated for `net10.0` but VS is still requesting `net9.0` | Delete `obj/`, run `dotnet restore`, and reload solution |

---

## Summary Checklist

- [ ] Update `<TargetFramework>` to `net10.0` in all `.csproj` files
- [ ] Update all framework-versioned NuGet packages to `10.x`
- [ ] Update `Dockerfile` base images to `dotnet/aspnet:10.0` and `dotnet/sdk:10.0`
- [ ] Delete all `bin/` and `obj/` folders
- [ ] Run `dotnet restore`
- [ ] Run `dotnet build` — confirm 0 errors
- [ ] Reload solution in Visual Studio
- [ ] Clear `.vs/` cache folder if VS IDE build still fails
