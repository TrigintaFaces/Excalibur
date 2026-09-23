# EXCMP002: Encryption annotation on a property without a getter and a setter

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXCMP002 |
| **Title** | An encryption annotation is on a property without both a getter and a setter |
| **Category** | Excalibur.Compliance.Encryption |
| **Severity** | Warning |
| **Enabled by default** | Yes |
| **Code-fix** | No |

## Cause

A property annotated with `[EncryptedField]` or `[Sensitive]` has no setter, or no getter. The framework
reads the value, encrypts it, and writes the ciphertext back into the same property, so it needs both. An
`init` accessor counts as a setter; a get-only property does not. The framework refuses to encrypt a model
carrying this annotation.

## Example

```csharp
public sealed class Customer
{
    [EncryptedField]          // EXCMP002: no setter
    public string Ssn { get; } = "";
}
```

## How to Fix

Add a setter or an `init` accessor. Its accessibility does not matter — a `private set` works:

```csharp
public sealed class Customer
{
    [EncryptedField]
    public string Ssn { get; init; } = "";
}
```

## When to Suppress

Do not suppress. The framework will refuse to encrypt this model at run time.

## See Also

- [Field-level encryption](../security/encryption-architecture.md#field-level-encryption)
