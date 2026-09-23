// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text;

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

/// <summary>
/// ONE processor instance, polled more than once, must keep delivering and keep checkpointing.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this binds and why a unit test cannot.</b> Each <c>ProcessBatchAsync</c> invocation builds its own
/// bounded queue and clears the producer-stopped flag. Before that fix the queue was created once in the
/// constructor, so the first poll drained it, completed the channel, and every later poll on the same instance
/// returned nothing - a processor that silently stopped delivering after its first batch. The producer contacts
/// the database before anything reaches the channel, so there is no seam at which a second poll is observable
/// without a live server: a shape test asserting the field is reassigned would pass an implementation that
/// reassigns and still delivers nothing.
/// </para>
/// <para>
/// <b>The queue and the flag are separate halves and each needs its own mutant.</b> <c>_producerStopped</c> is
/// volatile and is read by <c>ShouldWaitForProducer</c>, so an arm proving only that the channel was replaced
/// would still pass an implementation that leaves the flag set and delivers nothing on poll two. Both halves
/// are severed independently when these arms are proven.
/// </para>
/// <para>
/// NEVER SKIPPED. Docker is a hard requirement here, not a convenience: a skip-gated infra arm that never runs
/// is the gap that ships the bug.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServerCdc)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait("Database", "SqlServer")]
[Trait("SubComponent", "SecondPollDelivery")]
public sealed class SqlServerCdcSecondPollIntegrationShould : IntegrationTestBase
{
	private static readonly TimeSpan CaptureWindow = TimeSpan.FromSeconds(45);

	private readonly SqlServerCdcContainerFixture _fixture;

