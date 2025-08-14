using System.Data;

using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.SqlServer.RequestProviders.Authorization.Grants;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

using Shouldly;

namespace Excalibur.Tests.Integration.A3.SqlServer;

public class SqlServerGrantRequestProviderShould : SqlServerPersistenceOnlyTestBase
{
	private readonly SqlServerGrantRequestProvider _provider;
	private readonly IDbConnection _dbConnection;

	public SqlServerGrantRequestProviderShould()
	{
		_provider = new SqlServerGrantRequestProvider();
		_dbConnection = DbConnection;
	}

	[Fact]
	public async Task GrantExists_ReturnsFalse_WhenGrantDoesNotExist()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier = "resource1";

		// Act
		var request = _provider.GrantExists(userId, tenantId, grantType, qualifier);
		var result = await _dbConnection.ResolveAsync(request).ConfigureAwait(true);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task SaveAndRetrieveGrant_Successfully()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier = "resource1";
		var grantedBy = "system";
		var fullName = "Test User";

		var grant = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		// Act - Save grant
		var saveRequest = _provider.SaveGrant(grant);
		var saveResult = await _dbConnection.ResolveAsync(saveRequest).ConfigureAwait(true);

		// Assert - Save was successful
		saveResult.ShouldBeGreaterThan(0);

		// Act - Check if grant exists
		var existsRequest = _provider.GrantExists(userId, tenantId, grantType, qualifier);
		var existsResult = await _dbConnection.ResolveAsync(existsRequest).ConfigureAwait(true);

		// Assert - Grant exists
		existsResult.ShouldBeTrue();

		// Act - Get grant
		var getRequest = _provider.GetGrant(userId, tenantId, grantType, qualifier);
		var getResult = await _dbConnection.ResolveAsync(getRequest).ConfigureAwait(true);

