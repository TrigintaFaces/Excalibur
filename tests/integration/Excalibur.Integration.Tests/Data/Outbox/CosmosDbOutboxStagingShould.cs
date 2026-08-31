// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Cosmos-emulator lock on the first step of the change-feed pattern: that a message can be staged
/// at all.
/// </summary>
/// <remarks>
/// <para>
/// This is not a formality. The store declared a nullable per-document time-to-live, never assigned it on
/// the staging path, and built a client whose serializer emits nulls — so every staged message carried an
/// explicit <c>ttl</c> of null, which Cosmos rejects outright with <c>400 BadRequest</c>: <i>"The specified
/// document 'ttl' value is invalid. Valid values are -1 or a non-zero positive 32-bit integer."</i> Staging
/// therefore failed for every message, under the shipped defaults, on first use.
/// </para>
/// <para>
/// It stayed invisible because nothing could reach the emulator to find out — the store was the only Cosmos
/// store in this framework with no way for a caller to supply an HttpClient, so no real-infrastructure test
/// for it had ever existed. A mocked container cannot substitute here: the rejection is the *server's*
/// validation of a document property, and a mock returns whatever it was told.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbOutboxStagingShould
	: IClassFixture<CosmosDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbOutboxStoreContainerFixture _fixture;
	private readonly List<(CosmosDbOutboxStore Store, string ContainerName)> _created = [];

	public CosmosDbOutboxStagingShould(CosmosDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		var containers = new HashSet<string>(StringComparer.Ordinal);

		foreach (var (store, containerName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			_ = containers.Add(containerName);
		}

		foreach (var containerName in containers)
		{
			await _fixture.CleanupContainerAsync(containerName).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: a message staged through the single-message path is accepted by the server and reads back.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The round-trip is deliberate. Asserting only that the call reported success would still pass against a
	/// store that swallowed the server's rejection, and asserting only that the server accepted *something*
	/// would pass against one that wrote a document nobody can find again.
	/// </remarks>
	[Fact]
	public async Task AcceptAStagedMessage_AndReadItBack()
	{
		var ct = TestContext.Current.CancellationToken;
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var message = CreateMessage(partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);

		addResult.Success.ShouldBeTrue(
			"staging a message is the first step of the change-feed pattern and must be accepted by the "
			+ $"server. The server said: {addResult.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 10, ct).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, message.MessageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("a staged message must be readable afterwards, not merely accepted");
		own.MessageType.ShouldBe(message.MessageType);
		own.Payload.ShouldBe(message.Payload);
	}

	/// <summary>
	/// LIVENESS: the batch path is accepted too — it builds its documents through the same mapping.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// A second arm rather than a variation of the first, because the defect lived in the shared mapping and
	/// reached the server through two independent write paths. Fixing the one the single-message path uses
	/// while leaving the batch path broken would be indistinguishable, from the first arm alone, from fixing
	/// both.
	/// </remarks>
	[Fact]
	public async Task AcceptABatchOfStagedMessages_AndReadThemBack()
	{
		var ct = TestContext.Current.CancellationToken;
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var messages = Enumerable.Range(0, 3).Select(_ => CreateMessage(partitionKey.Value)).ToList();

		var batchResult = await store.AddBatchAsync(messages, partitionKey, ct).ConfigureAwait(false);

		batchResult.Success.ShouldBeTrue(
			"the batch staging path builds its documents through the same mapping as the single-message "
			+ $"path and must also be accepted. The server said: {batchResult.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 10, ct).ConfigureAwait(false);

		pending.Documents
			.Select(m => m.MessageId)
			.Order(StringComparer.Ordinal)
			.ShouldBe(
				messages.Select(m => m.MessageId).Order(StringComparer.Ordinal),
				"every message in the batch must be staged, not just the first");
	}

	private static CloudOutboxMessage CreateMessage(string partitionKeyValue) =>
		new()
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKeyValue
		};

	private async Task<CosmosDbOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("The Cosmos DB emulator must be available -- never skipped.");

		var container = $"outbox_{Guid.NewGuid():N}";
		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = container,
			CreateContainerIfNotExists = true,

			// Gateway plus the emulator's own HttpClient; a default client cannot reach it.
			UseDirectMode = false,
			HttpClientFactory = () => _fixture.EmulatorHttpClient,
			Connection = new CosmosDbOutboxConnectionOptions { ConnectionString = _fixture.ConnectionString }
		};

		var store = new CosmosDbOutboxStore(Options.Create(options), NullLogger<CosmosDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, container));
		return store;
	}
}
