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
		var key = AuthorizationCacheKey.ForActivityGroups();

		key.ShouldBe("authorization/activity-groups");
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
		var activityGroups = AuthorizationCacheKey.ForActivityGroups();

		grants.ShouldBe("authorization/user-123/grants");
		activityGroups.ShouldBe("authorization/activity-groups");
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
		AuthorizationCacheKey.ForActivityGroups()
			.ShouldNotBe(AuthorizationCacheKey.ForGrants("user-a"));
	}
}
