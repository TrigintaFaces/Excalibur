// Separation of Duties Sample
// Demonstrates defining SoD policies, evaluating conflicts,
// and showing how the preventive middleware would block conflicting grants.

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores + Governance with SoD
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
// cache server address the same entry for the same user and one serves the other's grants. The scope
// below is what keeps them apart, which is why every sample here uses a different one.
//
// A real host points the unkeyed registration at Redis or SQL Server; the in-memory one here is what
// makes this sample self-contained, and it is not a production choice.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddApplicationScopedDistributedCache(o => o.Scope = "separation-of-duties-sample");

builder.Services.AddExcaliburA3()
	.AddGovernance(g => g
		.AddSeparationOfDuties(opts =>
		{
			opts.EnablePreventiveEnforcement = true;
			opts.MinimumEnforcementSeverity = SoDSeverity.Violation;
			opts.EnableDetectiveScanning = false; // Disabled for sample
		}));

var app = builder.Build();

// Run the startup gates NOW. This sample builds a host and never calls StartAsync(), so the gates a
// hosted run would fire never fire on their own -- the shape a console tool, a migration utility or a
// test fixture has. ValidateStartupGates() is the host-less trigger; without the opt-in above, this
// line is where the run would stop.
_ = app.Services.ValidateStartupGates();

using var scope = app.Services.CreateScope();
var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();
var policyStore = scope.ServiceProvider.GetRequiredService<ISoDPolicyStore>();
var evaluator = scope.ServiceProvider.GetRequiredService<ISoDEvaluator>();

Console.WriteLine("=== Excalibur A3 Separation of Duties Sample ===");
Console.WriteLine();

// 1. Define SoD policies
Console.WriteLine("--- Step 1: Define SoD Policies ---");

var policies = new[]
{
	new SoDPolicy(
		PolicyId: "sod-001",
		Name: "Payment Maker/Checker",
		Description: "Users cannot both create and approve payments",
		Severity: SoDSeverity.Critical,
		PolicyScope: SoDPolicyScope.Role,
		ConflictingItems: ["PaymentCreator", "PaymentApprover"],
		TenantId: null,
		CreatedBy: "compliance-officer"),
	new SoDPolicy(
		PolicyId: "sod-002",
		Name: "Finance/Audit Separation",
		Description: "Finance and audit roles must be held by different people",
		Severity: SoDSeverity.Violation,
		PolicyScope: SoDPolicyScope.Role,
		ConflictingItems: ["FinanceManager", "InternalAuditor", "ExternalAuditor"],
		TenantId: null,
		CreatedBy: "compliance-officer"),
};

foreach (var policy in policies)
{
	await policyStore.SavePolicyAsync(policy, CancellationToken.None);
	Console.WriteLine($"  Policy '{policy.Name}' ({policy.Severity}): {string.Join(" vs ", policy.ConflictingItems)}");
}

Console.WriteLine();

// 2. Grant roles to a user
Console.WriteLine("--- Step 2: Assign Role Grants ---");
var now = DateTimeOffset.UtcNow;

await grantStore.SaveGrantAsync(
	new Excalibur.A3.Authorization.Grant("user-1", "Jane Doe", "tenant-1", GrantType.Role, "PaymentCreator", null, "admin", now),
	CancellationToken.None);
Console.WriteLine("  Granted PaymentCreator to user-1");

await grantStore.SaveGrantAsync(
	new Excalibur.A3.Authorization.Grant("user-1", "Jane Doe", "tenant-1", GrantType.Role, "FinanceManager", null, "admin", now),
	CancellationToken.None);
Console.WriteLine("  Granted FinanceManager to user-1");

Console.WriteLine();

// 3. Evaluate current state (detective mode)
Console.WriteLine("--- Step 3: Evaluate Current Grants (Detective) ---");
var currentConflicts = await evaluator.EvaluateCurrentAsync("user-1", CancellationToken.None);
Console.WriteLine($"  Conflicts found: {currentConflicts.Count}");
foreach (var c in currentConflicts)
{
	Console.WriteLine($"  [{c.Severity}] Policy '{c.PolicyId}': {c.ConflictingItem1} vs {c.ConflictingItem2}");
}

Console.WriteLine();

// 4. Hypothetical evaluation (preventive mode)
Console.WriteLine("--- Step 4: Evaluate Hypothetical Grant (Preventive) ---");
Console.WriteLine("  Checking: What if we also grant PaymentApprover to user-1?");

var hypotheticalConflicts = await evaluator.EvaluateHypotheticalAsync(
	"user-1", "PaymentApprover", GrantType.Role, CancellationToken.None);

Console.WriteLine($"  Conflicts if granted: {hypotheticalConflicts.Count}");
foreach (var c in hypotheticalConflicts)
{
	Console.WriteLine($"  [{c.Severity}] BLOCKED by policy '{c.PolicyId}': {c.ConflictingItem1} vs {c.ConflictingItem2}");
}

Console.WriteLine();

// 5. Show non-conflicting grant
Console.WriteLine("--- Step 5: Non-conflicting Grant Check ---");
Console.WriteLine("  Checking: What if we grant 'Viewer' role to user-1?");

var safeConflicts = await evaluator.EvaluateHypotheticalAsync(
	"user-1", "Viewer", GrantType.Role, CancellationToken.None);

Console.WriteLine($"  Conflicts: {safeConflicts.Count} (safe to grant)");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
