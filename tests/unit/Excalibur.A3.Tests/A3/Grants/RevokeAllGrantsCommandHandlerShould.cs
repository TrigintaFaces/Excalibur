using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Events;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Exceptions;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Grants;

[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class RevokeAllGrantsCommandHandlerShould
{
	private readonly IGrantRepository _grantRepository = A.Fake<IGrantRepository>();
	private readonly IDistributedCache _cache = A.Fake<IDistributedCache>();

	public RevokeAllGrantsCommandHandlerShould()
	{
		// Grant.Revoke() reads ApplicationContext.ApplicationName (static)
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
			["AuthorizationCacheKey"] = "test-cache",
		});
	}

	private RevokeAllGrantsCommandHandler CreateSut() => new(_grantRepository, _cache);

	private static RevokeAllGrantsCommand CreateValidCommand(
		string userId = "target-user",
		string fullName = "Target User",
		string? tenantId = "tenant-1")
	{
		var command = new RevokeAllGrantsCommand(userId, fullName, Guid.NewGuid(), tenantId);
		command.AccessToken = CreateFakeAccessToken("admin-user", "Admin User");
		return command;
	}

	private static IAccessToken CreateFakeAccessToken(string userId, string? fullName)
	{
		var token = A.Fake<IAccessToken>();
		A.CallTo(() => token.UserId).Returns(userId);
		A.CallTo(() => token.FullName).Returns(fullName ?? "Unknown");
		return token;
	}

	[Fact]
	public async Task Throw_when_access_token_is_null()
	{
		// Arrange
		var sut = CreateSut();
		var command = new RevokeAllGrantsCommand("user-1", "User", Guid.NewGuid(), "tenant-1");
		command.AccessToken = null;

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.HandleAsync(command, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_user_administers_own_grants()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand(userId: "same-user");
		command.AccessToken = CreateFakeAccessToken("same-user", "Same User");

		// Act & Assert
		await Should.ThrowAsync<NotAuthorizedException>(
			() => sut.HandleAsync(command, CancellationToken.None));
	}

	[Fact]
	public async Task Revoke_and_delete_all_non_activity_group_grants()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		var grants = new[]
		{
			CreateGrant("role", "admin"),
			CreateGrant("permission", "write"),
		};

		A.CallTo(() => _grantRepository.ReadAllAsync("target-user"))
			.Returns(grants.AsEnumerable());

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldContain("Revoked from");
		A.CallTo(() => _grantRepository.DeleteAsync(A<Grant>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task Skip_activity_group_grants()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		// One ActivityGroup grant and one regular grant
		var activityGroupGrant = CreateGrant(GrantType.ActivityGroup, "orders-group");
		var regularGrant = CreateGrant("role", "admin");

		A.CallTo(() => _grantRepository.ReadAllAsync("target-user"))
			.Returns(new[] { activityGroupGrant, regularGrant }.AsEnumerable());

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		// Only the regular grant should be deleted, ActivityGroup is filtered out
		A.CallTo(() => _grantRepository.DeleteAsync(A<Grant>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_no_grants_message_when_none_found()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		A.CallTo(() => _grantRepository.ReadAllAsync("target-user"))
			.Returns(Enumerable.Empty<Grant>());

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldContain("No grants were found");
	}

	[Fact]
	public async Task Invalidate_cache_after_revoking_all_grants()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		A.CallTo(() => _grantRepository.ReadAllAsync("target-user"))
			.Returns(new[] { CreateGrant("role", "admin") }.AsEnumerable());

		// Act
		await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("target-user"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Not_revoke_already_expired_grants_but_still_delete_them()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		var expiredGrant = CreateExpiredGrant("role", "admin");

		A.CallTo(() => _grantRepository.ReadAllAsync("target-user"))
			.Returns(new[] { expiredGrant }.AsEnumerable());

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		// Expired grant should be deleted even though it wasn't revoked
		A.CallTo(() => _grantRepository.DeleteAsync(expiredGrant, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static Grant CreateGrant(string grantType, string qualifier)
	{
		var addedEvent = new GrantAdded(
			"target-user", "Target User", "TestApp", "tenant-1", grantType, qualifier,
			DateTimeOffset.UtcNow.AddDays(30), "admin", DateTimeOffset.UtcNow.AddDays(-1));
		return Grant.FromEvents($"target-user:tenant-1:{grantType}:{qualifier}", [addedEvent]);
	}

	private static Grant CreateExpiredGrant(string grantType, string qualifier)
	{
		var addedEvent = new GrantAdded(
			"target-user", "Target User", "TestApp", "tenant-1", grantType, qualifier,
			DateTimeOffset.UtcNow.AddDays(-1), "admin", DateTimeOffset.UtcNow.AddDays(-5));
		return Grant.FromEvents($"target-user:tenant-1:{grantType}:{qualifier}", [addedEvent]);
	}
}
