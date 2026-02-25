# Converting Samples to PackageReference

This guide explains how to convert sample projects from using `ProjectReference` (development mode) to `PackageReference` (consumer mode) for use in your own applications.

## Why Samples Use ProjectReference

The sample projects in this repository use `ProjectReference` instead of `PackageReference` for development convenience:

- **Immediate feedback** - Changes to source code are reflected immediately without publishing
- **Debugging** - Step into framework source code during development
- **Single build** - Build everything together from the repository root

**Consumers should use `PackageReference`** to published NuGet packages for:

- **Version stability** - Use tested, released versions
- **Faster builds** - No need to compile framework source
- **Simpler dependencies** - Just add package references

## Quick Conversion

### Before (Sample ProjectReference)

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>
```

### After (Consumer PackageReference)

```xml
<ItemGroup>
  <PackageReference Include="Dispatch" Version="1.0.0" />
</ItemGroup>
```

Note: `Dispatch` automatically includes `Excalibur.Dispatch.Abstractions` as a transitive dependency.

## Step-by-Step Conversion

### Step 1: Copy the Sample Project

Copy the sample folder to your solution:

```bash
# Copy sample to your project
cp -r samples/DispatchMinimal ~/my-project/
```

### Step 2: Replace ProjectReferences

Open the `.csproj` file and replace all `ProjectReference` items with `PackageReference`:

```xml
<!-- Remove this entire ItemGroup -->
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>

<!-- Add this instead -->
<ItemGroup>
  <PackageReference Include="Dispatch" Version="1.0.0" />
</ItemGroup>
```

### Step 3: Remove Repository-Specific Files

Remove references to repository-specific build configuration if present:

```xml
<!-- Remove if present -->
<Import Project="..\..\Directory.Build.props" />
<Import Project="..\..\Directory.Build.targets" />
```

Also delete the `samples/Directory.Build.props` import if you copied it:

```bash
rm Directory.Build.props  # If copied from samples/
```

### Step 4: Restore and Build

```bash
dotnet restore
dotnet build
```

## Package Reference Mapping

Use this table to convert `ProjectReference` paths to `PackageReference` package names:

### Dispatch Packages (Messaging)

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj` | `Dispatch` |
| `src/Dispatch/Excalibur.Dispatch.Abstractions/Excalibur.Dispatch.Abstractions.csproj` | `Excalibur.Dispatch.Abstractions` |
| `src/Dispatch/Excalibur.Dispatch.SourceGenerators/Excalibur.Dispatch.SourceGenerators.csproj` | `Excalibur.Dispatch.SourceGenerators` |
| `src/Dispatch/Excalibur.Dispatch.Analyzers/Excalibur.Dispatch.Analyzers.csproj` | `Excalibur.Dispatch.Analyzers` |

### Dispatch Transports

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Dispatch/Excalibur.Dispatch.Transport.Abstractions/...` | `Excalibur.Dispatch.Transport.Abstractions` |
| `src/Dispatch/Excalibur.Dispatch.Transport.RabbitMQ/...` | `Excalibur.Dispatch.Transport.RabbitMQ` |
| `src/Dispatch/Excalibur.Dispatch.Transport.Kafka/...` | `Excalibur.Dispatch.Transport.Kafka` |
| `src/Dispatch/Excalibur.Dispatch.Transport.AzureServiceBus/...` | `Excalibur.Dispatch.Transport.AzureServiceBus` |
| `src/Dispatch/Excalibur.Dispatch.Transport.GooglePubSub/...` | `Excalibur.Dispatch.Transport.GooglePubSub` |
| `src/Dispatch/Excalibur.Dispatch.Transport.AwsSqs/...` | `Excalibur.Dispatch.Transport.AwsSqs` |

### Dispatch Serialization

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Dispatch/Excalibur.Dispatch.Serialization.MessagePack/...` | `Excalibur.Dispatch.Serialization.MessagePack` |
| `src/Dispatch/Excalibur.Dispatch.Serialization.Protobuf/...` | `Excalibur.Dispatch.Serialization.Protobuf` |
| `src/Dispatch/Excalibur.Dispatch.Serialization.MemoryPack/...` | `Excalibur.Dispatch.Serialization.MemoryPack` |

