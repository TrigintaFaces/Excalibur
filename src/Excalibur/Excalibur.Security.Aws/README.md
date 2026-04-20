# Excalibur.Dispatch.Security.Aws

AWS security integrations for Dispatch messaging including AWS Secrets Manager credential management.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Security.Aws
```

## Features

- AWS Secrets Manager integration for credential management
- Automatic secret rotation support
- Secure credential caching
- Integration with Dispatch messaging security pipeline

## Configuration

```csharp
services.AddDispatch(options =>
{
    options.UseSecurity(security =>
    {
        security.UseAwsSecretsManager(aws =>
        {
            aws.Region = "us-east-1";
            aws.SecretName = "my-application/secrets";
        });
    });
});
```

## Requirements

- AWS SDK for .NET
- AWS credentials configured (IAM role, environment variables, or AWS profile)

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
