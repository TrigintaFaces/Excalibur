# Security Best Practices

**Framework:** Excalibur.Dispatch
**Focus:** Production security hardening
**Last Updated:** 2026-01-01

---

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A deployed or staging Excalibur application
- Familiarity with [encryption providers](../security/encryption-providers.md) and [authorization](../security/authorization.md)

## Overview

Comprehensive security guidance for deploying Excalibur applications in production environments.

**Security layers covered:**
- Authentication and authorization
- Secrets management
- Network security
- Data protection
- Infrastructure hardening
- Monitoring and auditing

---

## Authentication and Authorization

### JWT Bearer Authentication

```csharp
// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-identity-provider.com";
        options.Audience = "your-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero  // No clock skew tolerance
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("RequireOrderWritePermission", policy =>
        policy.RequireClaim("permissions", "orders:write"));
});

app.UseAuthentication();
app.UseAuthorization();
```

### Excalibur Permission-Based Authorization

```csharp
using Excalibur.A3.Authorization;

// Define protected action
public class CreateOrderAction : IDispatchAction<Order>
{
    [RequirePermission("orders.create")]
    public string CustomerId { get; set; }

    public List<OrderItem> Items { get; set; }
}

// Handler with automatic permission check
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Order>
{
    public async Task<Order> HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        // Permission already validated by framework
        var order = new Order(action.CustomerId, action.Items);
        // ... save order
        return order;
    }
}
```

### API Key Authentication (Simple Scenarios)

```csharp
// Middleware for API key validation
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key missing");
            return;
        }

        var apiKey = configuration["ApiKey"];
        if (!apiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }

        await _next(context);
    }
}

// Register middleware
app.UseMiddleware<ApiKeyMiddleware>();
```

**⚠️ Important:** API keys should be stored in secure secret management (Azure Key Vault, AWS Secrets Manager, etc.), never hardcoded.

---

## Secrets Management

### Azure Key Vault Integration

```csharp
// Program.cs
using Azure.Identity;

if (builder.Environment.IsProduction())
{
    var keyVaultName = builder.Configuration["KeyVaultName"];
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

// Usage - secrets loaded automatically
var connectionString = builder.Configuration["SqlConnectionString"];
var apiKey = builder.Configuration["ExternalApiKey"];
```

**Managed Identity setup:**

```bash
# Enable system-assigned managed identity
az webapp identity assign --name your-app --resource-group your-rg

# Grant Key Vault access
az keyvault set-policy \
  --name your-keyvault \
  --object-id <principal-id> \
  --secret-permissions get list
```

### AWS Secrets Manager Integration

```csharp
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

public static class SecretsManagerExtensions
{
    public static void AddAwsSecretsManager(
        this IConfigurationBuilder config,
        string region)
    {
        var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
        config.Add(new SecretsManagerConfigurationSource(client));
    }
}

// Usage
if (builder.Environment.IsProduction())
{
    builder.Configuration.AddAwsSecretsManager("us-east-1");
}
```

### Google Cloud Secret Manager Integration

```csharp
using Google.Cloud.SecretManager.V1;

public static string GetSecret(string projectId, string secretId)
{
    var client = SecretManagerServiceClient.Create();
    var secretVersionName = new SecretVersionName(projectId, secretId, "latest");

    var response = client.AccessSecretVersion(secretVersionName);
    return response.Payload.Data.ToStringUtf8();
}

// Usage
var connectionString = GetSecret("your-project", "sql-connection-string");
```

### Environment Variables (Development/Kubernetes)

```csharp
// NEVER commit secrets to source control
// Use environment variables or user secrets for development

// Development: User Secrets
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;..."

// Production: Environment variables (Kubernetes secret)
var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
```

---

## HTTPS and TLS

### Enforce HTTPS

```csharp
// Program.cs
if (app.Environment.IsProduction())
{
    app.UseHsts();  // HTTP Strict Transport Security
    app.UseHttpsRedirection();
}

// Configure HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
```

### TLS Configuration (Kestrel)

