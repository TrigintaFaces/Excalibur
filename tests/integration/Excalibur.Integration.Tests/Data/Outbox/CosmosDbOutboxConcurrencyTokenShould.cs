// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.CosmosDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-Cosmos-emulator locks on the concurrency token a caller is handed: that a message read back from
/// the store carries the server's own <c>_etag</c>, and that the token tracks the document rather than
/// being a constant.
/// </summary>
/// <remarks>
/// <para>
/// <c>CloudOutboxMessage.ETag</c> is documented as "the ETag for optimistic concurrency". On Cosmos it was
/// always <see langword="null"/>: the stored document's property bound to the name <c>eTag</c> under the
/// client's camelCase policy, while Cosmos's system property is <c>_etag</c> — the two never met, so the
/// value was never read back. The store's own code worked around it by taking the token from the response
/// header instead of the document, which is what one does when the document's copy is useless.
/// </para>
/// <para>
/// This matters most on the recovery path. A change feed surfaces a document when it is written or updated
/// and not again, so a message whose publish failed without the failure being recorded is found only by
/// reading pending messages — and a caller doing that recovery sweep was handed messages with no usable
/// token to write conditionally against.
/// </para>
/// <para>
/// The arms bind the round-trip against the server's real value, deliberately, rather than asserting that
/// a mapping attribute is present. An attribute can be present and still bind the wrong name; only
/// comparing to what the server holds proves the token arrived.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbOutboxConcurrencyTokenShould
	: IClassFixture<CosmosDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbOutboxStoreContainerFixture _fixture;
	private readonly List<(CosmosDbOutboxStore Store, string ContainerName)> _created = [];

	public CosmosDbOutboxConcurrencyTokenShould(CosmosDbOutboxStoreContainerFixture fixture)
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
	/// LIVENESS: a message read back carries the server's actual concurrency token.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public async Task HandBackTheServersConcurrencyToken_WhenReadingPendingMessages()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, container) = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		var addResult = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);
		addResult.Success.ShouldBeTrue($"staging must succeed: {addResult.ErrorMessage}");

		var pending = await store.GetPendingAsync(partitionKey, 10, ct).ConfigureAwait(false);
		var own = pending.Documents.FirstOrDefault(m => string.Equals(m.MessageId, message.MessageId, StringComparison.Ordinal));

		own.ShouldNotBeNull("the staged message must be readable");

		var serverETag = await _fixture.ReadDocumentETagAsync(container, message.MessageId, partitionKey.Value)
			.ConfigureAwait(false);

		own.ETag.ShouldNotBeNullOrEmpty(
			"the message is documented as carrying the ETag for optimistic concurrency, and a caller "
			+ "performing a recovery sweep needs it to write conditionally. A null token is the field "
			+ "silently not being mapped.");

		own.ETag.ShouldBe(
			serverETag,
			"the token handed to a caller must be the one the server actually holds -- a token that does "
			+ "not match is worse than none, because a conditional write against it fails forever.");
	}

	/// <summary>
	/// SAFETY: the token tracks the document, so a stale one is distinguishable from a current one.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the one above is satisfied by any implementation that returns some constant, which
	/// would read as a working token and silently make every conditional write either always succeed or
	/// always fail.
	/// </remarks>
	[Fact]
	public async Task ChangeTheToken_WhenTheMessageIsModified()
	{
		var ct = TestContext.Current.CancellationToken;
		var (store, container) = await CreateStoreAsync().ConfigureAwait(false);
		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		_ = await store.AddAsync(message, partitionKey, ct).ConfigureAwait(false);

		var before = await _fixture.ReadDocumentETagAsync(container, message.MessageId, partitionKey.Value)
			.ConfigureAwait(false);

		var published = await store.MarkAsPublishedAsync(message.MessageId, partitionKey, ct).ConfigureAwait(false);
		published.Success.ShouldBeTrue($"marking published must succeed: {published.ErrorMessage}");

		var after = await _fixture.ReadDocumentETagAsync(container, message.MessageId, partitionKey.Value)
			.ConfigureAwait(false);

		after.ShouldNotBe(
			before,
			"the concurrency token must move when the document does, otherwise it cannot distinguish a "
			+ "stale read from a current one and is not a concurrency token at all.");
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
