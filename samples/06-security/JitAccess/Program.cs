// JIT Access Sample
// Demonstrates requesting temporary (just-in-time) access with automatic expiry.
// The JitAccessExpiryService would normally auto-revoke expired grants as a BackgroundService.

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores + Governance with Provisioning + JIT
builder.Services.AddExcaliburA3Core()
	.AddGovernance(g => g
		.AddProvisioning(
			provisioning =>
			{
				provisioning.EnableJitAccess = true;
				provisioning.RequireRiskAssessment = false;
			},
			jit =>
			{
				jit.DefaultJitDuration = TimeSpan.FromHours(4);
				jit.MaxJitDuration = TimeSpan.FromHours(8);
				jit.ExpiryCheckInterval = TimeSpan.FromMinutes(1);
			}));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var provisioningStore = scope.ServiceProvider.GetRequiredService<IProvisioningStore>();
var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();

Console.WriteLine("=== Excalibur A3 JIT Access Sample ===");
Console.WriteLine();

// 1. Create a JIT provisioning request with expiry
Console.WriteLine("--- Step 1: Request Temporary Access ---");
var requestId = "jit-001";
var userId = "engineer-dave";
var grantScope = "Production.Admin";
var grantType = "ActivityGroup";
var expiry = DateTimeOffset.UtcNow.AddHours(4);

Console.WriteLine($"  User '{userId}' requests JIT access to '{grantScope}'");
Console.WriteLine($"  Requested expiry: {expiry:yyyy-MM-dd HH:mm} UTC ({4} hours)");

var step = new ApprovalStep(
	StepId: "step-1",
	ApproverRole: "SecurityOfficer",
	Outcome: null,
	Justification: null,
	DecidedAt: null,
	DecidedBy: null);

var summary = new ProvisioningRequestSummary(
	RequestId: requestId,
	UserId: userId,
	GrantScope: grantScope,
	GrantType: grantType,
	Status: ProvisioningRequestStatus.InReview,
	IdempotencyKey: $"jit-{userId}-{grantScope}",
	RiskScore: 0,
	RequestedBy: userId,
	CreatedAt: DateTimeOffset.UtcNow,
	ApprovalSteps: [step],
	TenantId: "tenant-prod",
	RequestedExpiry: expiry);

await provisioningStore.SaveRequestAsync(summary, CancellationToken.None);
Console.WriteLine($"  Request '{requestId}' submitted with JIT expiry");

// 2. Approve the request
Console.WriteLine();
Console.WriteLine("--- Step 2: Security Officer Approves ---");
var approvedStep = step with
{
	Outcome = ApprovalOutcome.Approved,
	DecidedBy = "security-officer-eve",
	Justification = "Emergency production access - incident #42",
	DecidedAt = DateTimeOffset.UtcNow,
};

var approved = summary with
{
	Status = ProvisioningRequestStatus.Approved,
	ApprovalSteps = [approvedStep],
};
await provisioningStore.SaveRequestAsync(approved, CancellationToken.None);
Console.WriteLine($"  Approved by {approvedStep.DecidedBy}: '{approvedStep.Justification}'");

// 3. Create the temporary grant with expiry
Console.WriteLine();
Console.WriteLine("--- Step 3: Create Temporary Grant ---");
var grant = new Grant(
	UserId: userId,
	FullName: "Dave Engineer",
	TenantId: "tenant-prod",
	GrantType: grantType,
	Qualifier: grantScope,
	ExpiresOn: expiry,
	GrantedBy: "JitProvisioning",
	GrantedOn: DateTimeOffset.UtcNow);

await grantStore.SaveGrantAsync(grant, CancellationToken.None);
Console.WriteLine($"  Temporary grant created: {userId} -> {grantScope}");
Console.WriteLine($"  Expires at: {expiry:yyyy-MM-dd HH:mm} UTC");

var provisioned = approved with { Status = ProvisioningRequestStatus.Provisioned };
await provisioningStore.SaveRequestAsync(provisioned, CancellationToken.None);

// 4. Verify the grant exists
Console.WriteLine();
Console.WriteLine("--- Step 4: Verify JIT Grant ---");
var exists = await grantStore.GrantExistsAsync(userId, "tenant-prod", grantType, grantScope, CancellationToken.None);
Console.WriteLine($"  Grant exists now: {exists}");
Console.WriteLine($"  Time remaining: ~{(expiry - DateTimeOffset.UtcNow).TotalHours:F1} hours");

// 5. Explain auto-revoke behavior
Console.WriteLine();
Console.WriteLine("--- Step 5: Auto-Revoke Behavior ---");
Console.WriteLine("  In production, JitAccessExpiryService (BackgroundService) would:");
Console.WriteLine($"    - Check every {1} minute(s) for expired JIT grants");
Console.WriteLine($"    - Revoke this grant after {expiry:yyyy-MM-dd HH:mm} UTC");
Console.WriteLine("    - Log the revocation for audit trail");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
