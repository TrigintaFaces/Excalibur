using System.Data;
using System.Net;

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants.Domain.Model;
using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.Core;
using Excalibur.DataAccess;
using Excalibur.DataAccess.Exceptions;
using Excalibur.Tests.Mothers.Core;

using FakeItEasy;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Shouldly;

namespace Excalibur.Tests.Unit.A3.Authorization;

public sealed class ActivityGroupServiceShould : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly ICorrelationId _correlationId;
	private readonly IDbConnection _connection;
	private readonly IAuthenticationToken _token;
	private readonly IActivityGroupRequestProvider _activityGroupRequestProvider;
	private readonly IGrantRequestProvider _grantRequestProvider;
	private readonly ILogger<ActivityGroupService> _logger;
	private readonly IDistributedCache _cache;
	private readonly ActivityGroupService _sut;
	private readonly FakeHttpMessageHandler _mockHttpMessageHandler;

	public ActivityGroupServiceShould()
	{
		// Set up ApplicationContext for cache key
		ApplicationContextMother.Initialize(new Dictionary<string, string?>
		{
			{ "AuthorizationCacheKey", "test-cache" }, { "ApplicationName", "TestApp" }
		});

		// Set up mocks
		_correlationId = A.Fake<ICorrelationId>();
		_connection = A.Fake<IDbConnection>();
		_token = A.Fake<IAuthenticationToken>();
		_activityGroupRequestProvider = A.Fake<IActivityGroupRequestProvider>();
		_grantRequestProvider = A.Fake<IGrantRequestProvider>();
		_logger = A.Fake<ILogger<ActivityGroupService>>();
		_cache = A.Fake<IDistributedCache>();

		// Set up mock HTTP handler
		_mockHttpMessageHandler = new FakeHttpMessageHandler();
		_httpClient = new HttpClient(_mockHttpMessageHandler);
		_httpClient.BaseAddress = new Uri("https://localhost/");

		// Common setup
		_ = A.CallTo(() => _token.Jwt).Returns("fake-jwt-token");
		_ = A.CallTo(() => _token.FullName).Returns("Test User");
		_ = A.CallTo(() => _correlationId.ToString()).Returns("test-correlation-id");

		// Create the SUT (System Under Test)
		_sut = new ActivityGroupService(
			_httpClient,
			_correlationId,
			_connection,
			_token,
			_activityGroupRequestProvider,
			_grantRequestProvider,
			_logger,
			_cache);
	}

	public void Dispose()
	{
		_httpClient.Dispose();
		_connection.Dispose();
		_mockHttpMessageHandler.Dispose();
		GC.SuppressFinalize(this);
	}

	[Fact]
	public async Task ReturnTrueWhenActivityGroupExists()
	{
		// Arrange
		const string ActivityGroupName = "TestGroup";
		var request = A.Fake<IDataRequest<IDbConnection, bool>>();

		_ = A.CallTo(() => _activityGroupRequestProvider.ActivityGroupExists(ActivityGroupName, A<CancellationToken>._))
			.Returns(request);
		_ = A.CallTo(() => request.ResolveAsync(_connection))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.Exists(ActivityGroupName).ConfigureAwait(true);

		// Assert
		result.ShouldBeTrue();

		// Verify
		_ = A.CallTo(() => _activityGroupRequestProvider.ActivityGroupExists(ActivityGroupName, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => request.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnFalseWhenActivityGroupDoesNotExist()
	{
		// Arrange
		const string ActivityGroupName = "NonExistentGroup";
		var request = A.Fake<IDataRequest<IDbConnection, bool>>();

		_ = A.CallTo(() => _activityGroupRequestProvider.ActivityGroupExists(ActivityGroupName, A<CancellationToken>._))
			.Returns(request);
		_ = A.CallTo(() => request.ResolveAsync(_connection))
			.Returns(Task.FromResult(false));

		// Act
		var result = await _sut.Exists(ActivityGroupName).ConfigureAwait(true);

		// Assert
		result.ShouldBeFalse();

		// Verify
		_ = A.CallTo(() => _activityGroupRequestProvider.ActivityGroupExists(ActivityGroupName, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => request.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SyncActivityGroupsSuccessfully()
	{
		// Arrange
		var activityGroups = new[]
		{
			new TestActivityGroup
			{
				TenantId = "tenant1",
				Name = "Group1",
				Description = "First group",
				Activities = [new TestActivity { ApplicationName = "TestApp", ActivityName = "Activity1" }]
			},
			new TestActivityGroup
			{
				TenantId = "tenant1",
				Name = "Group2",
				Description = "Second group",
				Activities =
				[
					new TestActivity { ApplicationName = "TestApp", ActivityName = "Activity2" },
					new TestActivity { ApplicationName = "TestApp", ActivityName = "Activity3" }
				]
			}
		};

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(activityGroups))
		};

		// Setup database requests
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _activityGroupRequestProvider.DeleteAllActivityGroups(A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Setup create requests for each activity
		foreach (var group in activityGroups)
		{
			foreach (var activity in group.Activities)
			{
				var createRequest = A.Fake<IDataRequest<IDbConnection, int>>();
				_ = A.CallTo(() => _activityGroupRequestProvider.CreateActivityGroup(
						group.TenantId,
						group.Name,
						activity.ActivityName,
						A<CancellationToken>._))
					.Returns(createRequest);
				_ = A.CallTo(() => createRequest.ResolveAsync(_connection))
					.Returns(Task.FromResult(1));
			}
		}

		// Act
		await _sut.SyncActivityGroups().ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _activityGroupRequestProvider.DeleteAllActivityGroups(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared
		_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups(), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Verify each activity group was created
		_ = A.CallTo(() => _activityGroupRequestProvider.CreateActivityGroup(
				"tenant1", "Group1", "Activity1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _activityGroupRequestProvider.CreateActivityGroup(
				"tenant1", "Group2", "Activity2", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => _activityGroupRequestProvider.CreateActivityGroup(
				"tenant1", "Group2", "Activity3", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleEmptyActivityGroupsWhenSyncing()
	{
		// Arrange
		var emptyActivityGroups = Array.Empty<TestActivityGroup>();

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(emptyActivityGroups))
		};

		// Setup database requests
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _activityGroupRequestProvider.DeleteAllActivityGroups(A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Act
		await _sut.SyncActivityGroups().ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _activityGroupRequestProvider.DeleteAllActivityGroups(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared
		_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForActivityGroups(), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Verify no activity groups were created
		A.CallTo(() => _activityGroupRequestProvider.CreateActivityGroup(
				A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowOperationFailedExceptionWhenSyncActivityGroupsFails()
	{
		// Arrange
		var errorReason = "API call failed";

		// Setup mock HTTP response for failure
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(JsonConvert.SerializeObject(errorReason))
		};

		// Act & Assert
		var exception = await Should.ThrowAsync<OperationFailedException>(
			() => _sut.SyncActivityGroups()).ConfigureAwait(true);

		exception.StatusCode.ShouldBe(400);

		// We can't directly verify logging with FakeItEasy due to complexities with the Log method

		// Verify database operations did not occur
		A.CallTo(() => _activityGroupRequestProvider.DeleteAllActivityGroups(A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SyncActivityGroupGrantsSuccessfully()
	{
		// Arrange
		const string UserId = "user123";
		var activityGroupGrants = new[]
		{
			new TestActivityGroupGrant { TenantId = "tenant1", ActivityGroupName = "Group1", ExpiresOn = null, UserId = UserId },
			new TestActivityGroupGrant
			{
				TenantId = "tenant2", ActivityGroupName = "Group2", ExpiresOn = DateTimeOffset.UtcNow.AddDays(30), UserId = UserId
			}
		};

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(activityGroupGrants))
		};

		// Setup database requests
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _grantRequestProvider.DeleteActivityGroupGrantsByUserId(UserId, GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Setup insert requests for each grant
		foreach (var grant in activityGroupGrants)
		{
			var insertRequest = A.Fake<IDataRequest<IDbConnection, int>>();
			_ = A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
					UserId, UserId, grant.TenantId, GrantType.ActivityGroup, grant.ActivityGroupName,
					grant.ExpiresOn, A<string>._, A<CancellationToken>._))
				.Returns(insertRequest);
			_ = A.CallTo(() => insertRequest.ResolveAsync(_connection))
				.Returns(Task.FromResult(1));
		}

		// Act
		await _sut.SyncActivityGroupGrants(UserId).ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _grantRequestProvider.DeleteActivityGroupGrantsByUserId(UserId, GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared
		_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants(UserId), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Verify each grant was inserted
		foreach (var grant in activityGroupGrants)
		{
			_ = A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
					UserId, UserId, grant.TenantId, GrantType.ActivityGroup, grant.ActivityGroupName,
					grant.ExpiresOn, A<string>._, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}
	}

	[Fact]
	public async Task HandleEmptyGrantsWhenSyncingActivityGroupGrants()
	{
		// Arrange
		const string UserId = "user123";
		var emptyGrants = Array.Empty<TestActivityGroupGrant>();

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(emptyGrants))
		};

		// Setup database requests
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _grantRequestProvider.DeleteActivityGroupGrantsByUserId(UserId, GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Act
		await _sut.SyncActivityGroupGrants(UserId).ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _grantRequestProvider.DeleteActivityGroupGrantsByUserId(UserId, GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared
		_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants(UserId), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Verify no grants were inserted
		A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
				A<string>._, A<string>._, A<string>._, A<string>._, A<string>._,
				A<DateTimeOffset?>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowOperationFailedExceptionWhenSyncActivityGroupGrantsFails()
	{
		// Arrange
		const string UserId = "user123";
		var errorReason = "API call failed";

		// Setup mock HTTP response for failure
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(JsonConvert.SerializeObject(errorReason))
		};

		// Act & Assert
		var exception = await Should.ThrowAsync<OperationFailedException>(
			() => _sut.SyncActivityGroupGrants(UserId)).ConfigureAwait(true);

		exception.StatusCode.ShouldBe(400);

		// Verify database operations did not occur
		A.CallTo(() => _grantRequestProvider.DeleteActivityGroupGrantsByUserId(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SyncAllActivityGroupGrantsSuccessfully()
	{
		// Arrange
		var userIds = new[] { "user1", "user2", "user3" };
		var activityGroupGrants = new[]
		{
			new TestActivityGroupGrant { TenantId = "tenant1", ActivityGroupName = "Group1", ExpiresOn = null, UserId = "user1" },
			new TestActivityGroupGrant
			{
				TenantId = "tenant2", ActivityGroupName = "Group2", ExpiresOn = DateTimeOffset.UtcNow.AddDays(30), UserId = "user2"
			},
			new TestActivityGroupGrant
			{
				TenantId = "tenant1", ActivityGroupName = "Group3", ExpiresOn = DateTimeOffset.UtcNow.AddDays(15), UserId = "user3"
			}
		};

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(activityGroupGrants))
		};

		// Setup user IDs request
		var userIdsRequest = A.Fake<IDataRequest<IDbConnection, IEnumerable<string>>>();
		_ = A.CallTo(() => _grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(userIdsRequest);
		_ = A.CallTo(() => userIdsRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult((IEnumerable<string>)userIds));

		// Setup delete request
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Setup insert requests for each grant
		foreach (var grant in activityGroupGrants)
		{
			var insertRequest = A.Fake<IDataRequest<IDbConnection, int>>();
			_ = A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, GrantType.ActivityGroup, grant.ActivityGroupName,
					grant.ExpiresOn, A<string>._, A<CancellationToken>._))
				.Returns(insertRequest);
			_ = A.CallTo(() => insertRequest.ResolveAsync(_connection))
				.Returns(Task.FromResult(1));
		}

		// Act
		await _sut.SyncAllActivityGroupGrants().ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => userIdsRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared for each user
		foreach (var userId in userIds)
		{
			_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants(userId), A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}

		// Verify each grant was inserted
		foreach (var grant in activityGroupGrants)
		{
			_ = A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
					grant.UserId, grant.UserId, grant.TenantId, GrantType.ActivityGroup, grant.ActivityGroupName,
					grant.ExpiresOn, A<string>._, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}
	}

	[Fact]
	public async Task HandleEmptyAllGrantsWhenSyncingAllActivityGroupGrants()
	{
		// Arrange
		var userIds = new[] { "user1", "user2" };
		var emptyGrants = Array.Empty<TestActivityGroupGrant>();

		// Setup mock HTTP response
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(JsonConvert.SerializeObject(emptyGrants))
		};

		// Setup user IDs request
		var userIdsRequest = A.Fake<IDataRequest<IDbConnection, IEnumerable<string>>>();
		_ = A.CallTo(() => _grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(userIdsRequest);
		_ = A.CallTo(() => userIdsRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult((IEnumerable<string>)userIds));

		// Setup delete request
		var deleteRequest = A.Fake<IDataRequest<IDbConnection, int>>();
		_ = A.CallTo(() => _grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup, A<CancellationToken>._))
			.Returns(deleteRequest);
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.Returns(Task.FromResult(1));

		// Act
		await _sut.SyncAllActivityGroupGrants().ConfigureAwait(true);

		// Assert & Verify
		_ = A.CallTo(() => _grantRequestProvider.GetDistinctActivityGroupGrantUserIds(GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => userIdsRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _grantRequestProvider.DeleteAllActivityGroupGrants(GrantType.ActivityGroup, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => deleteRequest.ResolveAsync(_connection))
			.MustHaveHappenedOnceExactly();

		// Verify cache was cleared for each user
		foreach (var userId in userIds)
		{
			_ = A.CallTo(() => _cache.RemoveAsync(AuthorizationCacheKey.ForGrants(userId), A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}

		// Verify no grants were inserted
		A.CallTo(() => _grantRequestProvider.InsertActivityGroupGrant(
				A<string>._, A<string>._, A<string>._, A<string>._, A<string>._,
				A<DateTimeOffset?>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowOperationFailedExceptionWhenSyncAllActivityGroupGrantsFails()
	{
		// Arrange
		var errorReason = "API call failed";

		// Setup mock HTTP response for failure
		_mockHttpMessageHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(JsonConvert.SerializeObject(errorReason))
		};

		// Act & Assert
		var exception = await Should.ThrowAsync<OperationFailedException>(
			() => _sut.SyncAllActivityGroupGrants()).ConfigureAwait(true);

		exception.StatusCode.ShouldBe(400);

		// Verify database operations did not occur
		A.CallTo(() => _grantRequestProvider.GetDistinctActivityGroupGrantUserIds(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _grantRequestProvider.DeleteAllActivityGroupGrants(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		A.CallTo(() => _cache.RemoveAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	/// <summary>
	///     Custom HTTP handler for testing HTTP requests
	/// </summary>
	private sealed class FakeHttpMessageHandler : HttpMessageHandler
	{
		public HttpResponseMessage ResponseToReturn { get; set; } = new HttpResponseMessage(HttpStatusCode.OK);

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// Verify the request contains the expected headers
			_ = request.Headers.Authorization.ShouldNotBeNull();
			request.Headers.Authorization.Scheme.ShouldBe("Bearer");
			request.Headers.Authorization.Parameter.ShouldBe("fake-jwt-token");
			request.Headers.Contains(ExcaliburHeaderNames.CorrelationId).ShouldBeTrue();

			return Task.FromResult(ResponseToReturn);
		}
	}

	// Test model classes to match the classes in ActivityGroupService
	private sealed class TestActivity
	{
		public required string ApplicationName { get; init; }
		public required string ActivityName { get; init; }
	}

	private sealed class TestActivityGroup
	{
		public string? TenantId { get; init; }
		public required string Name { get; init; }
		public required string Description { get; init; }
		public required List<TestActivity> Activities { get; init; }
	}

	private sealed class TestActivityGroupGrant
	{
		public string? TenantId { get; init; }
		public required string ActivityGroupName { get; init; }
		public DateTimeOffset? ExpiresOn { get; init; }
		public required string UserId { get; init; }
	}
}
