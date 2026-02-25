// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Postgres.Outbox;

namespace Excalibur.Tests.Data.Postgres;

/// <summary>
///     Unit tests for PostgresOutboxStore parameter validation and basic behavior.
///     Note: Database interaction tests are covered in integration tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PostgresOutboxStoreShould : IDisposable
{
	private readonly IDb _db;
	private readonly IDbConnection _connection;
	private readonly IOptions<PostgresOutboxStoreOptions> _options;
	private readonly ILogger<PostgresOutboxStore> _logger;
	private readonly PostgresOutboxStoreMetrics _metrics;
	private readonly PostgresOutboxStore _store;

	public PostgresOutboxStoreShould()
	{
		_db = A.Fake<IDb>();
		_connection = A.Fake<IDbConnection>();
		_logger = A.Fake<ILogger<PostgresOutboxStore>>();
		_metrics = new PostgresOutboxStoreMetrics();

		_options = Microsoft.Extensions.Options.Options.Create(new PostgresOutboxStoreOptions
		{
			OutboxTableName = "test_outbox",
			DeadLetterTableName = "test_dead_letters",
			ReservationTimeout = 300,
		});

		_ = A.CallTo(() => _db.Connection).Returns(_connection);

		_store = new PostgresOutboxStore(_db, _options, _logger, _metrics);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullDb() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PostgresOutboxStore(null!, _options, _logger, _metrics));

	[Fact]
	public void ThrowArgumentNullExceptionForNullOptions() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PostgresOutboxStore(_db, null!, _logger, _metrics));

	[Fact]
	public void ThrowArgumentNullExceptionForNullLogger() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PostgresOutboxStore(_db, _options, null!, _metrics));

	[Fact]
	public void CreateWithNullMetrics()
	{
		// Act
		using var store = new PostgresOutboxStore(_db, _options, _logger, null);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullMessages() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await _store.SaveMessagesAsync(null!, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public async Task SaveMessagesAsyncReturnZeroForEmptyCollection()
	{
		// Arrange
		var messages = new List<IOutboxMessage>();

		// Act
		var result = await _store.SaveMessagesAsync(messages, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void ThrowArgumentNullOrWhiteSpaceForNullDispatcherId() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentException>(async () =>
			await _store.UnReserveOutboxMessagesAsync(null!, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentNullOrWhiteSpaceForEmptyDispatcherId() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentException>(async () =>
			await _store.UnReserveOutboxMessagesAsync(string.Empty, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentNullOrWhiteSpaceForWhitespaceDispatcherId() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentException>(async () =>
			await _store.UnReserveOutboxMessagesAsync(" ", CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentNullOrWhiteSpaceForNullMessageId() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentException>(async () =>
			await _store.DeleteOutboxRecord(null!, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentNullOrWhiteSpaceForEmptyMessageId() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentException>(async () =>
			await _store.DeleteOutboxRecord(string.Empty, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentOutOfRangeForNegativeBatchSize() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await _store.ReserveOutboxMessagesAsync("dispatcher1", -1, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public void ThrowArgumentOutOfRangeForZeroBatchSize() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await _store.ReserveOutboxMessagesAsync("dispatcher1", 0, CancellationToken.None).ConfigureAwait(true));

	[Fact]
	public async Task DeleteOutboxRecordsBatchAsyncReturnZeroForEmptyCollection()
	{
		// Arrange
		var messageIds = new List<string>();

		// Act
		var result = await _store.DeleteOutboxRecordsBatchAsync(messageIds, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task IncreaseAttemptsBatchAsyncReturnZeroForEmptyCollection()
	{
		// Arrange
		var messageIds = new List<string>();

		// Act
		var result = await _store.IncreaseAttemptsBatchAsync(messageIds, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task MoveToDeadLetterBatchAsyncReturnZeroForEmptyCollection()
	{
		// Arrange
		var messageIds = new List<string>();

		// Act
		var result = await _store.MoveToDeadLetterBatchAsync(messageIds, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullMessageIdsBatch() =>
		// Act & Assert
		_ = Should.ThrowAsync<ArgumentNullException>(async () =>
			await _store.DeleteOutboxRecordsBatchAsync(null!, CancellationToken.None).ConfigureAwait(true));

	/// <inheritdoc/>
	public void Dispose()
	{
		_store?.Dispose();
		_metrics?.Dispose();
		_connection?.Dispose();
	}
}
