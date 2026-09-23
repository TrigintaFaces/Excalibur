# EXCMP003: Encryption annotation on a property the framework never inspects

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXCMP003 |
| **Title** | An encryption annotation is on a property the framework never inspects |
| **Category** | Excalibur.Compliance.Encryption |
| **Severity** | Warning |
| **Enabled by default** | Yes |
| **Code-fix** | No |

## Cause

A `static` or non-public property is annotated with `[EncryptedField]` or `[Sensitive]`. The framework only
encrypts public instance properties, so this annotation is never seen: the value is stored exactly as written.

**Nothing at run time reports this case.** The other encryption diagnostics describe annotations the framework
refuses when it encrypts; this one describes an annotation it never notices. This diagnostic is the only place
the problem surfaces.

## Example

```csharp
public sealed class Settings
{
    [EncryptedField]          // EXCMP003: not public — stored unencrypted
    internal string ClientSecret { get; set; } = "";
}
```

## How to Fix

Make the property a public instance property. If it must stay non-public, encrypt the value yourself before
assigning it, and remove the annotation so nobody reads it as protection that is being applied:

```csharp
public sealed class Settings
{
    [EncryptedField]
    public string ClientSecret { get; set; } = "";
}
```

## When to Suppress

Do not suppress. A suppressed EXCMP003 is a value stored in plaintext beside an annotation that says otherwise.

## See Also

- [Field-level encryption](../security/encryption-architecture.md#field-level-encryption)