### Dispatch Cross-Cutting Concerns

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Dispatch/Excalibur.Dispatch.Caching/...` | `Excalibur.Dispatch.Caching` |
| `src/Dispatch/Excalibur.Dispatch.Security/...` | `Excalibur.Dispatch.Security` |
| `src/Dispatch/Excalibur.Dispatch.Security.Aws/...` | `Excalibur.Dispatch.Security.Aws` |
| `src/Dispatch/Excalibur.Dispatch.Security.Azure/...` | `Excalibur.Dispatch.Security.Azure` |
| `src/Dispatch/Excalibur.Dispatch.Observability/...` | `Excalibur.Dispatch.Observability` |
| `src/Dispatch/Excalibur.Dispatch.Resilience.Polly/...` | `Excalibur.Dispatch.Resilience.Polly` |
| `src/Dispatch/Excalibur.Dispatch.Validation.FluentValidation/...` | `Excalibur.Dispatch.Validation.FluentValidation` |

### Dispatch Hosting

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Dispatch/Excalibur.Dispatch.Hosting.AspNetCore/...` | `Excalibur.Dispatch.Hosting.AspNetCore` |
| `src/Dispatch/Excalibur.Dispatch.Hosting.AzureFunctions/...` | `Excalibur.Dispatch.Hosting.AzureFunctions` |
| `src/Dispatch/Excalibur.Dispatch.Hosting.AwsLambda/...` | `Excalibur.Dispatch.Hosting.AwsLambda` |
| `src/Dispatch/Excalibur.Dispatch.Hosting.GoogleCloudFunctions/...` | `Excalibur.Dispatch.Hosting.GoogleCloudFunctions` |

### Excalibur Packages (Application Framework)

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Excalibur/Excalibur.Domain/...` | `Excalibur.Domain` |
| `src/Excalibur/Excalibur.Data.Abstractions/...` | `Excalibur.Data.Abstractions` |
| `src/Excalibur/Excalibur.Data/...` | `Excalibur.Data` |
| `src/Excalibur/Excalibur.Application/...` | `Excalibur.Application` |

### Excalibur Event Sourcing

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Excalibur/Excalibur.EventSourcing.Abstractions/...` | `Excalibur.EventSourcing.Abstractions` |
| `src/Excalibur/Excalibur.EventSourcing/...` | `Excalibur.EventSourcing` |
| `src/Excalibur/Excalibur.EventSourcing.InMemory/...` | `Excalibur.EventSourcing.InMemory` |
| `src/Excalibur/Excalibur.EventSourcing.SqlServer/...` | `Excalibur.EventSourcing.SqlServer` |
| `src/Excalibur/Excalibur.EventSourcing.CosmosDb/...` | `Excalibur.EventSourcing.CosmosDb` |
| `src/Excalibur/Excalibur.EventSourcing.DynamoDb/...` | `Excalibur.EventSourcing.DynamoDb` |

### Excalibur Sagas

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Excalibur/Excalibur.Saga/...` | `Excalibur.Saga` |
| `src/Excalibur/Excalibur.Saga.SqlServer/...` | `Excalibur.Saga.SqlServer` |

### Excalibur Hosting

| ProjectReference Path | PackageReference Name |
|-----------------------|----------------------|
| `src/Excalibur/Excalibur.Hosting/...` | `Excalibur.Hosting` |
| `src/Excalibur/Excalibur.Hosting.Web/...` | `Excalibur.Hosting.Web` |
| `src/Excalibur/Excalibur.Hosting.Jobs/...` | `Excalibur.Hosting.Jobs` |

## Conversion Examples

### Example 1: DispatchMinimal Sample

**Before:**
```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>
```

**After:**
```xml
<ItemGroup>
  <PackageReference Include="Dispatch" Version="1.0.0" />
