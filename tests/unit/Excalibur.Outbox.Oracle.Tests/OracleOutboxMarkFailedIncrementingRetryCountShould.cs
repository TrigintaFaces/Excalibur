// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Instantiated by xUnit.

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// dit5es part 2 — re-verification against real Oracle (NOT re-diagnosis from the bead's title, per its own
/// instruction: "treat the title as a symptom report, not a diagnosis"). The exact test the bead names,
/// <c>MarkFailed_IncrementingRetryCount_TracksRetries</c>, no longer exists under that name anywhere in the
/// repo (the conformance base it cited was itself renamed from <c>OutboxStoreConformanceTestBase</c> to
/// <see cref="Excalibur.Testing.Conformance.OutboxStoreConformanceTestKit"/>), and neither of that kit's
/// current close relatives (<c>MarkFailedAsync_ShouldSetRetryCount</c>, a single call; and
/// <c>MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount</c>, two calls in DESCENDING order) exercises
/// the bead's specific scenario: three sequential <c>MarkFailedAsync</c> calls with an ASCENDING retry count
/// on the same message. This test reconstructs that exact scenario against real Oracle rather than inferring
/// the outcome from an adjacent test.
/// </summary>
/// <remarks>
/// Non-skipped real-infra lock, per <c>verify-against-real-infra-not-mock</c>: Oracle has no CI container, so
/// this and every other Oracle arm fails fast rather than skips when Docker is unavailable — a skip here is
/// not evidence, per project history (bd-2mtb74).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
[Collection(OracleOutboxCollection.Name)]
public sealed class OracleOutboxMarkFailedIncrementingRetryCountShould
{
	private readonly OracleOutboxStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="OracleOutboxMarkFailedIncrementingRetryCountShould"/> class.</summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleOutboxMarkFailedIncrementingRetryCountShould(OracleOutboxStoreContainerFixture fixture) => _fixture = fixture;

	private async Task<OracleOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available — dit5es part 2 is a real-infra re-verification and is never "
			+ "skipped; a skipped Oracle arm is not evidence (per project history, that is exactly how an earlier "
			+ "Oracle outbox no-op shipped).");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 10,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}

	/// <summary>
	/// SAFETY — the bead's exact scenario: stage, then three sequential <c>MarkFailedAsync</c> calls with
	/// retryCount 1, 2, 3, then <c>GetAllTenantsFailedMessagesAsync(10, null, 10)</c> must return the message
	/// with <c>RetryCount == 3</c> and <c>LastError == "Error 3"</c> (the bead's :598-:600 assertions).
	/// </summary>
	[Fact]
	public async Task TrackRetriesAcrossThreeSequentialMarkFailedCalls()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var msg = new OutboundMessage("Test.Retry", "payload"u8.ToArray(), "test-queue");
			await store.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);

			await store.MarkFailedAsync(msg.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "Error 2", 2, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "Error 3", 3, CancellationToken.None).ConfigureAwait(false);

			var admin = (IOutboxStoreAdmin)store;
			var failed = await admin.GetAllTenantsFailedMessagesAsync(10, null, 10, CancellationToken.None).ConfigureAwait(false);
			var reloaded = failed.FirstOrDefault(m => m.Id == msg.Id);

			_ = reloaded.ShouldNotBeNull(
				"three ascending MarkFailedAsync calls on the same message must leave it visible to "
				+ "GetAllTenantsFailedMessagesAsync(maxRetries: 10, ...) — 3 falls within the [1, 10] band the "
				+ "attempts filter admits.");
			reloaded.RetryCount.ShouldBe(3, "the third call's retryCount must be the one recorded — the bead's own assertion.");
			reloaded.LastError.ShouldBe("Error 3", "the most recent error message must be the one persisted.");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS — a message that reaches <c>RetryCount == 3</c> is excluded once <c>maxRetries</c> is set
	/// below it (boundary), and included exactly at the boundary. Pins the BETWEEN bound the bead's item 6
	/// asked for, using the same three-call message this test class already builds.
	/// </summary>
	[Fact]
	public async Task RespectTheMaxRetriesBoundaryAfterThreeIncrements()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		try
		{
			var msg = new OutboundMessage("Test.Retry", "payload"u8.ToArray(), "test-queue");
			await store.StageMessageAsync(msg, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "Error 2", 2, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(msg.Id, "Error 3", 3, CancellationToken.None).ConfigureAwait(false);

			var admin = (IOutboxStoreAdmin)store;

			var belowCeiling = await admin.GetAllTenantsFailedMessagesAsync(2, null, 10, CancellationToken.None).ConfigureAwait(false);
			belowCeiling.ShouldNotContain(m => m.Id == msg.Id, "attempts (3) exceeding maxRetries (2) must be excluded.");

			var atCeiling = await admin.GetAllTenantsFailedMessagesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false);
			atCeiling.ShouldContain(m => m.Id == msg.Id, "attempts (3) exactly at maxRetries (3) must be included — inclusive upper bound.");
		}
		finally
		{
			await _fixture.CleanupTableAsync().ConfigureAwait(false);
		}
	}
}