```json
// appsettings.Production.json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/etc/ssl/certs/your-cert.pfx",
          "Password": "<use-secret-manager>"
        }
      }
    },
    "Limits": {
      "MinRequestBodyDataRate": {
        "BytesPerSecond": 100,
        "GracePeriod": "00:00:05"
      }
    }
  }
}
```

### Certificate from Azure Key Vault

```csharp
using Azure.Security.KeyVault.Certificates;
using Azure.Identity;

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.ConfigureHttpsDefaults(listenOptions =>
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        var client = new CertificateClient(keyVaultUri, new DefaultAzureCredential());

        var certificate = client.DownloadCertificate("your-cert-name");
        listenOptions.ServerCertificate = certificate.Value;
    });
});
```

---

## Data Protection

### ASP.NET Core Data Protection

```csharp
// Program.cs
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(new Uri("https://..."))
    .ProtectKeysWithAzureKeyVault(new Uri("https://..."), new DefaultAzureCredential())
    .SetApplicationName("YourApp");
```

### Encryption at Rest (Excalibur.Dispatch)

```csharp
using Excalibur.Dispatch.Compliance;

// Configure encryption with Azure Key Vault
builder.Services.AddAzureKeyVaultKeyManagement(options =>
{
    options.VaultUri = new Uri(builder.Configuration["KeyVault:Uri"]!);
});

// Annotate sensitive data
public class Customer
{
    public Guid Id { get; set; }

    [PersonalData]  // Automatically encrypted
    public string Email { get; set; }

    [PersonalData]
    [Sensitive]  // Higher security classification
    public string CreditCardNumber { get; set; }
}
```

### Encryption in Transit (Database)

```json
// SQL Server - Enforce encrypted connections
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=...;Encrypt=true;TrustServerCertificate=false;"
  }
}
```

---

## Input Validation and Sanitization

### Command Validation

```csharp
using FluentValidation;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .Must(BeValidGuid).WithMessage("Invalid customer ID format");

        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count <= 100).WithMessage("Maximum 100 items per order");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .LessThanOrEqualTo(10000);

            item.RuleFor(x => x.ProductId)
                .NotEmpty()
                .Must(BeValidGuid);
        });
    }

    private bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}

// Register validators
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();

// Dispatch with validation pipeline
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddPipeline("default", pipeline => pipeline.UseValidation());
});
```

### SQL Injection Prevention

```csharp
// GOOD: Parameterized queries (Dapper)
var orders = await connection.QueryAsync<Order>(
    "SELECT * FROM Orders WHERE CustomerId = @CustomerId",
    new { CustomerId = customerId });

// BAD: String concatenation (NEVER DO THIS)
// var sql = $"SELECT * FROM Orders WHERE CustomerId = '{customerId}'";
```

### XSS Prevention

```csharp
// ASP.NET Core automatically encodes output
// Use HtmlEncoder for manual encoding
using System.Text.Encodings.Web;

var encoder = HtmlEncoder.Default;
var safeOutput = encoder.Encode(userInput);
```

---

## Rate Limiting and Throttling

### ASP.NET Core Rate Limiting (.NET 7+)

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    // Fixed window: 100 requests per minute
    options.AddFixedWindowLimiter("fixed", options =>
    {
        options.PermitLimit = 100;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 10;
    });

    // Sliding window: 1000 requests per hour
    options.AddSlidingWindowLimiter("sliding", options =>
    {
        options.PermitLimit = 1000;
        options.Window = TimeSpan.FromHours(1);
        options.SegmentsPerWindow = 12;  // 5-minute segments
    });

    // Token bucket: Burst handling
    options.AddTokenBucketLimiter("token", options =>
    {
        options.TokenLimit = 100;
        options.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        options.TokensPerPeriod = 10;
        options.AutoReplenishment = true;
    });

    // Concurrent requests limit
    options.AddConcurrencyLimiter("concurrency", options =>
    {
        options.PermitLimit = 100;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 50;
    });
});

app.UseRateLimiter();

// Apply to endpoints
app.MapPost("/orders", CreateOrder)
   .RequireRateLimiting("fixed");
