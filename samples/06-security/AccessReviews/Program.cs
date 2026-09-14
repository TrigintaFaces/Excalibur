// Access Reviews Sample
// Demonstrates creating an access review campaign, recording decisions,
// and observing expiry policy behavior using in-memory stores.

using Excalibur.A3.Authorization;
using Excalibur.A3.Governance.AccessReviews;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register the PRODUCTION A3 composition + Governance with Access Reviews.
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
builder.Services.AddApplicationScopedDistributedCache(o => o.Scope = "access-reviews-sample");

builder.Services.AddExcaliburA3()
	.AddGovernance(g => g
		.AddAccessReviews(opts =>
		{
			opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
			opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
			opts.AutoStartOnCreation = true;
		}));

var app = builder.Build();

// Run the startup gates NOW. This sample builds a host and never calls StartAsync(), so the gates a
// hosted run would fire never fire on their own -- the shape a console tool, a migration utility or a
// test fixture has. ValidateStartupGates() is the host-less trigger; without the opt-in above, this
// line is where the run would stop.
_ = app.Services.ValidateStartupGates();

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
