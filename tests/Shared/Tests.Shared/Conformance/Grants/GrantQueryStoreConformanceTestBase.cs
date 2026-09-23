// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.Dispatch;

namespace Tests.Shared.Conformance.Grants;

/// <summary>
/// The <see cref="IGrantQueryStore"/> contract, held against every provider.
/// </summary>
/// <remarks>
/// <para>
/// The contract: <see cref="IGrantQueryStore.GetMatchingGrantsAsync"/> returns exactly the non-revoked
/// grants of ONE tenant whose fields equal every non-null filter under ORDINAL comparison; a null filter,
/// and only a null filter, leaves its field unconstrained; an empty or whitespace filter (or tenant) is
/// refused. <see cref="IGrantQueryStore.GetMatchingGrantsAcrossTenantsAsync"/> is the same without the tenant.
/// </para>
/// <para>
/// Each arm is shaped to go red against a SPECIFIC way of getting this wrong, all of which shipped:
/// <c>LIKE</c> matching (where <c>_</c> is a one-character wildcard, so <c>orders_read</c> matched
/// <c>ordersXread</c>), a case-insensitive collation (<c>Admin</c> matching <c>admin</c>), an empty string
/// read as a value (so callers passing <c>""</c> to mean "all" silently got nothing), a revoked grant still
/// returned, and a tenant term that leaks a neighbour's grants.
/// </para>
/// <para>
/// Every arm seeds under identifiers unique to the arm, and filters any estate-wide result down to them,
/// so providers whose store is shared across arms and runs are measured on their own rows only.
/// </para>
/// </remarks>
public abstract class GrantQueryStoreConformanceTestBase : IAsyncLifetime
{
	private readonly string _run = Guid.NewGuid().ToString("N")[..10];

	/// <summary>Gets the store under test.</summary>
	protected IGrantStore Store { get; private set; } = null!;

	/// <summary>Gets the store's query facet.</summary>
	protected IGrantQueryStore Query { get; private set; } = null!;

	/// <summary>Creates the store under test. Called once per arm.</summary>
	/// <returns>The store.</returns>
	protected abstract Task<IGrantStore> CreateStoreAsync();

