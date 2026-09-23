// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Cdc.Postgres;
using Excalibur.Cdc.Postgres.Diagnostics;

using Npgsql.Replication;

namespace Excalibur.Data.Tests.Postgres.Cdc;

/// <summary>
/// Regression lock for <c>PostgresCdcProcessor.Dispose()</c> (bead <c>lkco82</c>). Npgsql's
/// <c>LogicalReplicationConnection</c> implements only <see cref="IAsyncDisposable"/> — it has no
/// synchronous release path — so a consumer that disposes this processor with a synchronous
/// <c>using</c> block cannot have the replication connection (and the replication slot it holds
/// open on the server) released. Removing <see cref="IDisposable"/> from the shared
/// <c>ICdcProcessor&lt;TEvent&gt;</c> contract would be a cross-package breaking change (it is
/// implemented by every CDC provider), so the fix applied here keeps the interface and makes the
/// leak LOUD instead of silent: a live replication connection at synchronous <c>Dispose()</c> time
/// is reported via an Error-level log naming the leaked slot.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Postgres")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class PostgresCdcProcessorDisposeShould
{
	private const string SlotName = "lkco82_test_slot";

	private static readonly FieldInfo ReplicationConnectionField = typeof(PostgresCdcProcessor)
		.GetField("_replicationConnection", BindingFlags.Instance | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("PostgresCdcProcessor._replicationConnection field not found.");

	private static PostgresCdcProcessor CreateProcessor(CapturingLogger logger) =>
		new(
			Options.Create(new PostgresCdcOptions
			{
				ConnectionString = "Host=localhost;Database=test",
				PublicationName = "excalibur_cdc_publication",
				ReplicationSlotName = SlotName,
			}),
			A.Fake<IPostgresCdcStateStore>(),
			logger);

	/// <summary>
	/// Liveness: when the processor holds a live replication connection, synchronous
	/// <see cref="IDisposable.Dispose"/> MUST report the leak loudly (Error level, naming the
	/// slot) instead of doing nothing — the pre-fix behavior of this method.
	/// </summary>
	[Fact]
	public void LogLoudErrorNamingTheSlot_WhenDisposedSynchronouslyWithAnOpenConnection()
	{
		var logger = new CapturingLogger();
		var processor = CreateProcessor(logger);
		ReplicationConnectionField.SetValue(processor, new LogicalReplicationConnection());

		processor.Dispose();

		logger.Entries.ShouldContain(e =>
			e.Level == LogLevel.Error &&
			e.EventId.Id == CdcPostgresEventId.CdcSyncDisposeLeaksReplicationConnection &&
			e.Message.Contains(SlotName, StringComparison.Ordinal));
	}

	/// <summary>
	/// Safety: a processor that never opened a replication connection (e.g. constructed but never
	/// started) has nothing to leak, so disposing it synchronously must NOT emit the leak warning —
	/// the log is reserved for the case it actually describes.
	/// </summary>
	[Fact]
	public void NotLogTheLeakWarning_WhenNoReplicationConnectionWasEverOpened()
	{
		var logger = new CapturingLogger();
		var processor = CreateProcessor(logger);

		processor.Dispose();

		logger.Entries.ShouldNotContain(e => e.EventId.Id == CdcPostgresEventId.CdcSyncDisposeLeaksReplicationConnection);
	}

	/// <summary>
	/// Dispose() remains idempotent under the fix: a second call after the processor is already
	/// disposed must not re-report the leak.
	/// </summary>
	[Fact]
	public void NotLogTheLeakWarningTwice_WhenDisposedRepeatedly()
	{
		var logger = new CapturingLogger();
		var processor = CreateProcessor(logger);
		ReplicationConnectionField.SetValue(processor, new LogicalReplicationConnection());

		processor.Dispose();
		processor.Dispose();

		logger.Entries.Count(e => e.EventId.Id == CdcPostgresEventId.CdcSyncDisposeLeaksReplicationConnection).ShouldBe(1);
	}

	private sealed record CapturedLogEntry(LogLevel Level, EventId EventId, string Message);

	/// <summary>
	/// Captures every log call verbatim (level, event id, formatted message) so assertions bind the
	/// real <see cref="ILogger"/> contract rather than a mocked "was this called" shape.
	/// </summary>
	private sealed class CapturingLogger : ILogger<PostgresCdcProcessor>
	{
		public List<CapturedLogEntry> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			Entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception)));
	}
}
