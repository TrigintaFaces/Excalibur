// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

using Excalibur.Data.Postgres.Diagnostics;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Provides metrics for Postgres outbox store operations.
/// </summary>
public sealed class PostgresOutboxStoreMetrics : IDisposable
{
	private readonly Meter _meter;
	private readonly Histogram<double> _saveMessagesTime;
	private readonly Histogram<double> _reserveMessagesTime;
	private readonly Histogram<double> _unreserveMessagesTime;
	private readonly Histogram<double> _deleteRecordTime;
	private readonly Histogram<double> _increaseAttemptsTime;
	private readonly Histogram<double> _moveToDeadLetterTime;
	private readonly Histogram<double> _batchDeleteTime;
	private readonly Histogram<double> _batchIncreaseAttemptsTime;
	private readonly Histogram<double> _batchMoveToDeadLetterTime;
	private readonly Counter<long> _messagesProcessed;
	private readonly Counter<long> _operationsCompleted;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresOutboxStoreMetrics" /> class.
	/// </summary>
	public PostgresOutboxStoreMetrics()
	{
		_meter = new Meter(PostgresOutboxTelemetryConstants.MeterName, PostgresOutboxTelemetryConstants.Version);

		// Operation duration histograms
		_saveMessagesTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.save_messages_duration",
			"milliseconds",
			"Time taken to save outbox messages");

		_reserveMessagesTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.reserve_messages_duration",
			"milliseconds",
			"Time taken to reserve outbox messages");

		_unreserveMessagesTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.unreserve_messages_duration",
			"milliseconds",
			"Time taken to unreserve outbox messages");

		_deleteRecordTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.delete_record_duration",
			"milliseconds",
			"Time taken to delete an outbox record");

		_increaseAttemptsTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.increase_attempts_duration",
			"milliseconds",
			"Time taken to increase message attempts");

		_moveToDeadLetterTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.move_to_dead_letter_duration",
			"milliseconds",
			"Time taken to move message to dead letter");

		_batchDeleteTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.batch_delete_duration",
			"milliseconds",
			"Time taken to delete multiple outbox records");

		_batchIncreaseAttemptsTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.batch_increase_attempts_duration",
			"milliseconds",
			"Time taken to increase attempts for multiple messages");

		_batchMoveToDeadLetterTime = _meter.CreateHistogram<double>(
			"excalibur.outbox.batch_move_to_dead_letter_duration",
			"milliseconds",
			"Time taken to move multiple messages to dead letter");

		// Counters for throughput tracking
		_messagesProcessed = _meter.CreateCounter<long>(
			"excalibur.outbox.messages_processed_total",
			"messages",
			"Total number of outbox messages processed");

		_operationsCompleted = _meter.CreateCounter<long>(
			"excalibur.outbox.operations_completed_total",
			"operations",
			"Total number of outbox operations completed");
	}

	/// <summary>
	/// Records the duration of a save messages operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages saved. </param>
	public void RecordSaveMessages(double durationMs, int messageCount)
	{
		_saveMessagesTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "save"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "save"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "save"));
	}

	/// <summary>
	/// Records the duration of a reserve messages operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages reserved. </param>
	public void RecordReserveMessages(double durationMs, int messageCount)
	{
		_reserveMessagesTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "reserve"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "reserve"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "reserve"));
	}

	/// <summary>
	/// Records the duration of an unreserve messages operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages unreserved. </param>
	public void RecordUnreserveMessages(double durationMs, int messageCount)
	{
		_unreserveMessagesTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "unreserve"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "unreserve"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "unreserve"));
	}

	/// <summary>
	/// Records the duration of a delete record operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	public void RecordDeleteRecord(double durationMs)
	{
		_deleteRecordTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "delete"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "delete"));
	}

	/// <summary>
	/// Records the duration of an increase attempts operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	public void RecordIncreaseAttempts(double durationMs)
	{
		_increaseAttemptsTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "increase_attempts"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "increase_attempts"));
	}

	/// <summary>
	/// Records the duration of a move to dead letter operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	public void RecordMoveToDeadLetter(double durationMs)
	{
		_moveToDeadLetterTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "move_to_dead_letter"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "move_to_dead_letter"));
	}

	/// <summary>
	/// Records the duration of a batch delete operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages deleted. </param>
	public void RecordBatchDelete(double durationMs, int messageCount)
	{
		_batchDeleteTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "batch_delete"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "batch_delete"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "batch_delete"));
	}

	/// <summary>
	/// Records the duration of a batch increase attempts operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages updated. </param>
	public void RecordBatchIncreaseAttempts(double durationMs, int messageCount)
	{
		_batchIncreaseAttemptsTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "batch_increase_attempts"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "batch_increase_attempts"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "batch_increase_attempts"));
	}

	/// <summary>
	/// Records the duration of a batch move to dead letter operation.
	/// </summary>
	/// <param name="durationMs"> Duration of the operation in milliseconds. </param>
	/// <param name="messageCount"> Number of messages moved. </param>
	public void RecordBatchMoveToDeadLetter(double durationMs, int messageCount)
	{
		_batchMoveToDeadLetterTime.Record(
			durationMs,
			new KeyValuePair<string, object?>("operation", "batch_move_to_dead_letter"));
		_messagesProcessed.Add(
			messageCount,
			new KeyValuePair<string, object?>("operation", "batch_move_to_dead_letter"));
		_operationsCompleted.Add(
			1,
			new KeyValuePair<string, object?>("operation", "batch_move_to_dead_letter"));
	}

	/// <summary>
	/// Disposes the metrics resources.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_meter?.Dispose();
		_disposed = true;
	}
}
