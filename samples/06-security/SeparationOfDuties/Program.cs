// Separation of Duties Sample
// Demonstrates defining SoD policies, evaluating conflicts,
// and showing how the preventive middleware would block conflicting grants.

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Governance.SeparationOfDuties;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores + Governance with SoD
builder.Services.AddExcaliburA3Core()
	.AddGovernance(g => g
		.AddSeparationOfDuties(opts =>
		{
			opts.EnablePreventiveEnforcement = true;
			opts.MinimumEnforcementSeverity = SoDSeverity.Violation;
			opts.EnableDetectiveScanning = false; // Disabled for sample
		}));

var app = builder.Build();

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
	new Excalibur.A3.Abstractions.Authorization.Grant("user-1", "Jane Doe", "tenant-1", GrantType.Role, "PaymentCreator", null, "admin", now),
	CancellationToken.None);
Console.WriteLine("  Granted PaymentCreator to user-1");

await grantStore.SaveGrantAsync(
	new Excalibur.A3.Abstractions.Authorization.Grant("user-1", "Jane Doe", "tenant-1", GrantType.Role, "FinanceManager", null, "admin", now),
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