		// Assert - Grant data is correct
		getResult.ShouldNotBeNull();
		getResult.UserId.ShouldBe(userId);
		getResult.TenantId.ShouldBe(tenantId);
		getResult.GrantType.ShouldBe(grantType);
		getResult.Qualifier.ShouldBe(qualifier);
		getResult.GrantedBy.ShouldBe(grantedBy);
		getResult.FullName.ShouldBe(fullName);
	}

	[Fact]
	public async Task GetMatchingGrants_ReturnsCorrectGrants()
	{
		// Arrange - Create multiple grants
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier1 = "resource1";
		var qualifier2 = "resource2";
		var grantedBy = "system";
		var fullName = "Test User";

		var grant1 = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier1,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		var grant2 = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier2,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		// Save grants
		await _dbConnection.ResolveAsync(_provider.SaveGrant(grant1)).ConfigureAwait(true);
		await _dbConnection.ResolveAsync(_provider.SaveGrant(grant2)).ConfigureAwait(true);

		// Act - Get all grants for user
		var getAllRequest = _provider.GetAllGrants(userId);
		var allGrants = await _dbConnection.ResolveAsync(getAllRequest).ConfigureAwait(true);

		// Assert - Both grants returned
		allGrants.ShouldNotBeNull();
		allGrants.Count().ShouldBeGreaterThanOrEqualTo(2);
		allGrants.Count(g => g.UserId == userId && g.TenantId == tenantId).ShouldBeGreaterThanOrEqualTo(2);

		// Act - Get matching grants with specific qualifier
		var matchingRequest = _provider.GetMatchingGrants(userId, tenantId, grantType, qualifier1);
		var matchingGrants = await _dbConnection.ResolveAsync(matchingRequest).ConfigureAwait(true);

		// Assert - Only matching grant returned
		matchingGrants.ShouldNotBeNull();
		matchingGrants.Count().ShouldBeGreaterThanOrEqualTo(1);
		matchingGrants.Any(g => g.Qualifier == qualifier1).ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteGrant_RemovesGrantSuccessfully()
	{
		// Arrange - Create a grant
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier = "resource1";
		var grantedBy = "system";
		var fullName = "Test User";

		var grant = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		// Save grant
		await _dbConnection.ResolveAsync(_provider.SaveGrant(grant)).ConfigureAwait(true);

		// Verify grant exists
		var exists = await _dbConnection.ResolveAsync(_provider.GrantExists(userId, tenantId, grantType, qualifier)).ConfigureAwait(true);
		exists.ShouldBeTrue();

		// Act - Delete grant
		var revokedBy = "admin";
		var revokedOn = DateTimeOffset.UtcNow;
		var deleteRequest = _provider.DeleteGrant(userId, tenantId, grantType, qualifier, revokedBy, revokedOn);
		var deleteResult = await _dbConnection.ResolveAsync(deleteRequest).ConfigureAwait(true);

		// Assert - Grant was deleted
		deleteResult.ShouldBeGreaterThan(0);

		// Verify grant no longer exists
		exists = await _dbConnection.ResolveAsync(_provider.GrantExists(userId, tenantId, grantType, qualifier)).ConfigureAwait(true);
		exists.ShouldBeFalse();
	}

	[Fact]
	public async Task ActivityGroupGrants_WorkSuccessfully()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var fullName = "Test User";
		var tenantId = "tenant1";
		var grantType = "activity_group";
		var qualifier = "admin_group";
		var grantedBy = "system";
		var expiresOn = DateTimeOffset.UtcNow.AddDays(30);

		// Act - Insert activity group grant
		var insertRequest = _provider.InsertActivityGroupGrant(userId, fullName, tenantId, grantType, qualifier, expiresOn, grantedBy);
		var insertResult = await _dbConnection.ResolveAsync(insertRequest).ConfigureAwait(true);

		// Assert - Insert was successful
		insertResult.ShouldBeGreaterThan(0);

		// Act - Get distinct user IDs with activity group grants
		var userIdsRequest = _provider.GetDistinctActivityGroupGrantUserIds(grantType);
		var userIds = await _dbConnection.ResolveAsync(userIdsRequest).ConfigureAwait(true);

		// Assert - User ID is in the list
		userIds.ShouldContain(userId);

		// Act - Delete activity group grants by user ID
		var deleteRequest = _provider.DeleteActivityGroupGrantsByUserId(userId, grantType);
		var deleteResult = await _dbConnection.ResolveAsync(deleteRequest).ConfigureAwait(true);

		// Assert - Deletion was successful
		deleteResult.ShouldBeGreaterThan(0);

		// Verify user ID is no longer in the list
		userIds = await _dbConnection.ResolveAsync(userIdsRequest).ConfigureAwait(true);
		userIds.ShouldNotContain(userId);
	}

	[Fact]
	public async Task DeleteAllActivityGroupGrants_RemovesAllGrants()
	{
		// Arrange - Create multiple activity group grants
		var userId1 = $"user1_{Guid.NewGuid()}";
		var userId2 = $"user2_{Guid.NewGuid()}";
		var fullName = "Test User";
		var tenantId = "tenant1";
		var grantType = $"test_activity_group_{Guid.NewGuid()}";
		var qualifier = "admin_group";
		var grantedBy = "system";

		await _dbConnection.ResolveAsync(_provider.InsertActivityGroupGrant(userId1, fullName, tenantId, grantType, qualifier, null, grantedBy)).ConfigureAwait(true);
		await _dbConnection.ResolveAsync(_provider.InsertActivityGroupGrant(userId2, fullName, tenantId, grantType, qualifier, null, grantedBy)).ConfigureAwait(true);

		// Verify grants exist
		var userIds = await _dbConnection.ResolveAsync(_provider.GetDistinctActivityGroupGrantUserIds(grantType)).ConfigureAwait(true);
		userIds.ShouldContain(userId1);
		userIds.ShouldContain(userId2);

		// Act - Delete all activity group grants
		var deleteAllRequest = _provider.DeleteAllActivityGroupGrants(grantType);
		var deleteAllResult = await _dbConnection.ResolveAsync(deleteAllRequest).ConfigureAwait(true);

		// Assert - All grants were deleted
		deleteAllResult.ShouldBeGreaterThanOrEqualTo(2);

		// Verify no more grants exist for this grant type
		userIds = await _dbConnection.ResolveAsync(_provider.GetDistinctActivityGroupGrantUserIds(grantType)).ConfigureAwait(true);
		userIds.ShouldNotContain(userId1);
		userIds.ShouldNotContain(userId2);
	}

	[Fact]
	public async Task FindUserGrants_ReturnsCorrectGrantsDictionary()
	{
		// Arrange
		var userId = $"user_{Guid.NewGuid()}";
		var tenantId = "tenant1";
		var grantType = "read";
		var qualifier1 = "resource1";
		var qualifier2 = "resource2";
		var grantedBy = "system";
		var fullName = "Test User";

		var grant1 = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier1,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		var grant2 = new Grant
		{
			UserId = userId,
			TenantId = tenantId,
			GrantType = grantType,
			Qualifier = qualifier2,
			GrantedBy = grantedBy,
			GrantedOn = DateTimeOffset.UtcNow,
			FullName = fullName
		};

		// Save grants
		await _dbConnection.ResolveAsync(_provider.SaveGrant(grant1)).ConfigureAwait(true);
		await _dbConnection.ResolveAsync(_provider.SaveGrant(grant2)).ConfigureAwait(true);

		// Act
		var findRequest = _provider.FindUserGrants(userId);
		var result = await _dbConnection.ResolveAsync(findRequest).ConfigureAwait(true);

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBeGreaterThanOrEqualTo(2);
		result.ShouldContainKey($"{tenantId}:{grantType}:{qualifier1}");
		result.ShouldContainKey($"{tenantId}:{grantType}:{qualifier2}");
	}
}
