// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Azure;
using Azure.Security.KeyVault.Keys;

using Excalibur.Compliance.Azure;
using Excalibur.Compliance.Tests.Erasure;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Azure;

/// <summary>
/// An erasure on Azure Key Vault reaches a terminal state instead of stalling in PartiallyCompleted, and never
/// reaches Completed while the vault can still recover the key.
/// </summary>
/// <remarks>
/// Key Vault soft-deletes. Without purge protection, and with a credential allowed to purge, the key is purged at
/// once and the erasure completes on execution. Otherwise the key is recoverable until the vault purges it, and a
/// soft-deleted key answers 404 on the ordinary lookup -- the same answer a purged one gives -- so completion must
/// be confirmed against the vault's deleted-keys collection, not the lookup.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AzureKeyVaultErasureReachesTerminalShould : IDisposable
{
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly KeyClient _keyClient = A.Fake<KeyClient>(options => options.WithArgumentsForConstructor(() =>
		new KeyClient(new Uri("https://unit-tests.vault.azure.net/"), new global::Azure.Identity.DefaultAzureCredential())));
	private readonly AzureKeyVaultProvider _provider;

	/// <summary>
	/// Whether the vault has accepted a delete yet, so the ordinary lookup can answer differently either side
	/// of it.
	/// </summary>
	/// <remarks>
	/// This flag exists because erasure reads the key BEFORE destroying it, to learn the opaque VERSION that
	/// Key Vault uses as the generation -- the destruction destroys that identifier, so the read has one
	/// chance and it is before. A fake that answered 404 from the start modelled only the post-delete vault,
	/// so every erasure here had no generation to record and correctly declined to attest a destruction it
	/// could not put on file. The arm then failed for a reason that had nothing to do with purging.
	/// </remarks>
	private bool _deleted;

	public AzureKeyVaultErasureReachesTerminalShould()
	{
		// Supplied through the constructor, not reflected onto the private field. The reflection this replaced
		// worked, but it is not a seam a consumer has, so it could not tell us the provider was unreachable from
		// a test by any supported route -- which is how the field-decrypt defect shipped with no arm.
		_provider = new AzureKeyVaultProvider(
			options: Microsoft.Extensions.Options.Options.Create(new AzureKeyVaultOptions
			{
				VaultUri = new Uri("https://unit-tests.vault.azure.net/"),
				KeyNamePrefix = "dispatch-",
			}),
			cache: _cache,
			logger: NullLogger<AzureKeyVaultProvider>.Instance,
			keyClient: _keyClient);

		var operation = A.Fake<DeleteKeyOperation>();
		A.CallTo(() => operation.WaitForCompletionAsync(A<CancellationToken>._))
			.Returns(new ValueTask<Response<DeletedKey>>(Response.FromValue(DeletedKeyNamed("dispatch-x"), A.Fake<Response>())));
		A.CallTo(() => _keyClient.StartDeleteKeyAsync(A<string>._, A<CancellationToken>._))
			.Invokes(() => _deleted = true)
			.Returns(Task.FromResult(operation));

		// BEFORE the delete the key is live and the lookup returns it, version and all. AFTER the delete the
		// ordinary lookup answers 404 -- for a soft-deleted key AND for a purged one, which is the whole reason
		// completion is confirmed against the deleted-keys collection instead.
		A.CallTo(() => _keyClient.GetKeyAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() => _deleted
				? throw new RequestFailedException(404, "KeyNotFound")
				: Task.FromResult(Response.FromValue(LiveKeyNamed("dispatch-x"), A.Fake<Response>())));
	}

	public void Dispose()
	{
		_provider.Dispose();
		_cache.Dispose();
	}

	[Fact]
	public async Task Complete_on_execution_when_the_vault_accepts_the_purge()
	{
		A.CallTo(() => _keyClient.PurgeDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(A.Fake<Response>()));

		// A PURGED key is gone from the deleted-keys collection too, and the vault says so with a 404. Without
		// this the fake answers the deleted-key lookup with a default value, so the vault reports the key as
		// still recoverable -- and the erasure correctly declines to attest completion over it. Stating the
		// post-purge answer is what makes this arm about the purge succeeding rather than about an
		// under-specified fake.
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "DeletedKeyNotFound"));

		var harness = new ErasureLifecycleHarness(_provider, _provider);

		var (requestId, _, result) = await harness.SubmitAndExecuteAsync("azure-subject-1");

		result.Success.ShouldBeTrue();
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
	}

	[Fact]
	public async Task Fall_back_to_awaiting_destruction_when_the_vault_refuses_the_purge()
	{
		// Purge protection (or no purge permission): the refusal must degrade to a revisitable state -- never to
		// Completed, and never to an exception that strands the request.
		RefusePurge();
		SoftDeletedKeyStillRecoverable();
		var harness = new ErasureLifecycleHarness(_provider, _provider);

		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("azure-subject-2");

		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
	}

	[Fact]
	public async Task Not_complete_while_the_vault_holds_the_soft_deleted_key()
	{
		RefusePurge();
		SoftDeletedKeyStillRecoverable();
		var harness = new ErasureLifecycleHarness(_provider, _provider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("azure-subject-3");

		// The ordinary lookup says 404. That is NOT destruction: the key is in the deleted-keys collection.
		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(0);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.AwaitingKeyDestruction);
		(await harness.Store.GetCertificateAsync(requestId, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task Complete_once_the_vault_has_purged_the_key()
	{
		RefusePurge();
		SoftDeletedKeyStillRecoverable();
		var harness = new ErasureLifecycleHarness(_provider, _provider);
		var (requestId, _, _) = await harness.SubmitAndExecuteAsync("azure-subject-4");
		_ = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		// The retention period ends and the vault purges the key.
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "DeletedKeyNotFound"));

		var completed = await harness.Processor.CompletePendingErasuresAsync(CancellationToken.None);

		completed.ShouldBe(1);
		(await harness.StatusOfAsync(requestId)).ShouldBe(ErasureRequestStatus.Completed);
		(await harness.Service.GenerateCertificateAsync(requestId, CancellationToken.None))
			.Payload.Verification.Verified.ShouldBeTrue();
	}

	[Fact]
	public async Task Not_report_an_already_soft_deleted_key_as_never_having_existed()
	{
		// A retry after an earlier soft-delete: the live key is gone (404), but the vault still holds it. Reporting
		// NotFound would let an erasure attest "nothing to destroy" over a recoverable key.
		A.CallTo(() => _keyClient.StartDeleteKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "KeyNotFound"));
		RefusePurge();
		SoftDeletedKeyStillRecoverable();

		var outcome = await _provider.DeleteKeyAsync("x", 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.ScheduledIrreversible);
	}

	[Fact]
	public async Task Report_a_key_the_vault_holds_nowhere_as_not_found()
	{
		A.CallTo(() => _keyClient.StartDeleteKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "KeyNotFound"));
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(404, "DeletedKeyNotFound"));

		var outcome = await _provider.DeleteKeyAsync("x", 0, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.NotFound);
	}

	[Fact]
	public async Task Not_purge_when_a_retention_window_was_requested()
	{
		SoftDeletedKeyStillRecoverable();

		var outcome = await _provider.DeleteKeyAsync("x", 30, CancellationToken.None);

		outcome.State.ShouldBe(KeyDestructionState.ScheduledIrreversible);
		A.CallTo(() => _keyClient.PurgeDeletedKeyAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	/// <summary>
	/// A live vault key carrying an opaque version, which is what the provider reports as the generation.
	/// </summary>
	// A key THIS PROVIDER PROVISIONED, which means it carries the lineage tag the provider writes at
	// creation. Without the tag the provider reports no generation -- correctly, because a key it did not
	// provision has no lineage identity it can state -- and an erasure then declines to attest a destruction
	// it cannot name. That refusal is the designed behaviour, so a fake omitting the tag makes every arm here
	// about an under-specified fake rather than about the vault operation under test. Same reason the
	// post-purge 404 is stated explicitly above.
	private static KeyVaultKey LiveKeyNamed(string name, string generation = "a1b2c3d4e5f60718293a4b5c6d7e8f90")
	{
		var properties = KeyModelFactory.KeyProperties(
			id: new Uri($"https://unit-tests.vault.azure.net/keys/{name}/v1"),
			vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
			name: name,
			version: "v1");

		properties.Tags["excalibur:generation"] = generation;

		return KeyModelFactory.KeyVaultKey(
			properties,
			KeyModelFactory.JsonWebKey(KeyType.Oct, id: null, keyOps: []));
	}

	[Fact]
	public async Task Report_a_live_key_as_not_destroyed()
	{
		var live = LiveKeyNamed("dispatch-x");
		A.CallTo(() => _keyClient.GetKeyAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(live, A.Fake<Response>())));

		(await _provider.IsKeyDestroyedAsync("x", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task Not_report_destroyed_when_the_key_is_recovered_between_the_two_reads()
	{
		// The deleted and live collections cannot be read atomically. The key is soft-deleted when the live
		// collection is read (404), and an operator recovers it before the deleted collection is read (404 too,
		// since it is live again). Reading the deleted collection on BOTH sides of the live read catches it.
		// Model: soft-deleted at first; the operator's recovery lands immediately after the live read.
		var softDeleted = true;
		var live = KeyModelFactory.KeyVaultKey(
			KeyModelFactory.KeyProperties(
				id: new Uri("https://unit-tests.vault.azure.net/keys/dispatch-x/v1"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: "dispatch-x",
				version: "v1"),
			KeyModelFactory.JsonWebKey(KeyType.Oct, id: null, keyOps: []));
		A.CallTo(() => _keyClient.GetKeyAsync(A<string>._, A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (softDeleted)
				{
					softDeleted = false; // recovered right after this read
					throw new RequestFailedException(404, "KeyNotFound");
				}

				return Task.FromResult(Response.FromValue(live, A.Fake<Response>()));
			});
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() => softDeleted
				? Task.FromResult(Response.FromValue(DeletedKeyNamed("dispatch-x"), A.Fake<Response>()))
				: throw new RequestFailedException(404, "DeletedKeyNotFound"));

		(await _provider.IsKeyDestroyedAsync("x", CancellationToken.None)).ShouldBeFalse();
	}

	[Fact]
	public async Task Throw_rather_than_report_destroyed_when_the_vault_cannot_be_asked()
	{
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(403, "Forbidden"));

		_ = await Should.ThrowAsync<RequestFailedException>(() => _provider.IsKeyDestroyedAsync("x", CancellationToken.None));
	}

	private static DeletedKey DeletedKeyNamed(string name) =>
		KeyModelFactory.DeletedKey(
			KeyModelFactory.KeyProperties(
				id: new Uri($"https://unit-tests.vault.azure.net/keys/{name}/v1"),
				vaultUri: new Uri("https://unit-tests.vault.azure.net/"),
				name: name,
				version: "v1",
				recoveryLevel: "Recoverable"),
			KeyModelFactory.JsonWebKey(KeyType.Oct, id: null, keyOps: []),
			recoveryId: new Uri($"https://unit-tests.vault.azure.net/deletedkeys/{name}"),
			deletedOn: DateTimeOffset.UtcNow,
			scheduledPurgeDate: DateTimeOffset.UtcNow.AddDays(90));

	private void RefusePurge() =>
		A.CallTo(() => _keyClient.PurgeDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Throws(new RequestFailedException(409, "Operation \"purge\" is not allowed because purge protection is enabled"));

	private void SoftDeletedKeyStillRecoverable() =>
		A.CallTo(() => _keyClient.GetDeletedKeyAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(Response.FromValue(DeletedKeyNamed("dispatch-x"), A.Fake<Response>())));
}