```

### Custom Rate Limiting with Redis

```csharp
using StackExchange.Redis;

public class RedisRateLimiter
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window)
    {
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        // Remove old entries
        await db.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

        // Count requests in window
        var count = await db.SortedSetLengthAsync(key);

        if (count < maxRequests)
        {
            // Add current request
            await db.SortedSetAddAsync(key, now, now);
            await db.KeyExpireAsync(key, window);
            return true;
        }

        return false;
    }
}
```

---

## CORS Configuration

### Strict CORS Policy

```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionPolicy", policy =>
    {
        policy.WithOrigins(
                "https://yourdomain.com",
                "https://app.yourdomain.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    // Development only
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("DevelopmentPolicy", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    }
});

app.UseCors(builder.Environment.IsProduction() ? "ProductionPolicy" : "DevelopmentPolicy");
```

---

## Security Headers

### Essential Security Headers

```csharp
app.Use(async (context, next) =>
{
    // Prevent clickjacking
    context.Response.Headers.Add("X-Frame-Options", "DENY");

    // Prevent MIME sniffing
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");

    // XSS protection (legacy, but still useful)
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

    // Content Security Policy
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'");

    // Referrer policy
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

    // Permissions policy
    context.Response.Headers.Add("Permissions-Policy",
        "geolocation=(), microphone=(), camera=()");

    await next();
});
```

### Using NWebsec Package

```csharp
using NWebsec.AspNetCore.Mvc;

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

app.UseXContentTypeOptions();
app.UseReferrerPolicy(opts => opts.StrictOriginWhenCrossOrigin());
app.UseXXssProtection(options => options.EnabledWithBlockMode());
app.UseXfo(options => options.Deny());

app.UseCsp(opts => opts
    .DefaultSources(s => s.Self())
    .ScriptSources(s => s.Self().UnsafeInline())
    .StyleSources(s => s.Self().UnsafeInline())
    .ImageSources(s => s.Self().CustomSources("data:", "https:"))
    .FontSources(s => s.Self().CustomSources("data:"))
    .ConnectSources(s => s.Self())
    .FrameAncestors(s => s.None()));
```

---

## Audit Logging

### Excalibur Audit Logging

```csharp
// Configure audit logging with persistent store for production
builder.Services.AddSqlServerAuditStore(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Compliance");
    options.SchemaName = "audit";
    options.EnableHashChain = true;
});

// Audit critical operations
public class OrderService
{
    private readonly IAuditLogger _auditLogger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<Order> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = await _orderRepository.CreateAsync(command, ct);

        await _auditLogger.LogAsync(new AuditEvent
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = AuditEventType.DataModification,
            Action = "OrderCreated",
            ActorId = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value ?? "unknown",
            ResourceId = order.Id.ToString(),
            ResourceType = "Order",
            Timestamp = DateTimeOffset.UtcNow,
            Outcome = AuditOutcome.Success,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["OrderTotal"] = order.Total.ToString(),
                ["CustomerId"] = order.CustomerId.ToString()
            }
        }, ct);

        return order;
    }
}
```

### Security Event Logging

```csharp
// Log authentication failures
public class AuthenticationEventsHandler
{
    private readonly IAuditLogger _auditLogger;

    public async Task OnAuthenticationFailed(AuthenticationFailedContext context)
    {
        await _auditLogger.LogAsync(new AuditEvent
        {
            EventType = "AuthenticationFailed",
            Timestamp = DateTime.UtcNow,
            Outcome = "Failure",
            ClientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["Reason"] = context.Exception.Message
            }
        });
    }
}
```

---

## Container Security

### Docker Security Best Practices

```dockerfile
# Use minimal base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime

# Run as non-root user
RUN addgroup -g 1000 appuser && \
    adduser -u 1000 -G appuser -s /bin/sh -D appuser

# Set ownership
COPY --from=publish /app/publish .
RUN chown -R appuser:appuser /app

# Drop to non-root user
USER appuser

