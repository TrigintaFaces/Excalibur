# DISP003: Reflection Without AOT Annotation

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DISP003 |
| **Title** | Reflection usage without AOT annotation |
| **Category** | Excalibur.Dispatch.Compatibility |
| **Severity** | Warning |
| **Enabled by default** | Yes |

## Cause

A method uses reflection-based operations (such as `Type.GetType()`, `Activator.CreateInstance()`, or `Assembly.GetTypes()`) without proper AOT annotations. This may cause issues when the application is published as Native AOT.

## Example

The following code triggers DISP003:

```csharp
// Warning DISP003: Method 'ResolveHandler' uses 'Activator.CreateInstance'
// without AOT annotation
public class HandlerFactory
{
    public object ResolveHandler(Type handlerType)
    {
        return Activator.CreateInstance(handlerType)!;
    }
}
```

## How to Fix

**Option 1:** Add AOT annotations to inform the trimmer:

```csharp
using System.Diagnostics.CodeAnalysis;

public class HandlerFactory
{
    [RequiresDynamicCode("Creates handler instances dynamically")]
    public object ResolveHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type handlerType)
    {
        return Activator.CreateInstance(handlerType)!;
    }
}
```

**Option 2 (Recommended):** Use source generators instead of reflection:

```csharp
// Source generators resolve handlers at compile time,
// avoiding reflection entirely
services.AddDispatch(typeof(Program).Assembly);
```

## When to Suppress

Suppress this warning if your application will never be published as Native AOT and you are comfortable with the reflection-based approach.

## See Also

- [DISP004: Optimization Hint](./DISP004.md)
- [Advanced Source Generators](../advanced/source-generators.md)
