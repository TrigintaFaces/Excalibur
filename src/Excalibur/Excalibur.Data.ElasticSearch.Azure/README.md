# Excalibur.Data.ElasticSearch.Azure

Azure Key Vault connection-credential storage for `Excalibur.Data.ElasticSearch`.

Install this package only if you back Elasticsearch authentication secrets with Key Vault. The base
`Excalibur.Data.ElasticSearch` package carries no Azure SDK dependency, so consumers who do not use
Key Vault do not download `Azure.Identity` or `Azure.Security.KeyVault.Secrets`.

## Usage

```csharp
services.AddAzureKeyVaultCredentialStorage(configuration);
services.AddElasticsearchSecurity(configuration);
```

Call it **before** `AddElasticsearchSecurity` (or `AddAuthentication`): the base package registers an
in-memory development store with `TryAdd`, so the first registration wins.

Configuration binds from `Elasticsearch:Security:Encryption:KeyManagement:AzureKeyVault`.

## What this stores, and what it does not

This is a **connection-credential store** — the OAuth tokens, service-account secrets, passwords and
API keys used to authenticate *to* Elasticsearch. It returns opaque secret strings.

**It never returns key material for cryptographic operations.** That is a deliberate boundary, not an
omission. Field-level encryption keys are resolved through `Excalibur.Compliance.IKeyManagementProvider`
— a structurally separate abstraction that hands back key metadata rather than keys, so this package
cannot obtain key material even accidentally.

Two other Excalibur packages also integrate Azure Key Vault and are **not** interchangeable with this
one:

| Package | Abstraction | Key Vault surface | What it is for |
|---|---|---|---|
| `Excalibur.Data.ElasticSearch.Azure` (this) | `IElasticsearchKeyProvider` | Secrets | Elasticsearch connection credentials |
| `Excalibur.Security.Azure` | `IKeyProvider` (`byte[]`-valued) | Secrets | Key custody via secrets |
| `Excalibur.Compliance.Azure` | `IKeyManagementProvider` | Keys + Crypto | True key custody |

They integrate with the same external system, which is the one property every adapter to Key Vault
shares and therefore the worst reason to treat them as duplicates. Substituting one for another moves
key material across a boundary that exists to keep it out of this package.

## Migrating from `Excalibur.Data.ElasticSearch`

These types shipped in the base package. Add a `PackageReference` to this one; namespaces, type names
and the `AddAzureKeyVaultCredentialStorage` signature are unchanged, so no source edit is required.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