# Read-only root filesystem (if possible)
# docker run --read-only --tmpfs /tmp your-image
```

### Kubernetes Security Context

```yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000
        seccompProfile:
          type: RuntimeDefault
      containers:
      - name: app
        securityContext:
          allowPrivilegeEscalation: false
          capabilities:
            drop:
            - ALL
          readOnlyRootFilesystem: true
        volumeMounts:
        - name: tmp
          mountPath: /tmp
      volumes:
      - name: tmp
        emptyDir: {}
```

---

## Dependency Scanning

### NuGet Package Vulnerabilities

```bash
# Install dotnet list package tool
dotnet tool install --global dotnet-outdated-tool

# Check for vulnerable packages
dotnet list package --vulnerable

# Check for outdated packages
dotnet outdated

# Update packages
dotnet add package PackageName
```

### GitHub Dependabot

```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10
    reviewers:
      - "security-team"
    labels:
      - "dependencies"
      - "security"
```

---

## Network Security

### Firewall Rules (Kubernetes Network Policies)

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: api-network-policy
spec:
  podSelector:
    matchLabels:
      app: api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          role: frontend
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: database
    ports:
    - protocol: TCP
      port: 5432
  - to:
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 53  # DNS
```

### Azure Application Gateway WAF

```bash
# Enable WAF
az network application-gateway waf-config set \
  --gateway-name your-gateway \
  --resource-group your-rg \
  --enabled true \
  --firewall-mode Prevention \
  --rule-set-type OWASP \
  --rule-set-version 3.2
```

---

## Penetration Testing Checklist

### Pre-Deployment Security Checklist

- [ ] Authentication enabled (JWT/OAuth2)
- [ ] Authorization enforced on all endpoints
- [ ] HTTPS/TLS configured
- [ ] Secrets stored in secret management (never in code/config)
- [ ] Input validation on all user inputs
- [ ] SQL parameterization (no string concatenation)
- [ ] Rate limiting configured
- [ ] CORS policy restricted to known domains
- [ ] Security headers configured
- [ ] Audit logging enabled for critical operations
- [ ] Dependencies scanned for vulnerabilities
- [ ] Container running as non-root user
- [ ] Network policies configured (Kubernetes)
- [ ] Data encryption at rest enabled
- [ ] Database connections encrypted
- [ ] Error messages don't expose sensitive information
- [ ] Default credentials changed
- [ ] Unnecessary endpoints disabled
- [ ] Health check endpoints don't expose sensitive data
- [ ] API versioning implemented
- [ ] Request size limits configured

---

## Incident Response

### Security Incident Logging

```csharp
public class SecurityIncidentLogger
{
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<SecurityIncidentLogger> _logger;

    public async Task LogSecurityIncidentAsync(string incidentType, string description)
    {
        _logger.LogCritical(
            "SECURITY INCIDENT: {IncidentType} - {Description}",
            incidentType,
            description);

        await _auditLogger.LogAsync(new AuditEvent
        {
            EventType = $"SecurityIncident.{incidentType}",
            Timestamp = DateTime.UtcNow,
            Outcome = "Alert",
            Metadata = new Dictionary<string, string>
            {
                ["Description"] = description,
                ["Severity"] = "Critical"
            }
        });

        // Trigger alerts (PagerDuty, OpsGenie, etc.)
        await SendAlertAsync(incidentType, description);
    }
}
```

---

## Next Steps

- **Compliance:** [FedRAMP](../compliance/checklists/fedramp.md), [GDPR](../compliance/checklists/gdpr.md), [SOC 2](../compliance/checklists/soc2.md), [HIPAA](../compliance/checklists/hipaa.md)
- **Monitoring:** [Health Checks](../observability/health-checks.md)
- **Deployment:** [Docker](docker.md), [Kubernetes](kubernetes.md)

---

## See Also

- [Security Overview](../security/index.md) - Framework-level security features and configuration
- [Encryption Architecture](../security/encryption-architecture.md) - Data encryption at rest and in transit design
- [Deployment Overview](index.md) - Choose the right deployment target for your application

---

**Last Updated:** 2026-01-01
**Framework:** Excalibur 1.0.0
**OWASP Top 10:** 2021 Edition
