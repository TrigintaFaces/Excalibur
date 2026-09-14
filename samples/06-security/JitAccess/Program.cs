// JIT Access Sample
// Demonstrates requesting temporary (just-in-time) access with automatic expiry.
// The JitAccessExpiryService would normally auto-revoke expired grants as a BackgroundService.

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores + Governance with Provisioning + JIT
//
// AddExcaliburA3() is what a real host calls. Unlike AddExcaliburA3Core() -- the gate-free
// lightweight path -- it installs the grant-durability startup gate, which FAILS CLOSED when grants
// would live in a volatile store: grants lost on restart make a user whose grants vanished
// indistinguishable from one who never had any, so authorization silently denies everyone.
//
// This sample keeps the in-memory stores, so it must say so out loud. A production host registers a
// durable grant store instead and leaves this option alone.
builder.Services.Configure<GrantDurabilityOptions>(o => o.AllowVolatileGrantStore = true);

// Authorization caching needs a distributed cache that is partitioned per application. This is TWO
// registrations, not one: the scoped wrapper is keyed, and it wraps whichever unkeyed cache the
// container holds -- so a host that registers only the wrapper has nothing for it to wrap. Grant cache
// keys identify a user but not an application, so without the partition two applications sharing one
// cache server address the same entry for the same user and one serves the other's grants.
//
// A real host points the unkeyed registration at Redis or SQL Server; the in-memory one here is what
// makes this sample self-contained, and it is not a production choice.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddApplicationScopedDistributedCache(o => o.Scope = "jit-access-sample");

builder.Services.AddExcaliburA3()
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

// Run the startup gates NOW. This sample builds a host and never calls StartAsync(), so the gates a
// hosted run would fire never fire on their own -- the shape a console tool, a migration utility or a
// test fixture has. ValidateStartupGates() is the host-less trigger; without the opt-in above, this
// line is where the run would stop.
_ = app.Services.ValidateStartupGates();

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
