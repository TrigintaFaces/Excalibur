// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;

using global::Marten;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Shouldly;

#pragma warning disable CA2100 // Identifiers come from this test's own options, never from input.

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure locks on the property that the Marten outbox records a failure as ONE atomic write.
/// </summary>
/// <remarks>
/// <para>
/// The failure mark touches two places: the claim table, where the claim is released and the retry floor is
/// stamped, and the document, where the status and the attempt count live. Performed as two independent
/// writes, a crash or a pause between them leaves the claim released under a floor while the document still
/// reads Staged with its attempt count unchanged. Once the floor elapses the message is claimed again, fails
/// again, and records the same count again. Because the dead-letter ceiling is driven by that count, the
/// message is retried without end and never dead-letters: a termination failure, and the mirror image of the
/// split the single-write rule exists to prevent — there the floor is lost, here the count is.
/// </para>
/// <para>
/// The safety arm induces the gap deterministically rather than waiting for a real crash: it holds a lock on
/// the document row so the document write cannot proceed, cancels the operation while it is blocked, and then
/// asserts that the claim release did not survive on its own. Under two independent writes the claim release
/// has already been committed by the time the document write is even attempted, so the message is left in
/// exactly the forever-retry state described above.
/// </para>
/// <para>
/// Every provider other than this one performs the mark as a single statement, so this is a parity defect as
/// much as a correctness one.
/// </para>
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxFailureMarkAtomicityShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private const string SchemaName = "marten_outbox_atomicity";

	/// <summary>Marten's default table name for the outbox document type.</summary>
	private const string DocumentTable = "mt_doc_martenoutboxdocument";

	private static IDocumentStore? SharedDocumentStore;

	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private readonly MartenOutboxStoreOptions _options = new() { FailureBackoffFloorSeconds = 2 };

	public MartenOutboxFailureMarkAtomicityShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY. When the document write cannot complete, the claim release must not survive on its own.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// This is the arm that binds the defect. A claim released without the matching attempt-count advance is
	/// the forever-retry state: the message returns to the pool once its floor elapses, fails again, and
	/// records the same count, so the retry ceiling is never reached.
	/// </remarks>
	[Fact]
	public async Task NotReleaseTheClaim_WhenTheDocumentWriteCannotComplete()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false);

		var claimedBy = await ReadClaimAsync(message.Id).ConfigureAwait(false);
		claimedBy.DispatcherId.ShouldNotBeNull("the staged message must be claimed before the failure is reported.");

		// Hold the document row so the document half of the mark cannot proceed. The claim half runs first,
		// so this opens exactly the window a crash would.
		await using var blocker = new NpgsqlConnection(_fixture.ConnectionString);
		await blocker.OpenAsync(ct).ConfigureAwait(false);
		await using var blockingTransaction = await blocker.BeginTransactionAsync(ct).ConfigureAwait(false);

		await using (var lockCommand = new NpgsqlCommand(
			$"SELECT id FROM \"{SchemaName}\".\"{DocumentTable}\" WHERE id = @Id FOR UPDATE;",
			blocker,
			blockingTransaction))
		{
			_ = lockCommand.Parameters.AddWithValue("Id", message.Id);
			_ = await lockCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
		}

		// Abandon the mark while it is blocked on that row.
		using var abandon = new CancellationTokenSource(TimeSpan.FromSeconds(3));
		_ = await Should.ThrowAsync<Exception>(
			async () => await store.MarkFailedAsync(message.Id, "boom", 5, abandon.Token).ConfigureAwait(false))
			.ConfigureAwait(false);

		await blockingTransaction.RollbackAsync(ct).ConfigureAwait(false);

		// SAFETY -- neither half landed.
		var afterAbandon = await ReadClaimAsync(message.Id).ConfigureAwait(false);
		afterAbandon.DispatcherId.ShouldBe(
			claimedBy.DispatcherId,
			"the claim release must not survive a failed document write. As two independent writes the " +
			"release is already committed by this point, so the message returns to the pool with its attempt " +
			"count unchanged, fails again, records the same count again, and never reaches the retry ceiling.");
		afterAbandon.NextAttemptAt.ShouldBeNull(
			"the retry floor belongs to the same write as the attempt-count advance. A floor stamped without " +
			"it is what schedules the endless retry.");
	}

	/// <summary>
	/// LIVENESS. An uncontended failure mark still commits BOTH halves.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// Without this arm the safety assertion above is satisfied by a mark that writes nothing at all, which
	/// would strand every failed message under its original claim until the claim timeout aged it out and
	/// would never advance the attempt count either.
	/// </remarks>
	[Fact]
	public async Task CommitTheClaimReleaseAndTheDocumentTogether_WhenTheMarkIsUncontended()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var ct = TestContext.Current.CancellationToken;

		var message = NewMessage();
		await store.StageMessageAsync(message, ct).ConfigureAwait(false);
		_ = await store.GetUnsentMessagesAsync(10, ct).ConfigureAwait(false);

		await store.MarkFailedAsync(message.Id, "boom", 5, ct).ConfigureAwait(false);

		// The claim half: released, and carrying the floor.
		var claim = await ReadClaimAsync(message.Id).ConfigureAwait(false);
		claim.NextAttemptAt.ShouldNotBeNull(
			"an uncontended mark must stamp the retry floor, or the message is re-claimed on the next poll.");

		// The document half: the failure and the advanced attempt count are both visible.
		var failed = (await store.GetAllTenantsFailedMessagesAsync(0, null, 10, ct).ConfigureAwait(false)).ToList();
		var recorded = failed.SingleOrDefault(m => m.Id == message.Id);
		recorded.ShouldNotBeNull(
			"the document half of the mark must be committed too. The attempt count lives there, and the " +
			"dead-letter ceiling is driven by it, so a message whose document never advances is retried " +
			"forever.");
		recorded.RetryCount.ShouldBe(
			5,
			"the reported attempt count must reach the document, or the retry ceiling is never approached.");
	}

	private static OutboundMessage NewMessage() =>
		new("Test.MessageType", "test-payload"u8.ToArray(), "test-queue") { Id = Guid.NewGuid().ToString() };

	private async Task<MartenOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - this real-infra atomicity lock is never skipped.");

		// Shared singleton: DocumentStore.For builds an NpgsqlDataSource that Npgsql pools by connection
		// string, so disposing it would break every later arm in this collection. Its own schema keeps these
		// locks clear of the other Marten suites' documents.
		var documentStore = SharedDocumentStore ??= DocumentStore.For(opts =>
		{
			opts.Connection(_fixture.ConnectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = SchemaName;
		});

		return new MartenOutboxStore(
			documentStore, Options.Create(_options), NullLogger<MartenOutboxStore>.Instance);
	}

	/// <summary>
	/// Reads the claim row straight from the claim table. Neither field is observable through
	/// <c>IOutboxStore</c>, and the coupling between them is the whole property under test.
	/// </summary>
	private async Task<(string? DispatcherId, DateTimeOffset? NextAttemptAt)> ReadClaimAsync(string messageId)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

		await using var command = new NpgsqlCommand(
			$"SELECT dispatcher_id, next_attempt_at FROM \"{_options.ClaimsSchemaName}\".\"{_options.ClaimsTableName}\" " +
			"WHERE message_id = @MessageId;",
			connection);
		_ = command.Parameters.AddWithValue("MessageId", messageId);

		await using var reader = await command.ExecuteReaderAsync(CancellationToken.None).ConfigureAwait(false);
		if (!await reader.ReadAsync(CancellationToken.None).ConfigureAwait(false))
		{
			return (null, null);
		}

		var dispatcherId = await reader.IsDBNullAsync(0, CancellationToken.None).ConfigureAwait(false)
			? null
			: reader.GetString(0);
		var nextAttemptAt = await reader.IsDBNullAsync(1, CancellationToken.None).ConfigureAwait(false)
			? (DateTimeOffset?)null
			: reader.GetFieldValue<DateTimeOffset>(1);

		return (dispatcherId, nextAttemptAt);
	}
}
