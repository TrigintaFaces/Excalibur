// Provisioning Workflow Sample
// Demonstrates creating a provisioning request, risk scoring, approval chain,
// and grant creation using in-memory stores.

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.Provisioning;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using ProvisioningWorkflow;

var builder = Host.CreateApplicationBuilder(args);

// Everything AddExcaliburA3 requires of the host but does not register itself: the application-scoped
// authorization cache, and this sample's opt-in to a volatile grant store. Both are explained on
// SampleHostComposition rather than here, so this file stays about the provisioning workflow.
builder.Services.AddSampleAuthorizationPrerequisites();

// Register A3 Core with in-memory stores + Governance with Provisioning
//
// AddExcaliburA3() is what a real host calls. Unlike AddExcaliburA3Core() -- the gate-free
// lightweight path -- it installs the grant-durability startup gate, which FAILS CLOSED when grants
// would live in a volatile store: grants lost on restart make a user whose grants vanished
// indistinguishable from one who never had any, so authorization silently denies everyone.
builder.Services.AddExcaliburA3()
	.AddGovernance(g => g
		.AddProvisioning(
			provisioning =>
			{
				provisioning.RequireRiskAssessment = true;
				provisioning.DefaultApprovalTimeout = TimeSpan.FromHours(48);
			}));

var app = builder.Build();

// Run the startup gates NOW. This sample builds a host and never calls StartAsync(), so the gates a
// hosted run would fire never fire on their own -- the shape a console tool, a migration utility or a
// test fixture has. ValidateStartupGates() is the host-less trigger; without the opt-in above, this
// line is where the run would stop.
_ = app.Services.ValidateStartupGates();

using var scope = app.Services.CreateScope();
var provisioningStore = scope.ServiceProvider.GetRequiredService<IProvisioningStore>();
var riskAssessor = scope.ServiceProvider.GetRequiredService<IGrantRiskAssessor>();
var workflowConfig = scope.ServiceProvider.GetRequiredService<IProvisioningWorkflowConfiguration>();
var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();

Console.WriteLine("=== Excalibur A3 Provisioning Workflow Sample ===");
Console.WriteLine();

// 1. Create a provisioning request
Console.WriteLine("--- Step 1: Create Provisioning Request ---");
var requestId = "prov-001";
var userId = "alice";
var grantScope = "Finance.Approve";
var grantType = "ActivityGroup";

Console.WriteLine($"  User '{userId}' requests access to '{grantScope}' ({grantType})");

// 2. Assess risk
Console.WriteLine();
Console.WriteLine("--- Step 2: Risk Assessment ---");
var riskScore = await riskAssessor.AssessRiskAsync(userId, grantScope, grantType, CancellationToken.None);
Console.WriteLine($"  Risk score: {riskScore}/100");

// 3. Get approval steps from workflow configuration
Console.WriteLine();
Console.WriteLine("--- Step 3: Determine Approval Steps ---");
var stepTemplates = await workflowConfig.GetApprovalStepsAsync(grantScope, grantType, riskScore, CancellationToken.None);
Console.WriteLine($"  {stepTemplates.Count} approval step(s) required:");

var steps = stepTemplates.Select((t, i) => new ApprovalStep(
	StepId: $"step-{i + 1}",
	ApproverRole: t.ApproverRole,
	Outcome: null,
	Justification: null,
	DecidedAt: null,
	DecidedBy: null)).ToList();

foreach (var step in steps)
{
	Console.WriteLine($"    - {step.StepId}: Approver role = {step.ApproverRole}");
}

// 4. Save the request
Console.WriteLine();
Console.WriteLine("--- Step 4: Submit Request ---");
var summary = new ProvisioningRequestSummary(
	RequestId: requestId,
	UserId: userId,
	GrantScope: grantScope,
	GrantType: grantType,
	Status: ProvisioningRequestStatus.InReview,
	IdempotencyKey: $"{userId}-{grantScope}-{grantType}",
	RiskScore: riskScore,
	RequestedBy: userId,
	CreatedAt: DateTimeOffset.UtcNow,
	ApprovalSteps: steps);

await provisioningStore.SaveRequestAsync(summary, CancellationToken.None);
Console.WriteLine($"  Request '{requestId}' submitted (status: InReview)");

// 5. Approve step 1
Console.WriteLine();
Console.WriteLine("--- Step 5: Approval Chain ---");
var approvedStep = steps[0] with
{
	Outcome = ApprovalOutcome.Approved,
	DecidedBy = "manager-bob",
	Justification = "Business need confirmed",
	DecidedAt = DateTimeOffset.UtcNow,
};

var updatedSteps = new List<ApprovalStep> { approvedStep };
var approved = summary with
{
	Status = ProvisioningRequestStatus.Approved,
	ApprovalSteps = updatedSteps,
};
await provisioningStore.SaveRequestAsync(approved, CancellationToken.None);
Console.WriteLine($"  Step '{approvedStep.StepId}' approved by {approvedStep.DecidedBy}");
Console.WriteLine($"  Request status: Approved");

// 6. Create the grant
Console.WriteLine();
Console.WriteLine("--- Step 6: Grant Creation ---");
var grant = new Grant(
	UserId: userId,
	FullName: "Alice Smith",
	TenantId: "tenant-1",
	GrantType: grantType,
	Qualifier: grantScope,
	ExpiresOn: null,
	GrantedBy: "ProvisioningWorkflow",
	GrantedOn: DateTimeOffset.UtcNow);

await grantStore.SaveGrantAsync(grant, CancellationToken.None);
Console.WriteLine($"  Grant created: {userId} -> {grantScope} ({grantType})");

// Mark request as provisioned
var provisioned = approved with { Status = ProvisioningRequestStatus.Provisioned };
await provisioningStore.SaveRequestAsync(provisioned, CancellationToken.None);
Console.WriteLine($"  Request status: Provisioned");

// 7. Verify
Console.WriteLine();
Console.WriteLine("--- Step 7: Verify ---");
var exists = await grantStore.GrantExistsAsync(userId, "tenant-1", grantType, grantScope, CancellationToken.None);
Console.WriteLine($"  Grant exists: {exists}");
var loaded = await provisioningStore.GetRequestAsync(requestId, CancellationToken.None);
Console.WriteLine($"  Request status: {loaded?.Status}");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
