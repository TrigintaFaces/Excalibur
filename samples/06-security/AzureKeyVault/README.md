# Azure Key Vault Sample

This sample demonstrates how to use **Azure Key Vault** with **Excalibur.Dispatch** for enterprise-grade secret management.

## Features

- **Secret Retrieval** - Secure credential access via `ICredentialStore`
- **Secret Storage** - Write credentials with `IWritableCredentialStore`
- **Caching Patterns** - Performance optimization with local caching
- **Rotation Support** - Secret rotation with version history
- **Managed Identity** - Production-ready authentication

## Prerequisites

### 1. Azure Subscription

Create an Azure Key Vault:

```bash
# Login to Azure
az login

# Create resource group (if needed)
az group create --name dispatch-demo-rg --location eastus

# Create Key Vault
az keyvault create \
  --name dispatch-demo-kv \
  --resource-group dispatch-demo-rg \
  --location eastus
```

### 2. Configure RBAC Permissions

Grant your identity access to secrets:

```bash
# Get your user principal ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Assign Key Vault Secrets User role (read)
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $USER_ID \
  --scope /subscriptions/{subscription-id}/resourceGroups/dispatch-demo-rg/providers/Microsoft.KeyVault/vaults/dispatch-demo-kv

# Assign Key Vault Secrets Officer role (read/write)
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee $USER_ID \
  --scope /subscriptions/{subscription-id}/resourceGroups/dispatch-demo-rg/providers/Microsoft.KeyVault/vaults/dispatch-demo-kv
```

### 3. Configure the Sample

Update `appsettings.json`:

```json
{
  "AzureKeyVault": {
    "VaultUri": "https://dispatch-demo-kv.vault.azure.net/",
    "KeyPrefix": "dispatch-"
  }
}
```

Or use environment variables:

```bash
export AzureKeyVault__VaultUri="https://dispatch-demo-kv.vault.azure.net/"
```

## Running the Sample

```bash
# Build
dotnet build

# Run
dotnet run
```

## Authentication Methods

The sample uses `DefaultAzureCredential`, which tries multiple authentication methods in order:

| Environment | Method | Configuration |
|-------------|--------|---------------|
| Local Dev | Azure CLI | `az login` |
| Local Dev | Visual Studio | Sign in to VS |
| Local Dev | VS Code | Azure Account extension |
| Production | Managed Identity | Enable on App Service/VM |
| CI/CD | Service Principal | Set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET` |

## Code Examples

### Basic Secret Retrieval

```csharp
public class MyService
{
    private readonly ICredentialStore _credentialStore;

    public MyService(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public async Task<string> GetApiKeyAsync(CancellationToken ct)
    {
        var secret = await _credentialStore.GetCredentialAsync("api-key", ct);
        if (secret == null)
            throw new InvalidOperationException("API key not found");

        // Convert SecureString only when needed
        return ConvertSecureString(secret);
    }
}
```

### Secret Storage with Auto-Expiration

```csharp
public async Task StoreConnectionStringAsync(string value, CancellationToken ct)
{
    var secure = new SecureString();
    foreach (var c in value) secure.AppendChar(c);
    secure.MakeReadOnly();

    try
    {
        // Secrets are stored with 90-day expiration by default
        await _writableStore.StoreCredentialAsync("db-connection", secure, ct);
    }
    finally
    {
        secure.Dispose();
    }
}
```

### Caching Pattern

```csharp
public class CachingCredentialStore
{
    private readonly ICredentialStore _store;
    private readonly ConcurrentDictionary<string, (SecureString Value, DateTime Expiry)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<SecureString?> GetCachedAsync(string key, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow < cached.Expiry)
            return cached.Value;

        var secret = await _store.GetCredentialAsync(key, ct);
        if (secret != null)
            _cache[key] = (secret, DateTime.UtcNow.Add(_cacheDuration));

        return secret;
    }
}
```

## Security Best Practices

### DO

- Use `SecureString` for in-memory credential storage
- Cache secrets locally to reduce Key Vault calls (rate limits apply)
- Use short cache durations (5-15 minutes)
- Implement secret rotation with Event Grid notifications
- Use Managed Identity in production

### DON'T

- Log credential values (even partially)
- Store secrets in configuration files
- Use long-lived secrets without rotation
- Share Key Vault access broadly

## Azure RBAC Roles

| Role | Permissions | Use Case |
|------|-------------|----------|
| Key Vault Secrets User | Get, List secrets | Read-only applications |
| Key Vault Secrets Officer | Get, Set, Delete, List, Backup, Restore | Admin/deployment tools |
| Key Vault Administrator | Full control | Emergency access only |

## Troubleshooting

### "Access Denied" Errors

1. Verify RBAC assignments:
   ```bash
   az role assignment list --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vault}
   ```

2. Check Azure CLI authentication:
   ```bash
   az account show
   ```

3. Allow 5-10 minutes for RBAC propagation

### "Secret Not Found" Errors

1. Verify the secret exists:
   ```bash
   az keyvault secret list --vault-name dispatch-demo-kv
   ```

2. Check the key prefix configuration matches

### Rate Limiting

Key Vault has transaction limits. If you see 429 errors:
- Implement caching (see examples above)
- Reduce polling frequency
- Consider Azure Cache for Redis for high-volume scenarios

## Related Samples

- [AWS Secrets Manager Sample](../AwsSecretsManager/) - AWS equivalent
- [Message Encryption Sample](../MessageEncryption/) - Encrypt message payloads
- [Audit Logging Sample](../AuditLogging/) - Security audit trails

## Learn More

- [Azure Key Vault Documentation](https://docs.microsoft.com/azure/key-vault/)
- [DefaultAzureCredential](https://docs.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [Key Vault Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)
