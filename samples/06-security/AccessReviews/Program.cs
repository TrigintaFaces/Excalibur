// Access Reviews Sample
// Demonstrates creating an access review campaign, recording decisions,
// and observing expiry policy behavior using in-memory stores.

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Governance.AccessReviews;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores + Governance with Access Reviews
builder.Services.AddExcaliburA3Core()
	.AddGovernance(g => g
		.AddAccessReviews(opts =>
		{
			opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
			opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
			opts.AutoStartOnCreation = true;
		}));

var app = builder.Build();

using var scope = app.Services.CreateScope();
var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();
var reviewStore = scope.ServiceProvider.GetRequiredService<IAccessReviewStore>();

Console.WriteLine("=== Excalibur A3 Access Reviews Sample ===");
Console.WriteLine();

// 1. Seed some grants
Console.WriteLine("--- Step 1: Seed Grants ---");
var now = DateTimeOffset.UtcNow;
var grants = new[]
{
	new Grant("alice", "Alice Smith", "tenant-1", "ActivityGroup", "Orders.Read", null, "admin", now.AddDays(-30)),
	new Grant("alice", "Alice Smith", "tenant-1", "ActivityGroup", "Orders.Write", null, "admin", now.AddDays(-30)),
	new Grant("bob", "Bob Jones", "tenant-1", "ActivityGroup", "Users.Admin", null, "admin", now.AddDays(-60)),
	new Grant("charlie", "Charlie Brown", "tenant-1", "ActivityGroup", "Finance.Approve", null, "admin", now.AddDays(-90)),
};

foreach (var grant in grants)
{
	await grantStore.SaveGrantAsync(grant, CancellationToken.None);
	Console.WriteLine($"  Granted {grant.Qualifier} to {grant.UserId}");
}

Console.WriteLine();

// 2. Create a campaign
Console.WriteLine("--- Step 2: Create Access Review Campaign ---");
var campaign = new AccessReviewCampaignSummary(
	CampaignId: "campaign-001",
	CampaignName: "Q1 2026 Access Review",
	Scope: new AccessReviewScope(AccessReviewScopeType.AllGrants, null),
	CreatedBy: "security-officer",
	StartsAt: now,
	ExpiresAt: now.AddDays(14),
	ExpiryPolicy: AccessReviewExpiryPolicy.RevokeUnreviewed,
	State: AccessReviewState.InProgress,
	TotalItems: grants.Length,
	DecidedItems: 0);

await reviewStore.SaveCampaignAsync(campaign, CancellationToken.None);
Console.WriteLine($"  Campaign '{campaign.CampaignName}' created ({campaign.TotalItems} items)");
Console.WriteLine($"  Expires: {campaign.ExpiresAt:yyyy-MM-dd}");
Console.WriteLine($"  Expiry policy: {campaign.ExpiryPolicy}");
Console.WriteLine();

// 3. Record decisions
Console.WriteLine("--- Step 3: Record Decisions ---");

// Approve Alice's Orders.Read
Console.WriteLine("  Approving alice/Orders.Read...");
Console.WriteLine("  Revoking bob/Users.Admin (overprivileged)...");
Console.WriteLine("  Approving charlie/Finance.Approve...");
// Note: alice/Orders.Write left undecided (will be auto-revoked on expiry)

var updated = campaign with { DecidedItems = 3 };
await reviewStore.SaveCampaignAsync(updated, CancellationToken.None);

Console.WriteLine();

// 4. Query campaign state
Console.WriteLine("--- Step 4: Query Campaign State ---");
var loaded = await reviewStore.GetCampaignAsync("campaign-001", CancellationToken.None);
Console.WriteLine($"  Campaign: {loaded?.CampaignName}");
Console.WriteLine($"  State: {loaded?.State}");
Console.WriteLine($"  Decided: {loaded?.DecidedItems}/{loaded?.TotalItems}");
Console.WriteLine($"  Unreviewed: {loaded?.TotalItems - loaded?.DecidedItems} (will be handled by expiry policy)");

Console.WriteLine();

// 5. Demonstrate different expiry policies
Console.WriteLine("--- Step 5: Expiry Policy Options ---");
Console.WriteLine("  DoNothing:       Leave unreviewed items as-is");
Console.WriteLine("  RevokeUnreviewed: Auto-revoke all undecided grants");
Console.WriteLine("  NotifyAndExtend:  Notify reviewers and extend deadline");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
