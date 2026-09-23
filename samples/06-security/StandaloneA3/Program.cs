// Standalone A3 Sample
// Demonstrates using Excalibur.A3.Core for grant management and authorization
// without any database, event sourcing, outbox, or Dispatch pipeline dependencies.

using Excalibur.A3.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register A3 Core with in-memory stores (no database required)
builder.Services.AddExcaliburA3Core();

var app = builder.Build();

// This sample stays on AddExcaliburA3Core() deliberately -- it exists to show the lightweight path,
// with no database, event sourcing, outbox or Dispatch pipeline. Core installs NO startup gates, so
// the call below passes trivially. That is the point of showing it here: the same line in the
// AccessReviews / JitAccess / ProvisioningWorkflow / SeparationOfDuties samples, which compose with
// AddExcaliburA3(), is where a volatile grant store stops the run unless the host opted in.
//
// A host that never calls StartAsync() -- a console tool, a migration utility, a test fixture -- must
// call this itself; nothing else runs the gates for it.
_ = app.Services.ValidateStartupGates();

using var scope = app.Services.CreateScope();
var grantStore = scope.ServiceProvider.GetRequiredService<IGrantStore>();
var activityGroupStore = scope.ServiceProvider.GetRequiredService<IActivityGroupStore>();

Console.WriteLine("=== Excalibur A3 Standalone Sample ===");
Console.WriteLine();

// 1. Create activity groups (permission templates)
Console.WriteLine("--- Step 1: Create Activity Groups ---");
await activityGroupStore.CreateActivityGroupAsync(
	tenantId: "tenant-1",
	name: "OrderManagement",
	activityName: "Orders.Read",
	CancellationToken.None);

await activityGroupStore.CreateActivityGroupAsync(
	tenantId: "tenant-1",
	name: "OrderManagement",
	activityName: "Orders.Write",
	CancellationToken.None);

await activityGroupStore.CreateActivityGroupAsync(
	tenantId: "tenant-1",
	name: "UserManagement",
	activityName: "Users.Read",
	CancellationToken.None);

// The read names the tenant: a group name is unique only WITHIN a tenant, so "which groups exist"
// has no single answer. You get this tenant's catalogue and never another's.
var groups = await activityGroupStore.FindActivityGroupsAsync("tenant-1", CancellationToken.None);
Console.WriteLine($"Activity groups created: {groups.Count}");
foreach (var group in groups)
{
	Console.WriteLine($"  {group.Key}: {string.Join(", ", group.Value)}");
}

Console.WriteLine();

// 2. Grant permissions to a user
Console.WriteLine("--- Step 2: Grant Permissions ---");
var grant = new Grant(
	UserId: "user-alice",
	FullName: "Alice Johnson",
	TenantId: "tenant-1",
	GrantType: "ActivityGroup",
	Qualifier: "OrderManagement",
	ExpiresOn: DateTimeOffset.UtcNow.AddDays(90),
	GrantedBy: "admin",
	GrantedOn: DateTimeOffset.UtcNow);

await grantStore.SaveGrantAsync(grant, CancellationToken.None);
Console.WriteLine($"Granted '{grant.Qualifier}' to {grant.FullName}");

var grant2 = new Grant(
	UserId: "user-alice",
	FullName: "Alice Johnson",
	TenantId: "tenant-1",
	GrantType: "ActivityGroup",
	Qualifier: "UserManagement",
	ExpiresOn: null, // No expiration
	GrantedBy: "admin",
	GrantedOn: DateTimeOffset.UtcNow);

await grantStore.SaveGrantAsync(grant2, CancellationToken.None);
Console.WriteLine($"Granted '{grant2.Qualifier}' to {grant2.FullName}");

Console.WriteLine();

// 3. Query grants for a user
Console.WriteLine("--- Step 3: Query User Grants ---");
var aliceGrants = await grantStore.GetAllGrantsAsync("user-alice", CancellationToken.None);
Console.WriteLine($"Alice has {aliceGrants.Count} grants:");
foreach (var g in aliceGrants)
{
	var expiry = g.ExpiresOn.HasValue ? $"expires {g.ExpiresOn.Value:yyyy-MM-dd}" : "no expiration";
	Console.WriteLine($"  {g.GrantType}:{g.Qualifier} ({expiry})");
}

