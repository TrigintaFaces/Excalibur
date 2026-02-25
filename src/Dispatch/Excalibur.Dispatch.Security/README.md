# Excalibur.Dispatch.Security

Security middleware and extensions for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Security
```

## Features

- Authorization middleware
- Claims-based security
- Role-based access control
- Message encryption
- Audit logging

## Configuration

```csharp
services.AddDispatch(options =>
{
    options.UseSecurity(security =>
    {
        security.RequireAuthentication();
        security.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    });
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
