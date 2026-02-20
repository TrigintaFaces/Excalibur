# AWS Secrets Manager Sample

This sample demonstrates how to use **AWS Secrets Manager** with **Excalibur.Dispatch** for enterprise-grade secret management.

## Features

- **Secret Retrieval** - Secure credential access via `ICredentialStore`
- **Secret Storage** - Write credentials with `IWritableCredentialStore`
- **LocalStack Support** - Local development without AWS account
- **IAM Authentication** - Production-ready with IAM roles

## Prerequisites

### Option 1: Real AWS Account

```bash
# Install AWS CLI
# https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html

# Configure credentials
aws configure
# Enter: Access Key ID, Secret Access Key, Region

# Create a test secret
aws secretsmanager create-secret \
  --name dispatch-test-secret \
  --secret-string "my-secret-value"
```

### Option 2: LocalStack (Recommended for Development)

```bash
# Start LocalStack
docker run -d -p 4566:4566 localstack/localstack

# Verify it's running
curl http://localhost:4566/_localstack/health
```

## Configuration

Update `appsettings.json`:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566"
  }
}
```

For real AWS, remove `ServiceURL`:

```json
{
  "AWS": {
    "Region": "us-east-1"
  }
}
```

Or use environment variables:

```bash
export AWS__Region="us-east-1"
export AWS__ServiceURL="http://localhost:4566"
```

## Running the Sample

```bash
# Build
dotnet build

# Run
dotnet run
```

## Authentication Methods

AWS SDK tries multiple authentication methods in order:

| Method | Configuration | Use Case |
|--------|---------------|----------|
| Environment Variables | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` | CI/CD, containers |
| Shared Credentials | `~/.aws/credentials` | Local development |
| IAM Instance Profile | Automatic on EC2 | EC2 instances |
| ECS Task Role | Automatic in ECS | ECS tasks |
| Lambda Execution Role | Automatic in Lambda | Lambda functions |

## IAM Permissions

### Minimum (Read Only)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret"
      ],
      "Resource": "arn:aws:secretsmanager:*:*:secret:dispatch-*"
    }
  ]
}
```

### Full Access

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret",
        "secretsmanager:CreateSecret",
        "secretsmanager:PutSecretValue",
        "secretsmanager:UpdateSecret",
        "secretsmanager:DeleteSecret"
      ],
      "Resource": "arn:aws:secretsmanager:*:*:secret:dispatch-*"
    }
  ]
}
```

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

    public async Task<string> GetDatabasePasswordAsync(CancellationToken ct)
    {
        var secret = await _credentialStore.GetCredentialAsync("db-password", ct);
        if (secret == null)
            throw new InvalidOperationException("Database password not found");

        return ConvertSecureString(secret);
    }
}
```

### Using with LocalStack

```csharp
// appsettings.Development.json
{
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566"
  }
}

// No credentials needed for LocalStack
// The SDK will use dummy credentials automatically
```

## LocalStack Development

### Docker Compose Setup

```yaml
version: '3.8'
services:
  localstack:
    image: localstack/localstack
    ports:
      - "4566:4566"
    environment:
      - SERVICES=secretsmanager
      - DEBUG=1
      - PERSISTENCE=1
    volumes:
      - "./localstack-data:/var/lib/localstack"
```

### Creating Test Secrets

```bash
# Using AWS CLI with LocalStack
aws --endpoint-url=http://localhost:4566 secretsmanager create-secret \
  --name dispatch-test \
  --secret-string "test-value"

# List secrets
aws --endpoint-url=http://localhost:4566 secretsmanager list-secrets
```

## Secret Rotation

AWS Secrets Manager supports automatic rotation:

```bash
# Enable rotation (requires Lambda function)
aws secretsmanager rotate-secret \
  --secret-id dispatch-db-password \
  --rotation-lambda-arn arn:aws:lambda:us-east-1:123456789:function:SecretsRotation \
  --rotation-rules AutomaticallyAfterDays=30
```

### Handling Rotation in Code

```csharp
public class RotationAwareService
{
    private readonly ICredentialStore _store;
    private SecureString? _cachedSecret;
    private DateTime _cacheExpiry;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<SecureString> GetSecretAsync(CancellationToken ct)
    {
        if (_cachedSecret != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedSecret;

        _cachedSecret = await _store.GetCredentialAsync("my-secret", ct);
        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);

        return _cachedSecret ?? throw new InvalidOperationException("Secret not found");
    }

    public void InvalidateCache()
    {
        _cachedSecret = null;
        _cacheExpiry = DateTime.MinValue;
    }
}
```

## Security Best Practices

### DO

- Use IAM roles instead of access keys in production
- Limit secret access with resource-based policies
- Enable CloudTrail for audit logging
- Use VPC endpoints to keep traffic private
- Implement short cache durations

### DON'T

- Store access keys in code or config files
- Use overly permissive IAM policies
- Log secret values
- Share secrets across environments

## Troubleshooting

### "Access Denied" Errors

1. Check IAM permissions:
   ```bash
   aws sts get-caller-identity
   aws secretsmanager get-secret-value --secret-id dispatch-test
   ```

2. Verify secret exists and matches name pattern

### LocalStack Connection Issues

1. Verify LocalStack is running:
   ```bash
   docker ps | grep localstack
   curl http://localhost:4566/_localstack/health
   ```

2. Check port mapping (4566)

3. Ensure `AWS:ServiceURL` is set correctly

## Related Samples

- [Azure Key Vault Sample](../AzureKeyVault/) - Azure equivalent
- [Message Encryption Sample](../MessageEncryption/) - Encrypt message payloads
- [Audit Logging Sample](../AuditLogging/) - Security audit trails

## Learn More

- [AWS Secrets Manager Documentation](https://docs.aws.amazon.com/secretsmanager/)
- [LocalStack](https://localstack.cloud/)
- [IAM Best Practices](https://docs.aws.amazon.com/IAM/latest/UserGuide/best-practices.html)
