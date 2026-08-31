// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Saga.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance.Saga;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Binds the refusal that stands between an unmigrated Cosmos saga container and a silently restarted saga:
/// a document written under the pre-tenant identifier is unaddressable now, so a load of the saga it holds
/// reports NO SAGA IN FLIGHT - which the caller reads as a saga that has not begun, so it starts the saga
/// over and re-fires every compensating action and external call that has already happened. On the create
/// path the same silence lets a second, duplicate saga be created beside the original: the point read
/// returns 404, so the store takes the create branch, and <c>CreateItemAsync</c> addresses the NEW
/// identifier and therefore never conflicts with the original.
/// </summary>
/// <remarks>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed document and requires
/// an absent saga to load as <see langword="null"/> and a new saga to then be creatable. That reaches the
/// probe rather than bypassing it - an absent saga is exactly what triggers it - so the arm proves the
/// probe comes back clean, not merely that it never ran.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbSagaStoreLegacyKeyRefusalShould : IClassFixture<CosmosDbSagaStoreContainerFixture>
{
	private const string TenantA = "tenant-A";

	private readonly CosmosDbSagaStoreContainerFixture _fixture;

	// One container per test instance. xUnit builds a fresh instance per arm, so neither arm can observe
	// what the other seeded.
	private readonly string _containerName = "saga_legacy_key_" + Guid.NewGuid().ToString("N");

	public CosmosDbSagaStoreLegacyKeyRefusalShould(CosmosDbSagaStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY: a container still holding a document written without a tenant segment is refused, by name,
	/// before it can be read back as "no saga in flight".
	/// </summary>
	[Fact]
	public async Task Refuse_a_container_holding_a_document_written_without_a_tenant_segment()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"the Cosmos emulator must be available - this arm exists to prove a real container is refused, "
			+ $"so it is never skipped: {_fixture.InitError}");

		await ProvisionContainerAsync().ConfigureAwait(false);

		// The shape an earlier release wrote on this provider: the saga identifier alone as the document id.
		var legacySagaId = Guid.NewGuid();
		var legacyDocumentId = legacySagaId.ToString();
		await SeedDocumentAsync(legacyDocumentId, legacySagaId).ConfigureAwait(false);

		var loadRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.LoadAsync<TestSagaState>(legacySagaId, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		loadRefusal.Message.ShouldContain(
			_containerName,
			Case.Sensitive,
			"the refusal must name the container a consumer has to re-key, or it cannot be acted on");

		loadRefusal.Message.ShouldContain(
			legacyDocumentId,
			Case.Sensitive,
			"naming the offending identifier is what lets a consumer confirm which documents are affected");

		// The create path is guarded separately, and it is the one that produces a DUPLICATE saga rather
		// than a restarted one: CreateItemAsync addresses the new identifier, so the legacy document does
		// not produce a 409 and nothing refuses the second create.
		var createRefusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await CreateStore(TenantA)
				.SaveAsync(
					new TestSagaState { SagaId = Guid.NewGuid(), TenantId = TenantA },
					CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);

		createRefusal.Message.ShouldContain(
			_containerName,
			Case.Sensitive,
			"a create must refuse for the same reason a load does, and name the same container");

		// The refusal is a refusal, not a repair: the document is still exactly where it was, and the
		// refused create wrote nothing beside it.
		(await CountDocumentsAsync().ConfigureAwait(false)).ShouldBe(
			1,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the "
			+ "data - and the refused create must not have written its own document");
	}

	/// <summary>
	/// LIVENESS: a container whose documents all carry a tenant segment is served normally. Without this arm
	/// a probe that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_container_whose_documents_all_carry_a_tenant_segment()
	{
		_fixture.IsInitialized.ShouldBeTrue(
			"the Cosmos emulator must be available - a correctly-keyed container must remain fully usable, "
			+ $"so this arm is never skipped: {_fixture.InitError}");

		// Written through the store, so the seeded document carries exactly the identifier this release
		// composes rather than one the test invented. This is also the empty-container case: a brand-new
		// deployment must not be refused.
		await CreateStore(TenantA)
			.SaveAsync(new TestSagaState { SagaId = Guid.NewGuid(), TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		// A fresh instance, so its probe has not already run: the load below is its first absence decision.
		var store = CreateStore(TenantA);

		var loaded = await store
			.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		loaded.ShouldBeNull("a saga that was never started must load as null, not refuse");

		// An absent read proves less than a write does: the store must actually remain usable.
		var newSagaId = Guid.NewGuid();
		await store
			.SaveAsync(new TestSagaState { SagaId = newSagaId, TenantId = TenantA }, CancellationToken.None)
			.ConfigureAwait(false);

		var reloaded = await store
			.LoadAsync<TestSagaState>(newSagaId, CancellationToken.None)
			.ConfigureAwait(false);

		_ = reloaded.ShouldNotBeNull("a correctly-keyed container must remain fully writable");
	}

	private CosmosDbSagaStore CreateStore(string? tenantId) =>
		new(
			_fixture.Client,
			Options.Create(new CosmosDbSagaOptions
			{
				Client = new CosmosDbClientOptions
				{
					ConnectionString = _fixture.ConnectionString,
					HttpClientFactory = () => _fixture.EmulatorHttpClient,
				},
				DatabaseName = _fixture.DatabaseName,
				ContainerName = _containerName,
				PartitionKeyPath = "/sagaType",
				CreateContainerIfNotExists = true,
				ContainerThroughput = 400,
			}),
			NullLogger<CosmosDbSagaStore>.Instance,
			new DispatchJsonSerializer(),
			new FixedTenantContext(tenantId));

	/// <summary>
	/// Creates the container through the store itself, which is the only thing in the system that knows the
	/// partition-key shape it expects. The load it performs is the empty-container arm of the probe: it must
	/// not refuse.
	/// </summary>
	private async Task ProvisionContainerAsync()
	{
		var loaded = await CreateStore(TenantA)
			.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None)
			.ConfigureAwait(false);

		loaded.ShouldBeNull("a newly provisioned, empty container holds nothing to refuse");
	}

	private Container SagaContainer() =>
		_fixture.Client.GetDatabase(_fixture.DatabaseName).GetContainer(_containerName);

	// Seeded through the raw client rather than through the store, because the store can no longer write the
	// shape under test - that is the whole point of the change this locks. A dictionary rather than a typed
	// object so the persisted keys are exactly these, whichever serializer the client is configured with.
	private async Task SeedDocumentAsync(string documentId, Guid sagaId) =>
		_ = await SagaContainer().CreateItemAsync(
			new Dictionary<string, object>
			{
				["id"] = documentId,
				["sagaId"] = sagaId.ToString(),
				["sagaType"] = nameof(TestSagaState),
				["stateJson"] = "{}",
				["isCompleted"] = false,
				["version"] = 1L,
				["createdUtc"] = DateTimeOffset.UtcNow,
				["updatedUtc"] = DateTimeOffset.UtcNow,
			},
			new PartitionKey(nameof(TestSagaState)),
			cancellationToken: CancellationToken.None).ConfigureAwait(false);

	private async Task<int> CountDocumentsAsync()
	{
		var count = 0;
		using var iterator = SagaContainer().GetItemQueryIterator<string>(
			new QueryDefinition("SELECT VALUE c.id FROM c"));

		while (iterator.HasMoreResults)
		{
			var page = await iterator.ReadNextAsync(CancellationToken.None).ConfigureAwait(false);
			count += page.Count;
		}

		return count;
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
