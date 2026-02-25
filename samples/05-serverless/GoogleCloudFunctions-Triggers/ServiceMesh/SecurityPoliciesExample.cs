// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.ServiceMesh;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.Serverless.GoogleCloudFunctions.ServiceMesh
 /// <summary>
 /// Example demonstrating security policies in service mesh.
 /// Shows how to implement mTLS, authorization policies, and secure service-to-service communication.
 /// </summary>
 public class SecurityPoliciesExample {
 public static async Task Main(string[] args)
 {
 var host = Host.CreateDefaultBuilder(args)
 .ConfigureServices((context, services) =>
 {
 // Configure Google Cloud Functions
 services.AddGoogleCloudFunctions()
 .AddHttpFunction<SecureApiFunction>();

 // Configure service mesh with advanced security
 services.AddCloudRunServiceMesh(options =>
 {
 options.ServiceName = "secure-api";
 options.Namespace = "production";
 options.Version = "v1";

 // Enable strict mTLS
 options.EnableMTLS = true;
 options.MTLSMode = MTLSMode.Strict;

 // Configure certificate management
 options.Sidecar.CertificateRotationInterval = TimeSpan.FromDays(7);
 options.Sidecar.CertificateValidityPeriod = TimeSpan.FromDays(30);

 // Enable authorization policies
 options.Sidecar.EnableAuthorizationPolicies = true;
 });

 // Configure security policies
 services.AddHostedService<SecurityPolicyConfigurationService>();
 })
 .Build();

 await host.RunAsync();
 }
 }

 /// <summary>
 /// Background service that configures security policies.
 /// </summary>
 public class SecurityPolicyConfigurationService : IHostedService
 {
 private readonly ISecurityManager _securityManager;
 private readonly ILogger<SecurityPolicyConfigurationService> _logger;

 public SecurityPolicyConfigurationService(
 ISecurityManager securityManager,
 ILogger<SecurityPolicyConfigurationService> logger)
 {
 _securityManager = securityManager;
 _logger = logger;
 }

 public async Task StartAsync(CancellationToken cancellationToken)
 {
 _logger.LogInformation("Configuring security policies");

 // Configure namespace-level mTLS
 await _securityManager.ConfigureMTLSAsync(
 "secure-api",
 "production",
 MTLSMode.Strict,
 cancellationToken);

 // Configure authorization policies
 await ConfigureAuthorizationPolicies(cancellationToken);

 // Configure API key validation
 await ConfigureApiKeyValidation(cancellationToken);

 // Configure rate limiting
 await ConfigureRateLimiting(cancellationToken);
 }

 private async Task ConfigureAuthorizationPolicies(CancellationToken cancellationToken)
 {
 // Allow only specific services to call this API
 var authPolicy = new AuthorizationPolicy
 {
 Name = "secure-api-auth",
 Rules = new List<AuthorizationRule>
 {
 new AuthorizationRule
 {
 // Allow calls from order-service
 From = new List<AuthorizationSource>
 {
 new AuthorizationSource
 {
 Principals = new[] { "cluster.local/ns/production/sa/order-service" }
 }
 },
 To = new List<AuthorizationOperation>
 {
 new AuthorizationOperation
 {
 Methods = new[] { "POST" },
 Paths = new[] { "/api/secure/*" }
 }
 }
 },
 new AuthorizationRule
 {
 // Allow calls from payment-service with specific headers
 From = new List<AuthorizationSource>
 {
 new AuthorizationSource
 {
 Principals = new[] { "cluster.local/ns/production/sa/payment-service" }
 }
 },
 To = new List<AuthorizationOperation>
 {
 new AuthorizationOperation
 {
 Methods = new[] { "GET", "POST" },
 Paths = new[] { "/api/payments/*" }
 }
 },
 When = new List<AuthorizationCondition>
 {
 new AuthorizationCondition
 {
 Key = "request.headers[x-api-version]",
 Values = new[] { "v1", "v2" }
 }
 }
 }
 }
 };

 await _securityManager.ApplyAuthorizationPolicyAsync(authPolicy, cancellationToken);
 }

 private async Task ConfigureApiKeyValidation(CancellationToken cancellationToken)
 {
 var apiKeyPolicy = new ApiKeyValidationPolicy
 {
 Name = "api-key-validation",
 HeaderName = "X-API-Key",
 RequiredScopes = new[] { "read", "write" },
 ValidationEndpoint = "https://auth.example.com/validate"
 };

 await _securityManager.ApplyApiKeyPolicyAsync(apiKeyPolicy, cancellationToken);
 }

 private async Task ConfigureRateLimiting(CancellationToken cancellationToken)
 {
 var rateLimitPolicy = new RateLimitPolicy
 {
 Name = "api-rate-limit",
 Rules = new List<RateLimitRule>
 {
 new RateLimitRule
 {
 Descriptor = "per-user",
 Limit = 100,
 Window = TimeSpan.FromMinutes(1),
 KeyExtractor = "request.headers[x-user-id]"
 },
 new RateLimitRule
 {
 Descriptor = "per-ip",
 Limit = 1000,
 Window = TimeSpan.FromMinutes(1),
 KeyExtractor = "connection.remote_address"
 }
 }
 };

 await _securityManager.ApplyRateLimitPolicyAsync(rateLimitPolicy, cancellationToken);
 }

 public Task StopAsync(CancellationToken cancellationToken)
 {
 _logger.LogInformation("Security policy configuration service stopping");
 return Task.CompletedTask;
 }
 }

 /// <summary>
 /// Secure API function with authentication and authorization.
 /// </summary>
 public class SecureApiFunction : GoogleCloudFunctionBase
 {
 private readonly ILogger<SecureApiFunction> _logger;

 public SecureApiFunction(ILogger<SecureApiFunction> logger)
 {
 _logger = logger;
 }

 public override async Task<GoogleCloudFunctionResult> ExecuteAsync(GoogleCloudFunctionRequest request, GoogleCloudFunctionExecutionContext context)
 {
 _logger.LogInformation("Processing secure API request");

 // Extract authentication information from headers
 var principal = ExtractPrincipal(request);
 if (principal == null)
 {
 return GoogleCloudFunctionResult.Unauthorized("Authentication required");
 }

 // Log the authenticated caller
 _logger.LogInformation($"Authenticated request from: {principal.Identity?.Name}");

 try
 {
 // Process the secure operation
 var data = await request.ReadAsJsonAsync<SecureData>();

 // Validate authorization
 if (!await IsAuthorized(principal, data.Operation))
 {
 return GoogleCloudFunctionResult.Forbidden("Insufficient permissions");
 }

 // Process the secure operation
 var result = await ProcessSecureOperation(data);

 return GoogleCloudFunctionResult.Ok(result);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error processing secure request");
 return GoogleCloudFunctionResult.Error("Secure operation failed");
 }
 }

 private ClaimsPrincipal? ExtractPrincipal(GoogleCloudFunctionRequest request)
 {
 // In a service mesh, the proxy injects identity headers
 if (request.Headers.TryGetValue("X-Forwarded-Client-Cert", out var clientCert))
 {
 // Parse the client certificate information
 var claims = new List<Claim>
 {
 new Claim(ClaimTypes.Name, ExtractServiceName(clientCert)),
 new Claim("namespace", ExtractNamespace(clientCert))
 };

 var identity = new ClaimsIdentity(claims, "mtls");
 return new ClaimsPrincipal(identity);
 }

 return null;
 }

 private string ExtractServiceName(string clientCert)
 {
 // Simplified extraction - in real implementation, parse the certificate
 return "order-service";
 }

 private string ExtractNamespace(string clientCert)
 {
 // Simplified extraction
 return "production";
 }

 private async Task<bool> IsAuthorized(ClaimsPrincipal principal, string operation)
 {
 // Implement authorization logic
 var serviceName = principal.Identity?.Name;

 return operation switch
 {
 "read" => true, // All authenticated services can read
 "write" => serviceName == "order-service", // Only order-service can write
 _ => false
 };
 }

 private async Task<SecureOperationResult> ProcessSecureOperation(SecureData data)
 {
 await Task.Delay(100); // Simulate processing

 return new SecureOperationResult
 {
 Id = Guid.NewGuid().ToString(),
 Status = "Completed",
 Operation = data.Operation,
 ProcessedAt = DateTime.UtcNow
 };
 }
 }

 public class SecureData {
 public string? Operation { get; set; }
 public Dictionary<string, object>? Parameters { get; set; }
 }

 public class SecureOperationResult {
 public string? Id { get; set; }
 public string? Status { get; set; }
 public string? Operation { get; set; }
 public DateTime ProcessedAt { get; set; }
 }
}
