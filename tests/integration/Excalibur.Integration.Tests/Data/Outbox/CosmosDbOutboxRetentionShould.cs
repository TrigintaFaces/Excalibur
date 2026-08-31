// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Cosmos-emulator locks on <b>who expires</b>: a message that has not been delivered must never be
/// reaped, and a message that has been published must carry the configured retention.
/// </summary>
/// <remarks>
/// <para>
/// The store provisioned its container with a <b>positive</b> default time-to-live taken from the retention
/// option. In Cosmos a positive container default expires <i>every</i> item that does not override it — so
/// an undelivered message was deleted once the window elapsed. An outbox exists to hold a message until it
/// is delivered; that setting silently discarded exactly the messages it was there to protect. The option's
/// own documentation described it as retention for <i>published</i> messages, which is the opposite.
/// </para>
/// <para>
/// <b>These arms assert what was provisioned and stamped, not an observed expiry.</b> Waiting out a
/// retention window is not something a test can do, so the property is bound where it is decided: the
/// container's default, and the per-message value written at publish. That is a real bind — the defect was
/// entirely in those two values — but it is worth being explicit that no message was watched disappearing.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbOutboxRetentionShould
	: IClassFixture<CosmosDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private const int RetentionSeconds = 3600;

	private readonly CosmosDbOutboxStoreContainerFixture _fixture;
	private readonly List<(CosmosDbOutboxStore Store, string ContainerName)> _created = [];

	public CosmosDbOutboxRetentionShould(CosmosDbOutboxStoreContainerFixture fixture)
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
	/// SAFETY: the provisioned container expires nothing on its own, so an undelivered message survives.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task ProvisionAContainerThatExpiresNothingByDefault()
	{
		var (_, container) = await CreateStoreAsync().ConfigureAwait(false);

		var containerTtl = await _fixture.ReadContainerDefaultTtlAsync(container).ConfigureAwait(false);

		containerTtl.ShouldBe(
			-1,
			"the container must enable time-to-live while expiring nothing by default (-1), so that a "
			+ "message's lifetime is decided by the message. A positive default expires every item that "
			+ "does not override it, including messages that have never been delivered -- which is the one "
			+ "outcome an outbox exists to prevent.");
	}

	/// <summary>
	/// SAFETY: a staged, undelivered message carries no expiry of its own either.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The container default and the per-message value are two independent ways to expire the same message,
	/// so both are asserted. Fixing the container while stamping a retention at staging time would leave the
	/// defect intact and this arm is what would catch it.
	/// </remarks>
	[Fact]
	public async Task StampNoExpiryOnAMessageThatHasNotBeenPublished()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, container) = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
		addResult.Success.ShouldBeTrue($"staging must succeed: {addResult.ErrorMessage}");

		var ttl = await _fixture.ReadDocumentTtlAsync(container, message.MessageId, partitionKey.Value)
			.ConfigureAwait(false);

		ttl.ShouldBeNull(
			"an undelivered message must carry no time-to-live of its own -- it is held until it is "
			+ "delivered, however long that takes.");
	}

	/// <summary>
	/// LIVENESS: a published message does carry the configured retention.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the two above are satisfied perfectly by disabling expiry everywhere and never
	/// cleaning anything up, which would trade silent message loss for unbounded growth.
	/// </remarks>
	[Fact]
	public async Task StampTheConfiguredRetentionOnAMessageOncePublished()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, container) = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		_ = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);

		var published = await store.MarkAsPublishedAsync(message.MessageId, partitionKey, ct).ConfigureAwait(false);
		published.Success.ShouldBeTrue($"marking published must succeed: {published.ErrorMessage}");

		var ttl = await _fixture.ReadDocumentTtlAsync(container, message.MessageId, partitionKey.Value)
			.ConfigureAwait(false);

		ttl.ShouldBe(
			RetentionSeconds,
			"a published message must carry the configured retention, so cleanup still happens where it is "
			+ "wanted. Expiring nothing anywhere would satisfy the safety arms and grow forever.");
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

	private async Task<(CosmosDbOutboxStore Store, string ContainerName)> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue("The Cosmos DB emulator must be available -- never skipped.");

		var container = $"outbox_{Guid.NewGuid():N}";
		var options = new CosmosDbOutboxOptions
		{
			DatabaseName = _fixture.DatabaseName,
			ContainerName = container,
			CreateContainerIfNotExists = true,
			DefaultTimeToLiveSeconds = RetentionSeconds,
			UseDirectMode = false,
			HttpClientFactory = () => _fixture.EmulatorHttpClient,
			Connection = new CosmosDbOutboxConnectionOptions { ConnectionString = _fixture.ConnectionString }
		};

		var store = new CosmosDbOutboxStore(Options.Create(options), NullLogger<CosmosDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, container));
		return (store, container);
	}
}
