using System.Data;

using Excalibur.A3.SqlServer.RequestProviders.Authorization.ActivityGroups;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;

using Shouldly;

namespace Excalibur.Tests.Integration.A3.SqlServer;

public class SqlServerActivityGroupRequestProviderShould : SqlServerPersistenceOnlyTestBase
{
	private readonly SqlServerActivityGroupRequestProvider _provider;
	private readonly IDbConnection _dbConnection;

	public SqlServerActivityGroupRequestProviderShould()
	{
		_provider = new SqlServerActivityGroupRequestProvider();
		_dbConnection = DbConnection;
	}

	[Fact]
	public async Task ExistsActivityGroupRequest_ReturnsFalse_WhenGroupDoesNotExist()
	{
		// Arrange
		var activityGroupName = $"NonExistentGroup_{Guid.NewGuid()}";
		var request = _provider.ActivityGroupExists(activityGroupName);

		// Act
		var result = await _dbConnection.ResolveAsync(request).ConfigureAwait(true);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task CreateAndFindActivityGroups_Successfully()
	{
		// Arrange
		var groupName = $"TestGroup_{Guid.NewGuid()}";
		var activityName = "TestActivity";
		string? tenantId = null;

		// Act - Create the activity group
		var createRequest = _provider.CreateActivityGroup(tenantId, groupName, activityName);
		var createResult = await _dbConnection.ResolveAsync(createRequest).ConfigureAwait(true);

		// Assert - Create was successful
		createResult.ShouldBeGreaterThan(0);

		// Act - Check if group exists
		var existsRequest = _provider.ActivityGroupExists(groupName);
		var existsResult = await _dbConnection.ResolveAsync(existsRequest).ConfigureAwait(true);

		// Assert - Group exists
		existsResult.ShouldBeTrue();

		// Act - Find all groups
		var findRequest = _provider.FindActivityGroups();
		var findResult = await _dbConnection.ResolveAsync(findRequest).ConfigureAwait(true);

		// Assert - Group is in the results
		findResult.ShouldNotBeNull();
		findResult.ShouldContainKey(groupName);
		findResult[groupName].ShouldBe(activityName);
	}

	[Fact]
	public async Task DeleteAllActivityGroups_RemovesAllGroups()
	{
		// Arrange - Create a few activity groups
		var groupName1 = $"TestGroup1_{Guid.NewGuid()}";
		var groupName2 = $"TestGroup2_{Guid.NewGuid()}";
		var activityName = "TestActivity";
		string? tenantId = null;

		await _dbConnection.ResolveAsync(_provider.CreateActivityGroup(tenantId, groupName1, activityName)).ConfigureAwait(true);
		await _dbConnection.ResolveAsync(_provider.CreateActivityGroup(tenantId, groupName2, activityName)).ConfigureAwait(true);

		// Verify groups exist
		var exists1 = await _dbConnection.ResolveAsync(_provider.ActivityGroupExists(groupName1)).ConfigureAwait(true);
		var exists2 = await _dbConnection.ResolveAsync(_provider.ActivityGroupExists(groupName2)).ConfigureAwait(true);
		exists1.ShouldBeTrue();
		exists2.ShouldBeTrue();

		// Act - Delete all groups
		var deleteRequest = _provider.DeleteAllActivityGroups();
		var deleteResult = await _dbConnection.ResolveAsync(deleteRequest).ConfigureAwait(true);

		// Assert - Groups were deleted
		deleteResult.ShouldBeGreaterThanOrEqualTo(2);

		// Verify groups no longer exist
		exists1 = await _dbConnection.ResolveAsync(_provider.ActivityGroupExists(groupName1)).ConfigureAwait(true);
		exists2 = await _dbConnection.ResolveAsync(_provider.ActivityGroupExists(groupName2)).ConfigureAwait(true);
		exists1.ShouldBeFalse();
		exists2.ShouldBeFalse();
	}
}
