using Excalibur.A3.Authorization;
using Excalibur.Domain;

namespace Excalibur.Tests.A3.Authorization;

[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationCacheKeyShould : IDisposable
{
	public AuthorizationCacheKeyShould()
	{
		// Set up ApplicationContext with a base path for cache keys
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
			[nameof(AuthorizationCacheKey)] = "myapp",
		});
	}

	public void Dispose()
	{
		// Clean up ApplicationContext
		ApplicationContext.Reset();
	}

	[Fact]
	public void Generate_grants_key_with_user_id()
	{
		// Act
		var key = AuthorizationCacheKey.ForGrants("user-123");

		// Assert
		key.ShouldContain("authorization");
		key.ShouldContain("user-123");
		key.ShouldContain("grants");
	}

	[Fact]
	public void Generate_activity_groups_key()
	{
		// Act
		var key = AuthorizationCacheKey.ForActivityGroups();

		// Assert
		key.ShouldContain("authorization");
		key.ShouldContain("activity-groups");
	}

	[Fact]
	public void Throw_when_user_id_is_null()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(null!));
	}

	[Fact]
	public void Throw_when_user_id_is_empty()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants(string.Empty));
	}

	[Fact]
	public void Throw_when_user_id_is_whitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => AuthorizationCacheKey.ForGrants("   "));
	}

	[Fact]
	public void Include_base_path_in_grants_key()
	{
		// Act
		var key = AuthorizationCacheKey.ForGrants("user-123");

		// Assert
		key.ShouldStartWith("myapp/");
	}

	[Fact]
	public void Include_base_path_in_activity_groups_key()
	{
		// Act
		var key = AuthorizationCacheKey.ForActivityGroups();

		// Assert
		key.ShouldStartWith("myapp/");
	}

	[Fact]
	public void Throw_when_base_path_not_configured()
	{
		// Arrange - clear the application context
		ApplicationContext.Reset();

		// Act & Assert
		Should.Throw<Exception>(() => AuthorizationCacheKey.ForGrants("user-123"));
	}
}