	/// <inheritdoc />
	public async ValueTask InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
		Query = Store.GetService(typeof(IGrantQueryStore)) as IGrantQueryStore
			?? throw new InvalidOperationException(
				$"{Store.GetType().Name} does not expose IGrantQueryStore through GetService, so the grant-query "
				+ "contract cannot be held against it.");
	}

	/// <inheritdoc />
	public virtual async ValueTask DisposeAsync()
	{
		if (Store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (Store is IDisposable disposable)
		{
			disposable.Dispose();
		}

		GC.SuppressFinalize(this);
	}

	private string TenantA => "tenant-a-" + _run;

	private string TenantB => "tenant-b-" + _run;

	private string User(string name) => name + "-" + _run;

	/// <summary>
	/// The fixture every arm reads: two tenants, the untenanted sentinel, look-alike qualifiers, look-alike
	/// casings, and one revoked grant.
	/// </summary>
	private async Task SeedAsync(CancellationToken ct)
	{
		var grantedOn = DateTimeOffset.UtcNow.AddDays(-1);

		async Task Save(string user, string tenant, string type, string qualifier, DateTimeOffset? expiresOn = null) =>
			_ = await Store.SaveGrantAsync(
				new Grant(user, "Full Name", tenant, type, qualifier, expiresOn, "conformance", grantedOn), ct)
				.ConfigureAwait(false);

		await Save(User("alice"), TenantA, "Activity", "orders_read").ConfigureAwait(false);
		await Save(User("alice"), TenantA, "Activity", "ordersXread").ConfigureAwait(false);
		await Save(User("bob"), TenantA, "Role", "Admin").ConfigureAwait(false);
		await Save(User("carol"), TenantA, "Role", "admin").ConfigureAwait(false);
		await Save(User("dave"), TenantA, "Activity", "orders_read", DateTimeOffset.UtcNow.AddDays(-2)).ConfigureAwait(false);
		await Save(User("erin"), TenantA, "Activity", "orders_read").ConfigureAwait(false);
		await Save(User("alice"), TenantB, "Activity", "orders_read").ConfigureAwait(false);
		await Save(User("alice"), TenantScope.UntenantedSentinel, "Activity", "orders_read").ConfigureAwait(false);

		// erin's grant is revoked: every provider must stop returning it, however it records the revocation.
		_ = await Store.DeleteGrantAsync(
			User("erin"), TenantA, "Activity", "orders_read", "conformance", DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
	}

	private IEnumerable<Grant> Mine(IEnumerable<Grant> grants) =>
		grants.Where(g => g.UserId.EndsWith("-" + _run, StringComparison.Ordinal));

	// Sorted ordinally, so the expected arrays are too: note 'X' (0x58) sorts before '_' (0x5F).
	private static string[] Keys(IEnumerable<Grant> grants) =>
		grants.Select(g => $"{g.UserId[..g.UserId.LastIndexOf('-')]}|{Short(g.TenantId)}|{g.GrantType}|{g.Qualifier}")
			.OrderBy(k => k, StringComparer.Ordinal)
			.ToArray();

	private static string Short(string tenant) =>
		tenant.StartsWith("tenant-a-", StringComparison.Ordinal) ? "A"
		: tenant.StartsWith("tenant-b-", StringComparison.Ordinal) ? "B"
		: tenant;

	/// <summary>
	/// SAFETY: a tenant-confined read with no other filter returns that tenant's non-revoked grants --
	/// expired ones included -- and nothing from the other tenant or from the untenanted partition.
	/// </summary>
	[Fact]
	public async Task ReturnExactlyTheTenantsNonRevokedGrants_WhenNoOtherFilterIsGiven()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var grants = await Query.GetMatchingGrantsAsync(TenantA, userId: null, grantType: null, qualifier: null, ct)
			.ConfigureAwait(false);

		Keys(grants).ShouldBe(
			[
				"alice|A|Activity|ordersXread",
				"alice|A|Activity|orders_read",
				"bob|A|Role|Admin",
				"carol|A|Role|admin",
				"dave|A|Activity|orders_read",
			],
			customMessage: "a null filter must mean unconstrained, the tenant must confine the read, the revoked "
				+ "grant must be excluded and the expired one included");
	}

	/// <summary>
	/// SAFETY, against <c>LIKE</c>: an underscore in a filter is a literal character, not a one-character
	/// wildcard. Under <c>LIKE</c>, <c>orders_read</c> also matched <c>ordersXread</c>.
	/// </summary>
	[Fact]
	public async Task MatchAnUnderscoreLiterally_NotAsAWildcard()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var grants = await Query.GetMatchingGrantsAsync(TenantA, User("alice"), "Activity", "orders_read", ct)
			.ConfigureAwait(false);

		Keys(grants).ShouldBe(
			["alice|A|Activity|orders_read"],
			customMessage: "'orders_read' must not match 'ordersXread': an underscore is not a wildcard");
	}

	/// <summary>
	/// SAFETY, against a case-insensitive collation: in authorization <c>Admin</c> and <c>admin</c> are
	/// different grants.
	/// </summary>
	[Fact]
	public async Task CompareCaseSensitively()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var upper = await Query.GetMatchingGrantsAsync(TenantA, userId: null, "Role", "Admin", ct).ConfigureAwait(false);
		var lower = await Query.GetMatchingGrantsAsync(TenantA, userId: null, "Role", "admin", ct).ConfigureAwait(false);

		Keys(upper).ShouldBe(["bob|A|Role|Admin"], customMessage: "'Admin' must not match 'admin'");
		Keys(lower).ShouldBe(["carol|A|Role|admin"], customMessage: "'admin' must not match 'Admin'");
	}

	/// <summary>LIVENESS: a user filter alone selects that user's grants in the tenant, and only those.</summary>
	[Fact]
	public async Task FilterByUserAlone()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var grants = await Query.GetMatchingGrantsAsync(TenantA, User("alice"), grantType: null, qualifier: null, ct)
			.ConfigureAwait(false);

		Keys(grants).ShouldBe(
			["alice|A|Activity|ordersXread", "alice|A|Activity|orders_read"],
			customMessage: "the user filter must select alice's grants in tenant A and nothing in tenant B");
	}

	/// <summary>
	/// The untenanted sentinel is a tenant like any other: a single-tenant host reads its grants by
	/// naming it, and gets none of the tenanted grants.
	/// </summary>
	[Fact]
	public async Task TreatTheUntenantedSentinelAsItsOwnTenant()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var grants = await Query.GetMatchingGrantsAsync(
			TenantScope.UntenantedSentinel, User("alice"), grantType: null, qualifier: null, ct).ConfigureAwait(false);

		Keys(grants).ShouldBe([$"alice|{TenantScope.UntenantedSentinel}|Activity|orders_read"]);
	}

	/// <summary>
	/// The estate-wide read spans every tenant, applies the same exact filters, and still excludes the
	/// revoked grant.
	/// </summary>
	[Fact]
	public async Task ReadAcrossEveryTenant_WhenAskedToByName()
	{
		var ct = TestContext.Current.CancellationToken;
		await SeedAsync(ct).ConfigureAwait(false);

		var grants = await Query.GetMatchingGrantsAcrossTenantsAsync(userId: null, "Activity", "orders_read", ct)
			.ConfigureAwait(false);

		Keys(Mine(grants)).ShouldBe(
			[
				"alice|A|Activity|orders_read",
				"alice|B|Activity|orders_read",
				$"alice|{TenantScope.UntenantedSentinel}|Activity|orders_read",
				"dave|A|Activity|orders_read",
			],
			customMessage: "the estate-wide read must span every tenant with exact filters and exclude the revoked grant");
	}

	/// <summary>
	/// An empty string is refused, never read as "all" and never read as a value. Callers passed
	/// <c>""</c> to mean "every grant" and every real store returned nothing -- silently.
	/// </summary>
	[Fact]
	public async Task RefuseAnEmptyFilterOrTenant()
	{
		var ct = TestContext.Current.CancellationToken;

		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAsync(string.Empty, userId: null, grantType: null, qualifier: null, ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAsync("  ", userId: null, grantType: null, qualifier: null, ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAsync(TenantA, userId: string.Empty, grantType: null, qualifier: null, ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAsync(TenantA, userId: null, grantType: string.Empty, qualifier: null, ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAsync(TenantA, userId: null, grantType: null, qualifier: string.Empty, ct));
		_ = await Should.ThrowAsync<ArgumentException>(
			() => Query.GetMatchingGrantsAcrossTenantsAsync(userId: null, grantType: string.Empty, qualifier: null, ct));
	}
}
