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
}
