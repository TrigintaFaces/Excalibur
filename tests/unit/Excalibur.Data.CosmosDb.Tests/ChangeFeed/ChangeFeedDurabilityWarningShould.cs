// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Data.CosmosDb.Tests.ChangeFeed;

/// <summary>
/// Behavioral regression guard for bead <c>ydln24</c> gate-2 (sprint 855): the <b>silent-default</b>
/// "never inert" gap. The durable change-feed continuation is strictly opt-in, so when a consumer does
/// NOT register <c>AddCosmosDbChangeFeedCheckpointStore</c> the framework falls back to the process-local
/// <c>InMemoryChangeFeedCheckpointStore</c> — which loses continuation across restarts. Without a LOUD
/// signal that is the <i>advertised-but-inert</i> bug-class one level up, so the in-memory fallback MUST
/// "warn about itself" on construction (the SA-ruled Microsoft-first seam — ASP.NET Core Data Protection /
/// <c>IDistributedCache</c> precedent; SA 17228/17236, PM 17229, PdM SPEC sign-off 17230).
/// </summary>
/// <remarks>
/// <para>
/// <b>Seam (pinned, SA 17228/17236):</b> <c>InMemoryChangeFeedCheckpointStore</c> takes
/// <c>ILogger&lt;InMemoryChangeFeedCheckpointStore&gt;? = null</c> (→ <c>NullLogger</c>, fail-open) and
/// emits a once-on-construction <c>[LoggerMessage(Level = Warning)]</c> naming the consequence and the
/// remedy (<c>AddCosmosDbChangeFeedCheckpointStore</c>) + traceability. The shared <c>TryAdd</c> default
/// is wired across all 3 Cosmos packages so every non-durable path resolves the warning-emitting store.
/// </para>
/// <para>
/// <b>This lock binds the real DI path</b> (<c>AddExcaliburCosmosDb</c>, no durable override) with a
/// capturing logger and asserts the AC (PdM 17226): (1) severity is <c>Warning</c> (an Info/Debug does
/// not satisfy it), (2) it fires on the non-durable InMemory resolution, (3) the message names the remedy,
/// and — the non-vacuity guard — (negative) a durable store registered ⇒ InMemory never constructed ⇒
/// NO durability warning. RED on a dropped or downgraded warning, and RED on a blanket always-warn.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class ChangeFeedDurabilityWarningShould
{
	// A connection-string sentinel — never resolved (the CosmosClient factory is lazy and we only resolve
	// the checkpoint store), so no real Cosmos endpoint is contacted.
	private const string ConnectionStringSentinel =
		"AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdC1rZXk=;";

	[Fact]
	public void WarnOnceWhenTheNonDurableInMemoryDefaultIsResolved()
	{
		var capture = new CapturingLoggerProvider();

		var services = new ServiceCollection();
		_ = services.AddLogging(b =>
		{
			_ = b.SetMinimumLevel(LogLevel.Trace);
			_ = b.AddProvider(capture);
		});
		_ = services.AddExcaliburCosmosDb(cosmos =>
			cosmos.ConnectionString(ConnectionStringSentinel).DatabaseName("db").ContainerName("c"));

		using var provider = services.BuildServiceProvider();

		// Resolving the checkpoint store constructs the InMemory default (no durable override registered).
		var store = provider.GetRequiredService<IChangeFeedCheckpointStore>();

		store.GetType().Name.ShouldBe("InMemoryChangeFeedCheckpointStore",
			"With no AddCosmosDbChangeFeedCheckpointStore override, the change-feed checkpoint store must fall back to the in-memory default.");

		var durabilityWarnings = capture.Entries
			.Where(e => e.Level >= LogLevel.Warning
					 && e.Message.Contains("AddCosmosDbChangeFeedCheckpointStore", StringComparison.Ordinal))
			.ToList();

		durabilityWarnings.ShouldNotBeEmpty(
			"ydln24 gate-2 — resolving the non-durable InMemory change-feed checkpoint store MUST emit a Warning "
			+ "naming the remedy (AddCosmosDbChangeFeedCheckpointStore); otherwise durable continuation is silently inert.");

		// Severity bar (PdM AC clause 1): an Info/Debug downgrade does NOT satisfy the gate.
		durabilityWarnings.ShouldAllBe(e => e.Level >= LogLevel.Warning,
			"ydln24 gate-2 — the durability signal must be Warning-level or higher (a downgrade to Info/Debug fails the gate).");
	}

	[Fact]
	public void NotWarnWhenADurableCheckpointStoreIsRegistered()
	{
		var capture = new CapturingLoggerProvider();

		var services = new ServiceCollection();
		_ = services.AddLogging(b =>
		{
			_ = b.SetMinimumLevel(LogLevel.Trace);
			_ = b.AddProvider(capture);
		});
		_ = services.AddExcaliburCosmosDb(cosmos =>
			cosmos.ConnectionString(ConnectionStringSentinel).DatabaseName("db").ContainerName("c"));

		// Opt into durability — this Replace()s the InMemory default with the Cosmos-backed store, so the
		// InMemory store is never constructed and must NOT warn (the negative half — guards against an
		// always-warn regression that would make the positive assertion vacuous).
		_ = services.AddCosmosDbChangeFeedCheckpointStore(_ => A.Fake<Container>());

		using var provider = services.BuildServiceProvider();

		var store = provider.GetRequiredService<IChangeFeedCheckpointStore>();

		store.GetType().Name.ShouldBe("CosmosDbChangeFeedCheckpointStore",
			"AddCosmosDbChangeFeedCheckpointStore must replace the in-memory default with the durable Cosmos-backed store.");

		capture.Entries
			.Where(e => e.Level >= LogLevel.Warning
					 && e.Message.Contains("AddCosmosDbChangeFeedCheckpointStore", StringComparison.Ordinal))
			.ShouldBeEmpty(
				"ydln24 gate-2 — when a durable checkpoint store is registered, the non-durable in-memory warning must NOT fire (no false alarm / no always-warn).");
	}

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		private readonly ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> _entries = new();

		public IReadOnlyList<(LogLevel Level, EventId EventId, string Message)> Entries => _entries.ToList();

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

		public void Dispose()
		{
		}

		// IsEnabled MUST return true: a [LoggerMessage] source-gen method skips Log() when IsEnabled is false,
		// which would make this lock vacuously pass. (See project MEMORY: [LoggerMessage] + IsEnabled gotcha.)
		private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> sink)
			: ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter)
			{
				ArgumentNullException.ThrowIfNull(formatter);
				sink.Enqueue((logLevel, eventId, formatter(state, exception)));
			}
		}
	}
}
