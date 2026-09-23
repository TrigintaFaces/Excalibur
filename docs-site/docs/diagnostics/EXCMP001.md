# EXCMP001: Encryption annotation on a type that cannot carry ciphertext

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXCMP001 |
| **Title** | An encryption annotation is on a property whose type cannot carry ciphertext |
| **Category** | Excalibur.Compliance.Encryption |
| **Severity** | Warning |
| **Enabled by default** | Yes |
| **Code-fix** | No |

## Cause

A property annotated with `[EncryptedField]` or `[Sensitive]` has a type other than `string` or `byte[]`.
Field encryption replaces the value with its ciphertext in place, so the property must be able to hold that
ciphertext. The framework refuses to encrypt a model carrying this annotation.

## Example

```csharp
public sealed class Account
{
    [EncryptedField]          // EXCMP001: 'int' is not encryptable
    public int Pin { get; set; }
}
```

## How to Fix

Store the value in a supported type, or remove the annotation if the value does not need protecting:

```csharp
public sealed class Account
{
    [EncryptedField]
    public string Pin { get; set; } = "";
}
```

## When to Suppress

Do not suppress. The framework will refuse to encrypt this model at run time; the diagnostic only moves that
refusal to the build.

## See Also

- [Field-level encryption](../security/encryption-architecture.md#field-level-encryption)
- [EXCMP002: missing accessor](./EXCMP002.md)
