using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.Services;
using Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Functional.A3.SqlServer;

public class SqlServerGrantRequestProviderFunctionalTests : SqlServerHostTestBase
{
	private readonly IGrantService _grantService;

	public SqlServerGrantRequestProviderFunctionalTests()
	{
		// Verify the SqlServerGrantRequestProvider is registered
		var provider = GetService<SqlServerGrantRequestProvider>();
		provider.ShouldNotBeNull();

		// Get the grant service from the service provider
		_grantService = GetService<IGrantService>();
		_grantService.ShouldNotBeNull();
	}

	[Fact]
	public async Task GrantService_WithSqlServerProvider_ManagedUserGrantsCorrectly()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier = "resource1";
		var grantedBy = "system";
		var fullName = "Test User";

		// Act - Grant access
		var granted = await _grantService.GrantAccess(userId, fullName, tenantId, grantType, qualifier, grantedBy).ConfigureAwait(true);

		// Assert - Grant was successful
		granted.ShouldBeTrue();

		// Act - Check if user has access
		var hasAccess = await _grantService.HasAccess(userId, tenantId, grantType, qualifier).ConfigureAwait(true);

		// Assert - User has access
		hasAccess.ShouldBeTrue();

		// Act - Revoke access
		var revoked = await _grantService.RevokeAccess(userId, tenantId, grantType, qualifier, grantedBy).ConfigureAwait(true);

		// Assert - Revoke was successful
		revoked.ShouldBeTrue();

		// Act - Check if user still has access
		hasAccess = await _grantService.HasAccess(userId, tenantId, grantType, qualifier).ConfigureAwait(true);

		// Assert - User no longer has access
		hasAccess.ShouldBeFalse();
	}

	[Fact]
	public async Task GrantService_WithSqlServerProvider_ManagesActivityGroupGrantsCorrectly()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "activity_group";
		var activityGroup = "admin_group";
		var grantedBy = "system";
		var fullName = "Test User";

		// Act - Add user to activity group
		await _grantService.AddUserToActivityGroup(userId, fullName, tenantId, activityGroup, grantedBy).ConfigureAwait(true);

		// Assert - Check if user is in activity group
		var inGroup = await _grantService.IsUserInActivityGroup(userId, tenantId, activityGroup).ConfigureAwait(true);
		inGroup.ShouldBeTrue();

		// Get all activity groups for user
		var activityGroups = await _grantService.GetActivityGroupsForUser(userId, tenantId).ConfigureAwait(true);
		activityGroups.ShouldContain(activityGroup);

		// Act - Remove user from activity group
		await _grantService.RemoveUserFromActivityGroup(userId, tenantId, activityGroup, grantedBy).ConfigureAwait(true);

		// Assert - Check if user is still in activity group
		inGroup = await _grantService.IsUserInActivityGroup(userId, tenantId, activityGroup).ConfigureAwait(true);
		inGroup.ShouldBeFalse();
	}

	[Fact]
	public async Task GrantService_WithSqlServerProvider_HandlesMultipleGrantsCorrectly()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var fullName = "Test User";
		var grantedBy = "system";

		var grants = new[]
		{
			new { Type = "read", Qualifier = "resource1" }, new { Type = "write", Qualifier = "resource1" },
			new { Type = "read", Qualifier = "resource2" }
		};

		// Act - Grant multiple access rights
		foreach (var grant in grants)
		{
			await _grantService.GrantAccess(userId, fullName, tenantId, grant.Type, grant.Qualifier, grantedBy).ConfigureAwait(true);
		}

		// Assert - All grants are present
		foreach (var grant in grants)
		{
			var hasAccess = await _grantService.HasAccess(userId, tenantId, grant.Type, grant.Qualifier).ConfigureAwait(true);
			hasAccess.ShouldBeTrue($"User should have {grant.Type} access to {grant.Qualifier}");
		}

		// Act - Get all user grants
		var userGrants = await _grantService.GetUserGrants(userId).ConfigureAwait(true);

		// Assert - Dictionary contains all grants
		userGrants.ShouldNotBeNull();
		userGrants.Count.ShouldBeGreaterThanOrEqualTo(grants.Length);

		foreach (var grant in grants)
		{
			var key = $"{tenantId}:{grant.Type}:{grant.Qualifier}";
			userGrants.ShouldContainKey(key);
		}

		// Act - Revoke all access
		foreach (var grant in grants)
		{
			await _grantService.RevokeAccess(userId, tenantId, grant.Type, grant.Qualifier, grantedBy).ConfigureAwait(true);
		}

		// Assert - All access is revoked
		foreach (var grant in grants)
		{
			var hasAccess = await _grantService.HasAccess(userId, tenantId, grant.Type, grant.Qualifier).ConfigureAwait(true);
			hasAccess.ShouldBeFalse($"User should no longer have {grant.Type} access to {grant.Qualifier}");
		}
	}

	[Fact]
	public async Task GrantService_WithSqlServerProvider_HandlesTemporaryGrantsCorrectly()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier = "resource1";
		var grantedBy = "system";
		var fullName = "Test User";

		// Grant with expiration 5 seconds in the past (already expired)
		var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-5);

		// Act - Grant with already expired timestamp
		await _grantService.GrantAccess(userId, fullName, tenantId, grantType, qualifier, grantedBy, expiredTimestamp).ConfigureAwait(true);

		// Assert - User should not have access due to expired grant
		var hasAccess = await _grantService.HasAccess(userId, tenantId, grantType, qualifier).ConfigureAwait(true);
		hasAccess.ShouldBeFalse("Access should be denied due to expired grant");

		// Grant with expiration 30 seconds in the future
		var futureTimestamp = DateTimeOffset.UtcNow.AddSeconds(30);

		// Act - Grant with future expiration
		await _grantService.GrantAccess(userId, fullName, tenantId, grantType, qualifier, grantedBy, futureTimestamp).ConfigureAwait(true);

		// Assert - User should have access due to valid grant
		hasAccess = await _grantService.HasAccess(userId, tenantId, grantType, qualifier).ConfigureAwait(true);
		hasAccess.ShouldBeTrue("Access should be granted for not-yet-expired grant");
	}
}