</ItemGroup>
```

### Example 2: ExcaliburCqrs Sample

**Before:**
```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Dispatch\Excalibur.Dispatch.Abstractions\Excalibur.Dispatch.Abstractions.csproj" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.Domain\Excalibur.Domain.csproj" />
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing.Abstractions\Excalibur.EventSourcing.Abstractions.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing\Excalibur.EventSourcing.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing.InMemory\Excalibur.EventSourcing.InMemory.csproj" />
</ItemGroup>
```

**After:**
```xml
<ItemGroup>
  <PackageReference Include="Dispatch" Version="1.0.0" />
  <PackageReference Include="Excalibur.Domain" Version="1.0.0" />
  <PackageReference Include="Excalibur.EventSourcing" Version="1.0.0" />
  <PackageReference Include="Excalibur.EventSourcing.InMemory" Version="1.0.0" />
</ItemGroup>
```

### Example 3: Full Event Sourcing with SQL Server

**Before:**
```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Dispatch\Dispatch\Excalibur.Dispatch.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.Domain\Excalibur.Domain.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing\Excalibur.EventSourcing.csproj" />
  <ProjectReference Include="..\..\src\Excalibur\Excalibur.EventSourcing.SqlServer\Excalibur.EventSourcing.SqlServer.csproj" />
</ItemGroup>
```

**After:**
```xml
<ItemGroup>
  <PackageReference Include="Dispatch" Version="1.0.0" />
  <PackageReference Include="Excalibur.EventSourcing.SqlServer" Version="1.0.0" />
</ItemGroup>
```

Note: `Excalibur.EventSourcing.SqlServer` includes transitive references to `Excalibur.EventSourcing` and `Excalibur.Domain`.

## Troubleshooting

### Missing Types After Conversion

**Problem:** Compiler errors like `The type or namespace 'IDispatcher' could not be found`

**Solution:** Ensure you have the correct package reference. Check:
1. Package name is spelled correctly
2. Package version exists (check nuget.org)
3. Run `dotnet restore` after adding references

### Version Conflicts

**Problem:** `Version conflict detected for Package.Name`

**Solution:** Use a consistent version across all Dispatch/Excalibur packages:

```xml
<ItemGroup>
  <!-- Use the same version for all packages -->
  <PackageReference Include="Dispatch" Version="1.0.0" />
  <PackageReference Include="Excalibur.Dispatch.Transport.RabbitMQ" Version="1.0.0" />
  <PackageReference Include="Excalibur.EventSourcing" Version="1.0.0" />
</ItemGroup>
```

Or use Central Package Management:

```xml
<!-- Directory.Packages.props -->
<ItemGroup>
  <PackageVersion Include="Dispatch" Version="1.0.0" />
  <PackageVersion Include="Excalibur.Dispatch.Transport.RabbitMQ" Version="1.0.0" />
  <PackageVersion Include="Excalibur.EventSourcing" Version="1.0.0" />
</ItemGroup>
```

### Build Errors from Directory.Build.props

**Problem:** Errors referencing paths like `..\..\src\...` or missing properties

**Solution:** Ensure you removed or updated any repository-specific build files:

```bash
# In your copied project directory
rm Directory.Build.props    # If copied from samples/
rm Directory.Build.targets  # If present
```

### Namespace Not Found

**Problem:** `The type or namespace name 'Dispatch' does not exist`

**Solution:** Add the correct `using` statement. Namespaces match package names:

```csharp
using Dispatch;                    // Core dispatcher
using Excalibur.Dispatch.Abstractions;       // Interfaces like IDispatcher
using Excalibur.Domain;            // Aggregate roots
using Excalibur.EventSourcing;     // Event sourcing
```

### Source Generators Not Running

**Problem:** `AddGeneratedServices()` method not found after conversion

**Solution:** Add the source generators package:

```xml
<ItemGroup>
  <PackageReference Include="Excalibur.Dispatch.SourceGenerators" Version="1.0.0" />
</ItemGroup>
```

Then clean and rebuild:

```bash
dotnet clean
dotnet build
```

## Related Documentation

- [Getting Started](../docs-site/docs/getting-started.md) - New project setup
- [Migration Guide](MIGRATION.md) - Dispatch to Excalibur migration
- [Source Generators](../docs-site/docs/source-generators/getting-started.md) - Using `[AutoRegister]`

---

*Updated: Sprint 427*
