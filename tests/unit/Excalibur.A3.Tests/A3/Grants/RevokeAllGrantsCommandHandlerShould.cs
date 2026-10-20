using Excalibur.A3;
using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.A3.Authorization.Stores.InMemory;
using Excalibur.A3.Exceptions;
using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Tests.A3.Grants;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Pattern", "AtomicRevocation")]
public sealed class RevokeAllGrantsCommandHandlerShould
{
	[Theory]
	[InlineData("tenant-1")]
	[InlineData(null)]
	public void Preserve_operation_and_explicit_scope_across_command_serialization(string? tenant)
	{
		var command = new RevokeAllGrantsCommand("target", "Target", Guid.NewGuid(), tenant);
		var encoded = System.Text.Json.JsonSerializer.Serialize(command);
		var decoded = System.Text.Json.JsonSerializer.Deserialize<RevokeAllGrantsCommand>(encoded)!;
		decoded.OperationId.ShouldBe(command.OperationId);
		decoded.CorrelationId.ShouldBe(command.CorrelationId);
		decoded.RevocationScope.ShouldBe(command.RevocationScope);
	}
	[Theory]
	[InlineData("{\"TenantId\":\"\"}")]
	[InlineData("{\"TenantId\":\" tenant-1\"}")]
	[InlineData("{}")]
	[InlineData("{\"TenantId\":null}")]
	public void Reject_malformed_scope_serialization(string encoded) =>
		Should.Throw<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<GrantRevocationScope>(encoded));

	private readonly InMemoryGrantStore _store = new();
	private readonly ITenantContext _tenant = A.Fake<ITenantContext>();
    public RevokeAllGrantsCommandHandlerShould() => A.CallTo(() => _tenant.TenantId).Returns("tenant-1");
	private static RevokeAllGrantsCommand Command(string? tenant = "tenant-1")
	{
		var token = A.Fake<IAccessToken>();
		A.CallTo(() => token.UserId).Returns("admin");
		A.CallTo(() => token.FullName).Returns("Administrator");
		return new RevokeAllGrantsCommand("target", "Target", Guid.NewGuid(), tenant) { AccessToken = token };
	}
	private Task Add(string tenant, string kind) => _store.SaveGrantAsync(
		new Excalibur.A3.Authorization.Grant("target", "Target", tenant, kind, "read", null, "admin", DateTimeOffset.UtcNow), CancellationToken.None);
	[Fact]
	public async Task Revoke_all_types_only_in_requested_tenant()
	{
		await Add("tenant-1", "activity");
		await Add("tenant-1", GrantType.ActivityGroup);
		await Add("tenant-2", "activity");
		var result = await new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(Command(), CancellationToken.None);
		result.Result.ShouldBeTrue();
		var remaining = await _store.GetAllGrantsAsync("target", true, CancellationToken.None);
		remaining.Count.ShouldBe(1);
		remaining[0].TenantId.ShouldBe("tenant-2");
	}
	[Fact]
	public async Task Refuse_global_scope_under_tenant_authorization()
	{
		await Add("tenant-1", GrantType.ActivityGroup);
		await Add("tenant-2", "activity");
		await Should.ThrowAsync<NotAuthorizedException>(() => new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(Command(null), CancellationToken.None));
		(await _store.GetAllGrantsAsync("target", true, CancellationToken.None)).Count.ShouldBe(2);
	}
	[Theory]
	[InlineData("tenant-2", "tenant-1")]
	[InlineData("tenant-1", "tenant-2")]
	public async Task Reject_scope_or_ambient_tenant_mismatch_before_mutation(string scopeTenant, string ambientTenant)
	{
		await Add("tenant-1", GrantType.ActivityGroup);
		await Add("tenant-2", GrantType.ActivityGroup);
		var command = Command();
		command.RevocationScope = GrantRevocationScope.ForTenant(scopeTenant);
		A.CallTo(() => _tenant.TenantId).Returns(ambientTenant);
		await Should.ThrowAsync<NotAuthorizedException>(() => new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(command, CancellationToken.None));
		(await _store.GetAllGrantsAsync("target", true, CancellationToken.None)).Count.ShouldBe(2);
	}
	[Fact]
	public async Task Refuse_unsupported_atomic_store_without_mutation()
	{
		var store = A.Fake<IGrantStore>();
		A.CallTo(() => store.GetService(typeof(IAtomicGrantRevocationStore))).Returns(null);
		await Should.ThrowAsync<NotSupportedException>(() => new RevokeAllGrantsCommandHandler(store, _tenant).HandleAsync(Command(), CancellationToken.None));
		A.CallTo(() => store.DeleteGrantAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<string?>._, A<DateTimeOffset?>._, A<CancellationToken>._)).MustNotHaveHappened();
	}
	/// <summary>
	/// A replayed revocation returns its recorded commit and does not revoke grants added after it.
	/// </summary>
	/// <remarks>
	/// This arm previously reached the retry by making <c>IDistributedCache.RemoveAsync</c> throw, and
	/// closed by asserting that removal happened exactly twice. Both halves bound a cache call that was
	/// inert: the handler passed the bare grants key while the reader composes key + revision, so the two
	/// could never name the same entry. The handler no longer takes a cache at all -- the store advances
	/// the user's revision inside its own commit, which is what fences the read.
	///
	/// The property that arm was really protecting survives and is asserted directly here, without
	/// injecting a failure into a mechanism that no longer exists: replaying the same command must not
	/// revoke a grant added after the first commit.
	/// </remarks>
	[Fact]
	public async Task Replay_a_revocation_without_revoking_later_additions()
	{
		await Add("tenant-1", "activity");
		var command = Command();
		var handler = new RevokeAllGrantsCommandHandler(_store, _tenant);

		(await handler.HandleAsync(command, CancellationToken.None)).Result.ShouldBeTrue();
		(await _store.GetAllGrantsAsync("target", true, CancellationToken.None)).ShouldBeEmpty();

		await Add("tenant-1", GrantType.ActivityGroup);

		(await handler.HandleAsync(command, CancellationToken.None)).Result.ShouldBeTrue();
		(await _store.GetAllGrantsAsync("target", true, CancellationToken.None)).Count.ShouldBe(
			1,
			"the replay returns the recorded commit and cannot reach a grant added after it");
	}
	[Fact]
	public async Task Cancel_before_commit_without_removing_grants()
	{
		await Add("tenant-1", "activity");
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(Command(), cancellation.Token));
		(await _store.GetAllGrantsAsync("target", true, CancellationToken.None)).Count.ShouldBe(1);
	}
	[Fact]
	public async Task Reject_self_administration()
	{
		var command = Command();
		command.UserId = "admin";
		await Should.ThrowAsync<NotAuthorizedException>(() => new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(command, CancellationToken.None));
	}
	[Fact]
	public async Task Require_access_token()
	{
		var command = Command();
		command.AccessToken = null;
		await Should.ThrowAsync<InvalidOperationException>(() => new RevokeAllGrantsCommandHandler(_store, _tenant).HandleAsync(command, CancellationToken.None));
	}
}

