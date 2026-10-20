using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Events;
using Excalibur.A3.Authorization.Grants;

using GrantAggregate = Excalibur.A3.Authorization.Grants.Grant;
using Excalibur.A3.Exceptions;
using Excalibur.Domain;
using Excalibur.Domain.Model;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Grants;

[Collection("ApplicationContext")]
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class RevokeGrantCommandHandlerShould
{
	private readonly IGrantRepository _grantRepository = A.Fake<IGrantRepository>();

	public RevokeGrantCommandHandlerShould()
	{
		// Grant.Revoke() reads ApplicationContext.ApplicationName (static)
		ApplicationContext.Init(new Dictionary<string, string?>
		{
			["ApplicationName"] = "TestApp",
		});
	}

	private RevokeGrantCommandHandler CreateSut() => new(_grantRepository);

	private static RevokeGrantCommand CreateValidCommand(
		string userId = "target-user",
		string grantType = "ActivityGroup",
		string qualifier = "orders",
		string? tenantId = "tenant-1")
	{
		var command = new RevokeGrantCommand(userId, grantType, qualifier, Guid.NewGuid(), tenantId);
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
		var command = new RevokeGrantCommand("user-1", "Role", "admin", Guid.NewGuid(), "tenant-1");
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
		var command = CreateValidCommand(userId: "self-user");
		command.AccessToken = CreateFakeAccessToken("self-user", "Self User");

		// Act & Assert
		await Should.ThrowAsync<NotAuthorizedException>(
			() => sut.HandleAsync(command, CancellationToken.None));
	}

	[Fact]
	public async Task Return_failure_when_grant_not_found()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns((GrantAggregate?)null);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeFalse();
		result.AuditMessage.ShouldContain("not found");
	}

	[Fact]
	public async Task Revoke_and_delete_existing_grant()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();
		var existingGrant = CreateActiveGrant();

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns(existingGrant);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		A.CallTo(() => _grantRepository.DeleteAsync(existingGrant, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	// Removed: Invalidate_cache_after_revoking_grant. It asserted that the handler CALLED
	// IDistributedCache.RemoveAsync, which it did -- with the bare grants key, while the reader composes
	// key + "revision-v1" + revision. The two could never name the same entry, so the removal was inert
	// and this assertion stayed green for the whole time it did nothing. It bound the mechanism rather
	// than the property, which is the shape that hides an inert control behind a passing test.
	//
	// The property -- a revoked grant is never served from cache -- is bound by
	// AuthorizationPolicyRevisionShould (Refresh_an_already_acquired_scope_after_revocation,
	// Fence_a_late_old_fill_and_refresh_its_original_acquisition), against the revision key that
	// actually fences reads. Coverage of the guarantee is unchanged by this removal.

	[Fact]
	public async Task Return_auditable_result_with_revoke_message()
	{
		// Arrange
		var sut = CreateSut();
		var command = CreateValidCommand();
		var existingGrant = CreateActiveGrant();

		A.CallTo(() => _grantRepository.GetByIdAsync(A<string>._, A<CancellationToken>._))
			.Returns(existingGrant);

		// Act
		var result = await sut.HandleAsync(command, CancellationToken.None);

		// Assert
		result.Result.ShouldBeTrue();
		result.AuditMessage.ShouldContain("Revoked from");
		result.AuditMessage.ShouldContain("Admin User");
	}

	private static GrantAggregate CreateActiveGrant()
	{
		var addedEvent = new GrantAdded(
			"target-user", "Target User", "TestApp", "tenant-1", "ActivityGroup", "orders",
			DateTimeOffset.UtcNow.AddDays(30), "admin", DateTimeOffset.UtcNow.AddDays(-1));
		return GrantAggregate.FromEvents("target-user:tenant-1:ActivityGroup:orders", [new HistoricEvent(addedEvent, 0)]);
	}
}
