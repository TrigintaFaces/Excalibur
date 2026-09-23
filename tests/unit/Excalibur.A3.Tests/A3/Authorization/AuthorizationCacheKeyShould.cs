using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationCacheKey"/>.
/// </summary>
/// <remarks>
/// This class deliberately performs NO <c>ApplicationContext</c> setup and is not in the
/// <c>ApplicationContext</c> collection. Needing neither is the property under test: these keys used to be
/// built from an undocumented configuration key that nothing in the repository set outside test fixtures,
/// so every grant-cache path threw for a consumer who had configured only what the documentation asks for.
/// A fixture that supplies the missing value is exactly what hid that.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationCacheKeyShould
{
	[Fact]
	public void Generate_grants_key_with_user_id()
	{
		var key = AuthorizationCacheKey.ForGrants("user-123");

		key.ShouldBe("authorization/user-123/grants");
	}

	[Fact]
	public void Generate_activity_groups_key()
	{
		var key = AuthorizationCacheKey.ForActivityGroups("tenant-1");

		// The trailing shape version is part of the contract, not decoration: it is what stops an instance
		// running a different version of this library from reading a cached document it cannot interpret.
		// Asserting it here means a change to the document's shape that forgets to bump the key goes red.
		key.ShouldBe("authorization/tenant-1/activity-groups/v3");
	}

	[Fact]
	public void Throw_when_user_id_is_null() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(null!));

	[Fact]
	public void Throw_when_user_id_is_empty() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(string.Empty));

	[Fact]
	public void Throw_when_user_id_is_whitespace() =>
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants("   "));

	[Fact]
	public void Build_keys_with_no_application_configuration_present_at_all()
	{
		// The arm this replaces asserted the OPPOSITE -- that an unconfigured application context makes
		// these throw -- and it passed, which is how a shipped path that no stock consumer could execute
		// stayed green. Nothing is initialised here, deliberately.
		var grants = AuthorizationCacheKey.ForGrants("user-123");
		var activityGroups = AuthorizationCacheKey.ForActivityGroups("tenant-1");

		grants.ShouldBe("authorization/user-123/grants");
		activityGroups.ShouldBe("authorization/tenant-1/activity-groups/v3");
	}

	[Fact]
	public void Depend_on_nothing_but_its_arguments()
	{
		// A pure function of its inputs: same input, same key, no ambient state to change the answer
		// between calls. Two distinct users must not collide, and the same user must be stable.
		AuthorizationCacheKey.ForGrants("user-a")
			.ShouldBe(AuthorizationCacheKey.ForGrants("user-a"));
		AuthorizationCacheKey.ForGrants("user-a")
			.ShouldNotBe(AuthorizationCacheKey.ForGrants("user-b"));
		AuthorizationCacheKey.ForActivityGroups("tenant-a")
			.ShouldBe(AuthorizationCacheKey.ForActivityGroups("tenant-a"));
		AuthorizationCacheKey.ForActivityGroups("tenant-a")
			.ShouldNotBe(AuthorizationCacheKey.ForGrants("user-a"));
	}

	/// <summary>
	/// SAFETY: two tenants never address the same activity-group entry.
	/// </summary>
	/// <remarks>
	/// The entry holds ONE tenant's catalogue, keyed by composed (tenant, name). A shared key would serve
	/// whichever tenant loaded it first to all of them, and a tenant finds none of its own groups under
	/// another's composed keys -- so every activity-group grant is denied, with no exception and no log.
	/// This arm is RED the moment the tenant term leaves the key.
	/// </remarks>
	[Fact]
	public void Give_two_tenants_distinct_activity_group_keys() =>
		AuthorizationCacheKey.ForActivityGroups("tenant-a")
			.ShouldNotBe(AuthorizationCacheKey.ForActivityGroups("tenant-b"));

	/// <summary>
	/// LIVENESS: the key builder still answers for a legitimate tenant.
	/// </summary>
	/// <remarks>
	/// Without this, a builder that threw for every input -- or returned the empty string -- would satisfy
	/// the distinctness arm above by refusing everyone equally.
	/// </remarks>
	[Fact]
	public void Build_a_usable_key_for_a_legitimate_tenant() =>
		AuthorizationCacheKey.ForActivityGroups("tenant-a")
			.ShouldBe("authorization/tenant-a/activity-groups/v3");

	[Fact]
	public void Throw_when_the_tenant_is_missing()
	{
		// A blank tenant would compose one shared key for everybody -- the exact cross-tenant entry the
		// tenant term exists to prevent -- so it is refused rather than silently accepted.
		_ = Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForActivityGroups(null!));
		_ = Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForActivityGroups(string.Empty));
		_ = Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForActivityGroups("   "));
	}
}
