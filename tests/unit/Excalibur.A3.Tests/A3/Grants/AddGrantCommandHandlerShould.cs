using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Exceptions;
using Excalibur.Domain;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Grants;

[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AddGrantCommandHandlerShould
{
	private readonly IGrantRepository _grantRepository = A.Fake<IGrantRepository>();
	private readonly IDistributedCache _cache = A.Fake<IDistributedCache>();

	public AddGrantCommandHandlerShould()
	{
		// Grant constructor reads ApplicationContext.ApplicationName (static)
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
			["AuthorizationCacheKey"] = "test-cache",
		});
	}

	private AddGrantCommandHandler CreateSut() => new(_grantRepository, _cache);

	private static AddGrantCommand CreateValidCommand(
		string userId = "target-user",
		string fullName = "Target User",
		string grantType = "ActivityGroup",
		string qualifier = "orders",
		DateTimeOffset? expiresOn = null,
		string? tenantId = "tenant-1")
	{
		var command = new AddGrantCommand(userId, fullName, grantType, qualifier, expiresOn, Guid.NewGuid(), tenantId);
		command.AccessToken = CreateFakeAccessToken("admin-user", "Admin User");
		return command;
	}

	private static IAccessToken CreateFakeAccessToken(string userId, string fullName)
	{
		var token = A.Fake<IAccessToken>();
		A.CallTo(() => token.UserId).Returns(userId);
		A.CallTo(() => token.FullName).Returns(fullName);
		return token;
	}

	[Fact]
	public async Task Throw_when_access_token_is_null()
	{
		// Arrange
		var sut = CreateSut();
		var command = new AddGrantCommand("user-1", "User", "Role", "admin", null, Guid.NewGuid(), "tenant-1");
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
	public async Task Return_failure_when_active_grant_already_exists()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		// Set up existing non-expired grant
		var existingGrant = CreateActiveGrant();
		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns(existingGrant);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeFalse();
		result.AuditMessage.ShouldContain("already in effect");
	}

	[Fact]
	public async Task Delete_expired_grant_and_create_new_one()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		// Set up existing expired grant
		var expiredGrant = CreateExpiredGrant();
		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns(expiredGrant);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		A.CallTo(() => _grantRepository.DeleteAsync(expiredGrant, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _grantRepository.SaveAsync(A<Grant>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Create_new_grant_when_none_exists()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns((Grant?)null);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldContain("Granted to");
		A.CallTo(() => _grantRepository.SaveAsync(A<Grant>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Invalidate_cache_after_creating_grant()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns((Grant?)null);

		// Act
		await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		A.CallTo(() => _cache.RemoveAsync(A<string>.That.Contains("target-user"), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Return_auditable_result_with_success_message()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand(fullName: "Jane Smith");

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns((Grant?)null);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldContain("Jane Smith");
		result.AuditMessage.ShouldContain("Admin User");
	}

	private static Grant CreateActiveGrant()
	{
		var addedEvent = new Excalibur.A3.Authorization.Events.GrantAdded(
			"target-user", "Target User", "TestApp", "tenant-1", "ActivityGroup", "orders",
			DateTimeOffset.UtcNow.AddDays(30), "admin", DateTimeOffset.UtcNow.AddDays(-1));
		return Grant.FromEvents("target-user:tenant-1:ActivityGroup:orders", [addedEvent]);
	}

	private static Grant CreateExpiredGrant()
	{
		var addedEvent = new Excalibur.A3.Authorization.Events.GrantAdded(
			"target-user", "Target User", "TestApp", "tenant-1", "ActivityGroup", "orders",
			DateTimeOffset.UtcNow.AddDays(-1), "admin", DateTimeOffset.UtcNow.AddDays(-5));
		return Grant.FromEvents("target-user:tenant-1:ActivityGroup:orders", [addedEvent]);
	}
}