Console.WriteLine();

// 4. Check authorization
Console.WriteLine("--- Step 4: Authorization Checks ---");
var hasOrderMgmt = await grantStore.GrantExistsAsync(
	"user-alice", "tenant-1", "ActivityGroup", "OrderManagement", CancellationToken.None);
Console.WriteLine($"Alice has OrderManagement: {hasOrderMgmt}");

var hasReporting = await grantStore.GrantExistsAsync(
	"user-alice", "tenant-1", "ActivityGroup", "Reporting", CancellationToken.None);
Console.WriteLine($"Alice has Reporting: {hasReporting}");

var hasBobGrants = await grantStore.GrantExistsAsync(
	"user-bob", "tenant-1", "ActivityGroup", "OrderManagement", CancellationToken.None);
Console.WriteLine($"Bob has OrderManagement: {hasBobGrants}");

Console.WriteLine();

// 5. Revoke a grant
Console.WriteLine("--- Step 5: Revoke Grant ---");
var deleted = await grantStore.DeleteGrantAsync(
	"user-alice", "tenant-1", "ActivityGroup", "UserManagement",
	revokedBy: "admin", revokedOn: DateTimeOffset.UtcNow, CancellationToken.None);
Console.WriteLine($"Revoked UserManagement from Alice: {(deleted > 0 ? "success" : "not found")}");

var remainingGrants = await grantStore.GetAllGrantsAsync("user-alice", CancellationToken.None);
Console.WriteLine($"Alice now has {remainingGrants.Count} grant(s)");

Console.WriteLine();

// 6. Use ISP sub-interface via GetService
Console.WriteLine("--- Step 6: Advanced Query (ISP) ---");
var queryStore = grantStore.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore;
if (queryStore is not null)
{
	// One tenant's grants; null means "any user". An empty string is refused, never read as "all".
	var matching = await queryStore.GetMatchingGrantsAsync(
		tenantId: "tenant-1",
		userId: null, // All users
		grantType: "ActivityGroup",
		qualifier: "OrderManagement",
		CancellationToken.None);
	Console.WriteLine($"All users with OrderManagement grant: {matching.Count}");
	foreach (var m in matching)
	{
		Console.WriteLine($"  {m.UserId} ({m.FullName})");
	}
}

Console.WriteLine();

// 7. Remove YOUR tenant's activity groups -- and not anybody else's
Console.WriteLine("--- Step 7: Remove One Tenant's Activity Groups ---");

// A second tenant, so the confinement below is observable rather than asserted.
await activityGroupStore.CreateActivityGroupAsync(
	tenantId: "tenant-2",
	name: "OrderManagement",
	activityName: "Orders.Read",
	CancellationToken.None);

// TWO NAMED OPERATIONS, and the one you want is the one you name.
//
// This is the tenant-scoped delete. Its sibling, DeleteAllActivityGroupsAsync(), removes EVERY
// tenant's groups and is meant for a full catalogue refresh -- it is not a wider version of this
// call, it is a different operation with a different blast radius.
//
// They are deliberately not one method with an optional tenant. A nullable "all tenants when null"
// parameter would make the destructive case the value you get by FORGETTING an argument, and the
// radius would stop being readable at the call site -- which, for an operation that can empty the
// estate, is the one place it has to be readable.
var removedForTenant1 = await activityGroupStore.DeleteActivityGroupsForTenantAsync(
	"tenant-1", CancellationToken.None);

Console.WriteLine($"Rows removed for tenant-1: {removedForTenant1}");

var tenant1After = await activityGroupStore.FindActivityGroupsAsync("tenant-1", CancellationToken.None);
var tenant2After = await activityGroupStore.FindActivityGroupsAsync("tenant-2", CancellationToken.None);

Console.WriteLine($"tenant-1 groups remaining: {tenant1After.Count}");
Console.WriteLine($"tenant-2 groups remaining: {tenant2After.Count}  (untouched)");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