	public SqlServerCdcSecondPollIntegrationShould(SqlServerCdcContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task DeliverAndCheckpointOnASecondPollOfTheSameProcessorInstance()
	{
		var (connectionString, provider, processor) = await ArrangeAsync("SecondPoll").ConfigureAwait(false);
		await using var scope = provider;

		// ---- poll ONE ----
		await InsertAndAwaitCaptureAsync(connectionString, 1, "first", 1).ConfigureAwait(false);

		var firstPoll = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
		firstPoll.ShouldNotBeEmpty(
			"the FIRST poll delivered nothing, so the arm cannot say anything about the second - this is a "
			+ "setup failure, not the defect under test");

		var firstCheckpoint = await ReadCheckpointAsync(connectionString).ConfigureAwait(false);
		firstCheckpoint.ShouldNotBeNull(
			"the first poll delivered changes but recorded no checkpoint, so a restart would redeliver them");

		// ---- poll TWO: the lock ----
		await InsertAndAwaitCaptureAsync(connectionString, 2, "second", 2).ConfigureAwait(false);

		var secondPoll = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
		secondPoll.ShouldNotBeEmpty(
			"THE DEFECT: the same processor instance delivered on its first poll and nothing on its second. "
			+ "A queue built once in the constructor is drained and completed by the first batch, so every "
			+ "later poll reads a finished channel and returns empty - a processor that stops delivering "
			+ "after one batch while reporting success.");

		var secondCheckpoint = await ReadCheckpointAsync(connectionString).ConfigureAwait(false);
		secondCheckpoint.ShouldNotBeNull("the second poll delivered changes but recorded no checkpoint");

		// Delivery without progress is its own defect: the instance would redeliver poll two's changes
		// forever. Asserting the LSN strictly advanced is what separates "it ran again" from "it made
		// progress again", and only the second is the guarantee.
		Compare(secondCheckpoint, firstCheckpoint).ShouldBeGreaterThan(
			0,
			"the second poll delivered changes but left the checkpoint where the first poll put it, so the "
			+ "processor is not recording progress across polls and a restart would redeliver.");
	}

	[Fact]
	public async Task DeliverOnAPollThatFollowsAnEmptyOne()
	{
		var (connectionString, provider, processor) = await ArrangeAsync("EmptyFirst").ConfigureAwait(false);
		await using var scope = provider;

		// Poll with nothing to read. This is the path the second-poll defect hides behind most easily:
		// a first poll that returns empty still runs the producer to completion and still completes the
		// writer, so an instance that survives a NON-empty first poll can still be wedged by an empty one.
		var emptyPoll = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
		emptyPoll.ShouldBeEmpty("nothing had been written yet, so anything delivered here is spurious");

		await InsertAndAwaitCaptureAsync(connectionString, 1, "afterempty", 1).ConfigureAwait(false);

		var secondPoll = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
		secondPoll.ShouldNotBeEmpty(
			"the instance was wedged by a poll that had nothing to deliver. An empty batch is the ordinary "
			+ "steady state of a CDC processor - if it terminates the instance, the processor stops working "
			+ "the moment its source goes quiet, which is exactly when nobody is watching.");
	}

	[Fact]
	public async Task DeliverOnAPollThatFollowsACancelledOne()
	{
		var (connectionString, provider, processor) = await ArrangeAsync("CancelRestart").ConfigureAwait(false);
		await using var scope = provider;

		await InsertAndAwaitCaptureAsync(connectionString, 1, "beforecancel", 1).ConfigureAwait(false);

		// Cancel from INSIDE the handler rather than on a timer. A timer makes the arm a race: it can cancel
		// before the producer starts, after the batch finishes, or anywhere between, so a green says nothing
		// about which path ran. Cancelling on the first delivered change pins the cancellation to a point the
		// processor has provably reached.
		using var cancelledPoll = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
		var delivered = new ConcurrentBag<DataChangeEvent>();

		try
		{
			_ = await processor.ProcessBatchAsync(
				(change, _) =>
				{
					delivered.Add(change);
					cancelledPoll.Cancel();
					return Task.CompletedTask;
				},
				cancelledPoll.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!TestCancellationToken.IsCancellationRequested)
		{
			// The intended shape. A cancelled poll is a normal shutdown, not a fault.
		}
		catch (SqlException) when (cancelledPoll.IsCancellationRequested
			&& !TestCancellationToken.IsCancellationRequested)
		{
			// MEASURED, and it is a defect rather than a quirk of this arm - tracked as
			// Excalibur_Dispatch-zshjlh. Cancelling while a change-fetch is in flight does not produce an
			// OperationCanceledException at all: Microsoft.Data.SqlClient raises "A severe error occurred
			// on the current command ... Operation cancelled by user", which travels back through the
			// Polly retry engine and the circuit breaker before reaching CdcProcessor's cancellation
			// handler - so the handler written to make cancellation quiet cannot fire, an ordinary
			// shutdown is retried as a database fault, and it counts toward opening the breaker.
			//
			// This arm accepts both shapes DELIBERATELY, and the reason is worth stating so nobody
			// "tidies" it: the property under test here is that a cancelled poll does not WEDGE the
			// instance, which is independent of which exception type surfaces. Asserting the type here
			// too would fuse two findings into one red and leave the restart property untested until
			// somebody else's fix landed. The type is asserted on its own bead, against this same arm.
#pragma warning restore CS0162
		}

		delivered.ShouldNotBeEmpty(
			"the handler never ran, so the cancellation did not happen where this arm assumes it did and the "
			+ "assertion below would be testing an untouched processor");

		await InsertAndAwaitCaptureAsync(connectionString, 2, "aftercancel", 2).ConfigureAwait(false);

		var afterCancel = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
		afterCancel.ShouldNotBeEmpty(
			"a cancelled poll left the instance unable to deliver again. Cancellation is how a host stops a "
			+ "batch - if it permanently wedges the processor, every graceful shutdown-and-resume silently "
			+ "stops the pipeline while the process stays healthy.");
	}

	[Fact]
	public async Task DeliverEveryCapturedChangeEvenWhenAHandlerFaultsTheFirstPoll()
	{
		var (connectionString, provider, processor) = await ArrangeAsync("NoSkip").ConfigureAwait(false);
		await using var scope = provider;

		// The COUNT is the whole experiment, and it must exceed the consumer's batch size.
		//
		// The hazard needs the PRODUCER STILL RUNNING when the consumer faults: the producer advances an
		// in-memory position as it ENQUEUES, the durable checkpoint advances only as the consumer
		// DELIVERS, and the gap between them is what a later poll can resume past. If the producer drains
		// cleanly first it REMOVES the table from its tracking, the in-memory position is discarded, and
		// the hazard cannot appear however the consumer then fails.
		//
		// Measured: at two rows this arm PASSES, because two rows are enqueued and the producer finishes
		// before the handler is ever called. Two rows is not a weaker version of this test - it is a
		// different test, one that cannot observe the property. Every other arm in this class uses one.
		const int ChangeCount = 300;
		for (var id = 1; id <= ChangeCount; id++)
		{
			await ExecuteAsync(
				connectionString,
				"INSERT INTO dbo.Orders VALUES (@Id, @Reference);",
				("@Id", id),
				("@Reference", $"row{id}")).ConfigureAwait(false);
		}

		(await SqlServerCdcContainerFixture.WaitForCapturedRowsAsync(
			connectionString, "dbo_Orders", ChangeCount, CaptureWindow, TestCancellationToken)
			.ConfigureAwait(false))
			.ShouldBeTrue($"the capture job did not surface all {ChangeCount} inserts, so nothing below tests the processor");

		var seen = new ConcurrentBag<string>();
		var deliveredBeforeTheFault = new ConcurrentBag<string>();

		// Poll ONE: the handler faults on the first change it is given. A handler throwing is ordinary -
		// a poison payload, a downstream outage - and is the documented reason the applier tracks failed
		// tables at all. It is NOT an exotic fault.
		try
		{
			_ = await processor.ProcessBatchAsync(
				(change, _) =>
				{
					seen.Add(Convert.ToHexString(change.Lsn));
					deliveredBeforeTheFault.Add(Convert.ToHexString(change.Lsn));
					throw new InvalidOperationException("deliberate handler fault on the first delivered change");
				},
				TestCancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
		{
			// The batch may fault, may surface a provider exception, or may return a count normally -
			// all three are reachable. Which one occurs is not the property under test.
		}

		// THE EXPERIMENT MUST BE SHOWN TO HAVE HAPPENED, or the assertion below is vacuous. If poll one
		// delivered nothing, the handler never threw, no batch ended abnormally, and poll two alone
		// supplies every change - so the final assertion passes while testing NOTHING. This arm cannot
		// tell those two worlds apart without saying so here.
		deliveredBeforeTheFault.ShouldNotBeEmpty(
			"poll one never invoked the handler, so no fault occurred and the assertion below would pass "
			+ "over an experiment that did not run");

		// Poll TWO on the SAME instance, with a handler that cannot fail.
		foreach (var change in await PollAsync(processor, TestCancellationToken).ConfigureAwait(false))
		{
			seen.Add(Convert.ToHexString(change.Lsn));
		}

		// THE SAFETY PROPERTY, and the one no other arm in this class asserts: every captured change is
		// handed to the handler at least once. Not "something was delivered" - THESE changes were.
		//
		// If this fails, the processor did not merely fail to redeliver the change whose handler threw;
		// it advanced past a change the handler was NEVER GIVEN. That is silent loss, it survives a
		// restart once the next poll checkpoints, and no green suite can see it, because every other
		// assertion here is satisfied by a processor that delivers one change and drops the rest.
		seen.Distinct(StringComparer.Ordinal).Count().ShouldBeGreaterThanOrEqualTo(
			ChangeCount,
			"a captured change was never delivered to the handler at all. The in-memory resume position "
			+ "is advanced when the producer ENQUEUES a change, while the durable checkpoint advances only "
			+ "when the consumer DELIVERS one - so a batch that ends abnormally leaves the instance "
			+ "resuming from a position ahead of what it actually delivered, and the changes in between "
			+ "are skipped rather than redelivered.");
	}

	[Fact]
	public async Task DeliverEveryCapturedChangeWhenABlockedProducerMeetsAFaultingHandler()
	{
		// The construction is not mine and the prediction attached to it is not mine either; both came
		// from an adversarial review of the arms above, which established that COUNT IS THE WRONG AXIS.
		// Loss requires TWO conditions at once, and no number of rows supplies either:
		//   (i)  the consumer stops draining while the channel is non-empty  -> a handler that TAKES TIME
		//   (ii) the producer is prevented from FINISHING                    -> it must BLOCK, because a
		//        producer that finishes REMOVES its table from tracking and restores the invariant
		// A prompt handler denies both at any scale: the dequeue helper drains only what is immediately
		// available, so the consumer takes ONE change, that batch completes, and its checkpoint is written
		// before the producer has advanced past the next LSN.
		//
		// (ii) is supplied structurally, not by timing: the queue is bounded with FullMode = Wait, so a
		// bound below the pending work makes blocking INEVITABLE rather than likely.
		const int ChangeCount = 50;
		const int QueueSize = 4;
		var hold = TimeSpan.FromSeconds(3);

		var (connectionString, provider, processor) = await ArrangeAsync(
			"BlockedProducer",
			(services, database, _) => services.AddSingleton<IDatabaseOptions>(new DatabaseOptions
			{
				DatabaseName = database,
				DatabaseConnectionIdentifier = $"cdc-{database}",
				StateConnectionIdentifier = $"state-{database}",

				// REQUIRED, and its absence is why the first run of this arm delivered nothing. The
				// provider's own factory populates Tables from TrackTable(...); pre-registering
				// IDatabaseOptions replaces that factory wholesale, so the tracked table has to be
				// restated here or the producer tracks NOTHING and the handler is never called.
				Tables = [new CdcTableConfig { TableName = "dbo.Orders" }],
				QueueSize = QueueSize,
				ConsumerBatchSize = 1,
			})).ConfigureAwait(false);
		await using var scope = provider;

		for (var id = 1; id <= ChangeCount; id++)
		{
			await ExecuteAsync(
				connectionString,
				"INSERT INTO dbo.Orders VALUES (@Id, @Reference);",
				("@Id", id),
				("@Reference", $"row{id}")).ConfigureAwait(false);
		}

		(await SqlServerCdcContainerFixture.WaitForCapturedRowsAsync(
			connectionString, "dbo_Orders", ChangeCount, CaptureWindow, TestCancellationToken)
			.ConfigureAwait(false))
			.ShouldBeTrue($"the capture job did not surface all {ChangeCount} inserts");

		var seen = new ConcurrentBag<string>();
		var handlerRan = new ConcurrentBag<string>();

		// Poll ONE. The handler occupies the consumer long enough for the producer to fill the bounded
		// queue and block in WriteAsync, then faults. The delay is NOT linked to the batch token: if it
		// were, a cancellation would turn this into a different experiment.
		try
		{
			_ = await processor.ProcessBatchAsync(
				async (change, _) =>
				{
					var lsn = Convert.ToHexString(change.Lsn);
					seen.Add(lsn);
					handlerRan.Add(lsn);
					await Task.Delay(hold).ConfigureAwait(false);
					throw new InvalidOperationException("deliberate fault after holding the consumer");
				},
				TestCancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
		{
		}

		handlerRan.ShouldNotBeEmpty(
			"poll one never invoked the handler, so neither the hold nor the fault happened and the "
			+ "assertion below would pass over an experiment that did not run");

		// Drain. Poll until the feed goes quiet, bounded - the property is about the UNION across every
		// poll, not about any single one.
		for (var poll = 0; poll < 8; poll++)
		{
			var delivered = await PollAsync(processor, TestCancellationToken).ConfigureAwait(false);
			foreach (var change in delivered)
			{
				seen.Add(Convert.ToHexString(change.Lsn));
			}

			if (delivered.Count == 0)
			{
				break;
			}
		}

		// THE SAFETY PROPERTY. Not "something was delivered" - every captured change, at least once.
		// A shortfall of roughly QueueSize is the specific signature predicted in advance: the changes
		// lost are the ones sitting in the discarded bounded queue when the batch ended abnormally.
		seen.Distinct(StringComparer.Ordinal).Count().ShouldBe(
			ChangeCount,
			$"captured changes were never delivered at all. Expected all {ChangeCount}; a shortfall of "
			+ $"about {QueueSize} is the predicted signature of changes discarded with the bounded queue "
			+ "when the batch ended abnormally, because the in-memory resume position advances as the "
			+ "producer ENQUEUES while the durable checkpoint advances only as the consumer DELIVERS.");
	}

	/// <summary>
	/// Builds a CDC-enabled database, a captured table, the shipped state schema, and a real DI container.
	/// </summary>
	/// <param name="label">Distinguishes this test's database from its siblings' on the shared container.</param>
	/// <returns>The scoped connection string, the provider to dispose, and the resolved processor.</returns>
	private async Task<(string ConnectionString, ServiceProvider Provider, ISqlServerCdcProcessor Processor)>
		ArrangeAsync(string label, Action<IServiceCollection, string, string>? preRegister = null)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the SQL Server CDC arms require a real Agent-enabled container and are NEVER skipped: "
			+ "the defect they bind is invisible without one, because the producer reaches the database "
			+ "before anything reaches the channel.");

		var database = $"Cdc{label}{Guid.NewGuid():N}"[..24];
		var connectionString = await _fixture
			.CreateCdcEnabledDatabaseAsync(database, TestCancellationToken)
			.ConfigureAwait(false);

		// The provider never creates its state table; the consumer runs the shipped script. Run THAT
		// script, so these arms redden if it ever stops matching what CdcStateStore reads.
		await ApplyShippedStateSchemaAsync(connectionString).ConfigureAwait(false);

		await ExecuteAsync(
			connectionString,
			"CREATE TABLE dbo.Orders (Id INT PRIMARY KEY, Reference NVARCHAR(64) NOT NULL);")
			.ConfigureAwait(false);

		await SqlServerCdcContainerFixture
			.EnableTableCaptureAsync(connectionString, "dbo", "Orders", TestCancellationToken)
			.ConfigureAwait(false);

		// The REAL registration path, not a hand-built processor: a lock that constructs the component itself
		// proves only that it works when handed its dependencies, never that the container resolves it.
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
		_ = services.AddSingleton<IHostApplicationLifetime, TestHostLifetime>();

		// BEFORE AddCdcProcessor, deliberately. The provider registers IDatabaseOptions with
		// TryAddSingleton, so anything registered here wins - which is the only way a test can reach
		// QueueSize and ConsumerBatchSize, since the fluent builder exposes neither.
		preRegister?.Invoke(services, database, connectionString);

		_ = services.AddCdcProcessor(cdc =>
			cdc.UseSqlServer(sql => sql
					.ConnectionString(connectionString)
					.DatabaseName(database)
					.BatchSize(100))
				.TrackTable("dbo.Orders", table => table.MapAll<OrderChanged>()));

		var provider = services.BuildServiceProvider();
		return (connectionString, provider, provider.GetRequiredService<ISqlServerCdcProcessor>());
	}

	/// <summary>Runs one batch and collects everything it delivers.</summary>
	/// <param name="processor">The processor under test.</param>
	/// <param name="cancellationToken">Cancels the batch.</param>
	/// <returns>The delivered changes.</returns>
	private static async Task<IReadOnlyCollection<DataChangeEvent>> PollAsync(
		ISqlServerCdcProcessor processor,
		CancellationToken cancellationToken)
	{
		var delivered = new ConcurrentBag<DataChangeEvent>();

		_ = await processor.ProcessBatchAsync(
			(change, _) =>
			{
				delivered.Add(change);
				return Task.CompletedTask;
			},
			cancellationToken).ConfigureAwait(false);

		return delivered;
	}

	/// <summary>Writes a row and waits for the capture job to surface it.</summary>
	/// <param name="connectionString">A connection scoped to the CDC-enabled database.</param>
	/// <param name="id">The row's key.</param>
	/// <param name="reference">The row's payload.</param>
	/// <param name="expectedCapturedRows">How many rows the change table must hold before returning.</param>
	private async Task InsertAndAwaitCaptureAsync(
		string connectionString,
		int id,
		string reference,
		int expectedCapturedRows)
	{
		await ExecuteAsync(
			connectionString,
			"INSERT INTO dbo.Orders VALUES (@Id, @Reference);",
			("@Id", id),
			("@Reference", reference)).ConfigureAwait(false);

		(await SqlServerCdcContainerFixture.WaitForCapturedRowsAsync(
			connectionString, "dbo_Orders", expectedCapturedRows, CaptureWindow, TestCancellationToken)
			.ConfigureAwait(false))
			.ShouldBeTrue(
				$"the capture job did not surface insert {id} within {CaptureWindow.TotalSeconds:F0}s, so "
				+ "nothing below tests the processor");
	}

	/// <summary>Reads the highest checkpoint the processor has recorded, or null if it has recorded none.</summary>
	/// <param name="connectionString">A connection scoped to the CDC-enabled database.</param>
	/// <returns>The stored log sequence number, or <see langword="null"/>.</returns>
	private static async Task<byte[]?> ReadCheckpointAsync(string connectionString)
	{
		var value = await ScalarAsync(
			connectionString,
			"SELECT MAX([LastProcessedLsn]) FROM [Cdc].[CdcProcessingState];").ConfigureAwait(false);

		return value as byte[];
	}

	/// <summary>Orders two fixed-width log sequence numbers.</summary>
	/// <param name="left">The later reading.</param>
	/// <param name="right">The earlier reading.</param>
	/// <returns>Positive when <paramref name="left"/> is the greater.</returns>
	/// <remarks>
	/// SQL Server log sequence numbers are <c>binary(10)</c> and order lexicographically, most significant
	/// byte first. Comparing them as strings or converting to a numeric type would both misorder them.
	/// </remarks>
	private static int Compare(byte[] left, byte[] right)
	{
		for (var i = 0; i < left.Length && i < right.Length; i++)
		{
			if (left[i] != right[i])
			{
				return left[i] > right[i] ? 1 : -1;
			}
		}

		return left.Length.CompareTo(right.Length);
	}

	/// <summary>
	/// Applies the schema script this package SHIPS, rather than a copy of its DDL.
	/// </summary>
	/// <param name="connectionString">A connection scoped to the CDC-enabled database.</param>
	/// <remarks>
	/// <para>
	/// The SQL Server provider deliberately never creates its state table at runtime - the script header
	/// says so - so a consumer runs <c>001_CreateCdcStateSchema.sql</c> by hand. That makes the script
	/// consumer-facing code with nothing behind it: if its columns drift from the ones
	/// <c>CdcStateStore</c> reads and writes, no shard in this repository goes red and the failure lands
	/// on the consumer's database the first time they checkpoint. Executing the shipped file here,
	/// instead of pasting its DDL into this class, makes these arms that missing backstop at no extra
	/// cost. A pasted copy would drift with the script and never notice.
	/// </para>
	/// </remarks>
	private static async Task ApplyShippedStateSchemaAsync(string connectionString)
	{
		var script = await File.ReadAllTextAsync(LocateShippedStateSchema()).ConfigureAwait(false);

		foreach (var batch in SplitOnGoSeparators(script))
		{
			await ExecuteAsync(connectionString, batch).ConfigureAwait(false);
		}
	}

	/// <summary>Finds the shipped script by walking up to the repository root.</summary>
	/// <returns>The absolute path of the script.</returns>
	/// <remarks>
	/// The script is packaged with <c>Pack="true"</c> and is neither copied to the test output nor
	/// embedded, so there is no assembly to read it from. Failing loudly here matters: a silent fallback
	/// to inline DDL would turn the drift detector above back into the copy it exists to avoid.
	/// </remarks>
	private static string LocateShippedStateSchema()
	{
		const string Relative = "src/Excalibur/Excalibur.Cdc.SqlServer/Scripts/001_CreateCdcStateSchema.sql";

		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			if (!File.Exists(Path.Combine(dir.FullName, "Excalibur.sln")))
			{
				continue;
			}

			var candidate = Path.Combine(dir.FullName, Relative);
			if (File.Exists(candidate))
			{
				return candidate;
			}

			throw new FileNotFoundException(
				$"Found the repository root at '{dir.FullName}' but not the shipped CDC state schema "
				+ "beneath it. These arms bind the script a consumer runs; they must not substitute their "
				+ "own DDL, because a substitute cannot detect the script drifting.",
				candidate);
		}

		throw new DirectoryNotFoundException(
			$"No repository root above '{AppContext.BaseDirectory}'. The shipped CDC state schema cannot "
			+ "be located, so these arms would be testing DDL written here rather than DDL we ship.");
	}

	/// <summary>Splits a script on its batch separators, which a single command cannot execute.</summary>
	/// <param name="script">The script text.</param>
	/// <returns>The non-empty batches, in order.</returns>
	private static IEnumerable<string> SplitOnGoSeparators(string script)
	{
		var batch = new StringBuilder();

		foreach (var line in script.Split('\n'))
		{
			if (string.Equals(line.Trim(), "GO", StringComparison.OrdinalIgnoreCase))
			{
				if (batch.ToString().Trim().Length > 0)
				{
					yield return batch.ToString();
				}

				_ = batch.Clear();
				continue;
			}

			_ = batch.AppendLine(line);
		}

		if (batch.ToString().Trim().Length > 0)
		{
			yield return batch.ToString();
		}
	}

	private static async Task ExecuteAsync(
		string connectionString,
		string sql,
		params (string Name, object Value)[] parameters)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: every call site of this helper is inside this file and passes either a compile-time
		// literal or a batch of the shipped schema script - never caller-supplied text, and row values
		// travel as parameters. The helper is private, so the set of call sites is enumerable by reading
		// the class. The suppression is on the construction only, not the method.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		foreach (var (name, value) in parameters)
		{
			_ = command.Parameters.AddWithValue(name, value);
		}

		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private static async Task<object?> ScalarAsync(string connectionString, string sql)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: see ExecuteAsync above - private helper, literal SQL at every call site.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
		await using var command = new SqlCommand(sql, connection);
#pragma warning restore CA2100
		var value = await command.ExecuteScalarAsync().ConfigureAwait(false);

		return value == DBNull.Value ? null : value;
	}

	/// <summary>A lifetime that never signals stopping, so the processor's shutdown path stays out of the way.</summary>
	private sealed class TestHostLifetime : IHostApplicationLifetime
	{
		public CancellationToken ApplicationStarted { get; } = new CancellationTokenSource().Token;

		public CancellationToken ApplicationStopping { get; } = new CancellationTokenSource().Token;

		public CancellationToken ApplicationStopped { get; } = new CancellationTokenSource().Token;

		public void StopApplication()
		{
		}
	}

	/// <summary>The mapped event. Shape is irrelevant here; delivery is the property under test.</summary>
	private sealed class OrderChanged
	{
		public int Id { get; set; }

		public string? Reference { get; set; }
	}
}
