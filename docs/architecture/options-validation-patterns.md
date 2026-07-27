# Options Validation: 3 Patterns

## When to Use Each

| Pattern | Use When | Example |
|---------|----------|---------|
| `DataAnnotations` | Simple property validation ([Required], [Range]) | `[Required] public string ConnectionString { get; set; }` |
| `IValidateOptions<T>` | Cross-property validation, custom logic | ConnectionString format, Region+QueueUrl consistency |
| `IPostConfigureOptions<T>` | Deferred resolution, defaults from other options | Setting defaults based on environment or other config |

## Registration Pattern

```csharp
// DataAnnotations + ValidateOnStart
services.AddOptions<MyOptions>()
    .BindConfiguration("My")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// IValidateOptions<T> (custom)
services.AddSingleton<IValidateOptions<MyOptions>, MyOptionsValidator>();

// IPostConfigureOptions<T> (deferred defaults)
services.AddSingleton<IPostConfigureOptions<MyOptions>, MyOptionsPostConfigure>();
```

## Priority Order

1. `IPostConfigureOptions<T>` runs first (sets defaults)
2. `DataAnnotations` validate property-level constraints
3. `IValidateOptions<T>` validates cross-property constraints
4. `ValidateOnStart()` triggers all validation at startup
