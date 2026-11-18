using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.Data;
using Excalibur.Data.Postgres.Authorization;
using Excalibur.Dispatch.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Authorization;

[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "A3")]
[Trait("Pattern", "AtomicRevocation")]
public sealed class RelationalAuthorizationAcquisitionShould(PostgresFixture fixture) : IAsyncLifetime
{
	public async ValueTask InitializeAsync()
	{
		await ShippedGrantSchema.ProvisionPostgresAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
		await ShippedActivityGroupSchema.ProvisionPostgresAsync(fixture.ConnectionString, TestContext.Current.CancellationToken);
	}
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	[Fact]
	public async Task Acquire_and_refresh_policy_with_both_stores_sharing_one_scoped_connection()
	{
		using var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		var grants = new PostgresGrantStore(db);
		var user = $"policy-{Guid.NewGuid():N}";
		var token = A.Fake<IAuthenticationToken>();
		A.CallTo(() => token.UserId).Returns(user);
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns("one");
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(token);
		services.AddSingleton(tenant);
		services.AddExcaliburA3();
		services.AddSingleton<IGrantStore>(grants);
		services.AddSingleton<IActivityGroupStore>(new PostgresActivityGroupStore(db, Microsoft.Extensions.Options.Options.Create(new PostgresAuthorizationOptions())));
		services.AddDistributedMemoryCache();
		services.AddApplicationScopedDistributedCache(options => options.Scope = user);
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		await grants.SaveGrantAsync(new Grant(user, null, "one", "Activity", "Read", null, "actor", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
		var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
		(await policies.GetPolicyAsync()).HasGrant("Read").ShouldBeTrue();
		await grants.RevokeAllAsync(new GrantRevocationRequest(Guid.NewGuid(), user, GrantRevocationScope.ForTenant("one"), "actor"), TestContext.Current.CancellationToken);
		(await policies.GetPolicyAsync()).HasGrant("Read").ShouldBeFalse();
	}
	/// <summary>
	/// An ACTIVITY-GROUP grant must authorize the activities in that group, on a relational provider.
	/// </summary>
	/// <returns>The arm.</returns>
	/// <remarks>
	/// <para>
	/// This is the arm whose absence let a shipped authorization feature be inert on both production
	/// providers behind a green suite. The decision path used to gate the group lookup on a structural
	/// test — <c>is IEnumerable&lt;object&gt;</c> — which the in-memory store satisfied by covariance
	/// while the relational stores, returning their own row shape, did not. So the group path contributed
	/// no decision at all on SQL Server and Postgres: a group grant granted NOTHING, and every test that
	/// covered groups covered them in memory.
	/// </para>
	/// <para>
	/// The arm that catches that cannot be a store test. The stores were fine — they returned their rows
	/// correctly. It has to go through the POLICY, on a real relational provider, and assert that an
	/// activity reached by a group grant is authorized. The direct-grant arm above passes either way,
	/// which is exactly why it did not catch this.
	/// </para>
	/// <para>
	/// The grant names only the GROUP. If the policy authorized the activity by any route other than
	/// resolving that group, the grant qualifier would have to mention the activity, and it does not.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Authorize_an_activity_reached_only_through_an_activity_group_grant()
	{
		using var db = new DomainDb(new NpgsqlConnection(fixture.ConnectionString));
		var grants = new PostgresGrantStore(db);
		var groups = new PostgresActivityGroupStore(
			db, Microsoft.Extensions.Options.Options.Create(new PostgresAuthorizationOptions()));

		var user = $"group-policy-{Guid.NewGuid():N}";
		var tenantId = $"t-{Guid.NewGuid():N}"[..12];
		var groupName = $"g-{Guid.NewGuid():N}"[..10];
		const string activity = "Approve";

		var token = A.Fake<IAuthenticationToken>();
		A.CallTo(() => token.UserId).Returns(user);
		var tenant = A.Fake<ITenantContext>();
		A.CallTo(() => tenant.TenantId).Returns(tenantId);

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(token);
		services.AddSingleton(tenant);
		services.AddExcaliburA3();
		services.AddSingleton<IGrantStore>(grants);
		services.AddSingleton<IActivityGroupStore>(groups);
		services.AddDistributedMemoryCache();
		services.AddApplicationScopedDistributedCache(options => options.Scope = user);

		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();

		// The group contains the activity...
		_ = await groups.CreateActivityGroupAsync(
			tenantId, groupName, activity, TestContext.Current.CancellationToken);

		// ...and the grant names the GROUP, never the activity.
		await grants.SaveGrantAsync(
			// "ActivityGroup" is the value of GrantType.ActivityGroup; the literal matches the arm above and
			// avoids pulling in a using that makes Grant itself ambiguous.
			new Grant(user, null, tenantId, "ActivityGroup", groupName, null, "actor", DateTimeOffset.UtcNow),
			TestContext.Current.CancellationToken);

		var policies = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

		(await policies.GetPolicyAsync()).HasGrant(activity).ShouldBeTrue(
			"the activity is reachable only by resolving the granted group on a relational provider. False "
			+ "here is the shipped defect: the group path contributing no authorization decision at all, "
			+ "which a store test cannot see and an in-memory policy test cannot either.");
	}
}
