# EXCMP004: Erasable personal data encrypted under a surviving purpose

| Property | Value |
|----------|-------|
| **Diagnostic ID** | EXCMP004 |
| **Title** | A property is both erasable personal data and encrypted under a surviving purpose |
| **Category** | Excalibur.Compliance.Encryption |
| **Severity** | Warning |
| **Enabled by default** | Yes |
| **Code-fix** | No |

## Cause

A property carries `[PersonalData]` and also `[EncryptedField]` with an explicit `Purpose`. The two make
opposite promises:

- `[PersonalData]` binds the value to a per-subject key that is **destroyed** when the subject is erased.
- An explicit `Purpose` binds it to a key chosen to **survive** erasure.

Honouring either one silently would make the other false, so the framework refuses both.

## Example

```csharp
public sealed class Patient
{
    [PersonalData]
    [EncryptedField(Purpose = "billing")]   // EXCMP004
    public string Name { get; set; } = "";
}
```

## How to Fix

Decide which promise the value needs:

```csharp
// Erasable with the subject: drop the Purpose.
[PersonalData]
[EncryptedField]
public string Name { get; set; } = "";

// Must outlive the subject: drop [PersonalData].
[EncryptedField(Purpose = "billing")]
public string BillingReference { get; set; } = "";
```

## When to Suppress

Do not suppress. The framework refuses this combination at run time.

## See Also

- [Field-level encryption](../security/encryption-architecture.md#field-level-encryption)
