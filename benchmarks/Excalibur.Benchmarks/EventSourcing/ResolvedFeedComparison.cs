// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Data.SqlClient;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>Research-only comparison of two complete single-tenant feed kernels, not shipping adapters.</summary>
internal static class ResolvedFeedComparison
{
	internal sealed class Observation
	{
		private long _appliedBits = -1;

		public int Id { get; init; }

		public double ScheduledMs { get; init; }

		public double SubmittedMs { get; set; }

		public double? StartedMs { get; set; }

		public double? AcknowledgedMs { get; set; }

		public double? AppliedMs
		{
			get
			{
				var bits = Volatile.Read(ref _appliedBits);
				return bits == -1 ? null : BitConverter.Int64BitsToDouble(bits);
			}

			set => Volatile.Write(ref _appliedBits, value is { } ms ? BitConverter.DoubleToInt64Bits(ms) : -1);
		}

		public string? Failure { get; set; }
	}

	internal static Task<int> RunAsync(string[] args) => RunCellAsync(args, warmup: true);

	private static async Task<int> RunCellAsync(string[] args, bool warmup)
	{
		if (args.Length != 6 || args[0] is not ("gapless" or "watermark"))
		{
			Console.Error.WriteLine("feed-spike <gapless|watermark> <writers> <batch> <offered requests/sec> <requests> <new output.json>");
			return 2;
		}

		var fault = warmup ? Environment.GetEnvironmentVariable("WATERMARK_SPIKE_TEST_FAULT") : null;
		if (fault is not (null or "" or "payload" or "counter" or "projection" or "after-drain" or "late-append" or "late-apply"))
		{
			throw new ArgumentException("Unknown experiment fault.", nameof(args));
		}

		var arm = args[0];
		var writers = int.Parse(args[1], CultureInfo.InvariantCulture);
		var batch = int.Parse(args[2], CultureInfo.InvariantCulture);
		var rate = int.Parse(args[3], CultureInfo.InvariantCulture);
		var count = int.Parse(args[4], CultureInfo.InvariantCulture);
		var output = Path.GetFullPath(args[5]);
		if (writers is < 1 or > 64 || batch is < 1 or > 100 || rate is < 1 or > 100000 || count is < 1 or > 2000000 || File.Exists(output))
		{
			throw new ArgumentException("Invalid matrix cell or existing evidence path.", nameof(args));
		}

		if (!string.Equals(Environment.GetEnvironmentVariable("WATERMARK_SPIKE_DISPOSABLE"), "1", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("This experiment requires an explicitly disposable SQL Server.");
		}

		var connectionString = Environment.GetEnvironmentVariable("BENCHMARK_SQL_CONNECTIONSTRING")
		?? throw new InvalidOperationException("Missing disposable SQL Server connection.");
		if (warmup)
		{
			var warmupArgs = (string[])args.Clone();
			warmupArgs[3] = Math.Min(rate, 100).ToString(CultureInfo.InvariantCulture);
			warmupArgs[4] = "200";
			warmupArgs[5] = output + ".warmup.json";
			if (await RunCellAsync(warmupArgs, warmup: false).ConfigureAwait(false) != 0)
			{
				return 1;
			}
		}

		var database = "FeedSpike_" + Guid.NewGuid().ToString("N");
		var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", MaxPoolSize = writers + 8 };
		var master = builder.ConnectionString;
		builder.InitialCatalog = database;
		var target = builder.ConnectionString;
		var samples = Enumerable.Range(0, count).Select(i => new Observation { Id = i, ScheduledMs = i * 1000d / rate }).ToArray();
		var failures = new ConcurrentQueue<string>();
		var stage = "create-database";
		var owned = false;
		var validated = false;
		var cleanupPassed = false;
		var elapsed = 0d;
		long publicationCalls = 0, pageCalls = 0;
		string? engine = null;
		object? logBefore = null, logAfter = null;
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds((count / (double)rate) + 90));
		try
		{
			await ExecuteAsync(master, $"CREATE DATABASE [{database}];", stop.Token).ConfigureAwait(false);
			owned = true;
			await ExecuteAsync(master, $"ALTER DATABASE [{database}] SET READ_COMMITTED_SNAPSHOT ON;", stop.Token).ConfigureAwait(false);
			stage = "create-schema";
			await ExecuteAsync(target, SchemaSql(arm), stop.Token).ConfigureAwait(false);
			var info = new SqlConnection(target);
			await using (info.ConfigureAwait(false))
			{
				await info.OpenAsync(stop.Token).ConfigureAwait(false);
				var version = new SqlCommand("SELECT @@VERSION", info);
				await using var versionScope = version.ConfigureAwait(false);
				engine = (string)(await version.ExecuteScalarAsync(stop.Token).ConfigureAwait(false))!;
			}

			logBefore = await ReadLogAsync(target, stop.Token).ConfigureAwait(false);
			stage = "workload";
			(validated, elapsed, publicationCalls, pageCalls, logAfter) = await RunWorkloadAsync(target, arm, writers, batch, rate, samples, failures, stop, fault).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			failures.Enqueue("run:" + stage + ":" + Describe(ex));
		}
		finally
		{
			await stop.CancelAsync().ConfigureAwait(false);
			if (owned)
			{
				try
				{
					SqlConnection.ClearAllPools();
					await ExecuteAsync(master, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];", CancellationToken.None).ConfigureAwait(false);
					cleanupPassed = true;
				}
				catch (Exception ex)
				{
					failures.Enqueue("cleanup:" + Describe(ex));
				}
			}

			var complete = validated && cleanupPassed && failures.IsEmpty;
			foreach (var sample in samples.Where(s => s.AppliedMs is null && s.Failure is null))
			{
				sample.Failure = "unresolved-or-not-submitted";
			}

			var windowMs = count * 1000d / rate;
			var latencies = complete ? samples.Select(s => s.AppliedMs!.Value - s.ScheduledMs).Order().ToArray() : [];
			var lastApplied = samples.Max(s => s.AppliedMs ?? 0);
			Directory.CreateDirectory(Path.GetDirectoryName(output)!);
			await File.WriteAllTextAsync(output, JsonSerializer.Serialize(
				new
				{
					arm,
					writers,
					batch,
					rate,
					count,
					database,
					fault,
					isWarmup = !warmup,
					payloadBytes = 1024,
					pageSize = 256,
					queueSeconds = 2,
					recoverySeconds = 60,
					complete,
					validated,
					cleanupPassed,
					engine,
					runtime = Environment.Version.ToString(),
					processorCount = Environment.ProcessorCount,
					stopwatchFrequency = Stopwatch.Frequency,
					windowMs,
					elapsedMs = elapsed,
					drainMs = Math.Max(0, lastApplied - windowMs),
					appliedAtCutoff = samples.Count(s => s.AppliedMs <= windowMs),
					completedBatchesPerSecond = complete ? count * 1000d / Math.Max(windowMs, lastApplied) : (double?)null,
					p99ScheduledToAppliedMs = complete ? latencies[(int)Math.Ceiling(count * .99) - 1] : (double?)null,
					publicationCalls,
					pageCalls,
					logBefore,
					logAfter,
					failures = failures.ToArray(),
					samples,
					qualification = "Research kernel only; no shipping adapter or production adoption qualification",
				}, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None).ConfigureAwait(false);
		}

		return validated && cleanupPassed && failures.IsEmpty ? 0 : 1;
	}

	private static async Task<(bool Validated, double Elapsed, long Publications, long Pages, object? Log)> RunWorkloadAsync(
	string target, string arm, int writers, int batch, int rate, Observation[] samples, ConcurrentQueue<string> failures, CancellationTokenSource stop, string? fault)
	{
		var count = samples.Length;
		var tasks = new List<Task>();
		var clock = new Stopwatch();
		var validated = false;
		var finishWorkers = false;
		var elapsed = 0d;
		long publicationCalls = 0, pageCalls = 0;
		object? logAfter = null;
		try
		{
			var queue = CreateQueue(writers, count, rate);
			var start = CreateStartSignal();
			using var ready = new SemaphoreSlim(0);
			var payload = new byte[1024];
			Array.Fill(payload, (byte)42);
			for (var worker = 0; worker < writers; worker++)
			{
				tasks.Add(WriteAsync());
			}

			tasks.Add(ReadAsync());
			if (string.Equals(arm, "watermark", StringComparison.Ordinal))
			{
				tasks.Add(PublishAsync());
			}

			for (var i = 0; i < tasks.Count; i++)
			{
				await ready.WaitAsync(stop.Token).ConfigureAwait(false);
			}

			if (!failures.IsEmpty)
			{
				throw new InvalidOperationException("Worker startup failed");
			}

			clock.Start();
			start.SetResult();
			foreach (var sample in samples)
			{
				while (clock.Elapsed.TotalMilliseconds < sample.ScheduledMs)
				{
					var remaining = sample.ScheduledMs - clock.Elapsed.TotalMilliseconds;
					await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, remaining)), stop.Token).ConfigureAwait(false);
				}

				sample.SubmittedMs = clock.Elapsed.TotalMilliseconds;
				if (!queue.Writer.TryWrite(sample))
				{
					sample.Failure = "admission-capacity";
				}
			}

			queue.Writer.Complete();
			var drainDeadline = TimeSpan.FromMilliseconds((count * 1000d / rate) + 60000);
			await Task.WhenAll(tasks.Take(writers)).WaitAsync(TimeSpan.FromMilliseconds(Math.Max(1, (drainDeadline - clock.Elapsed).TotalMilliseconds)), stop.Token).ConfigureAwait(false);
			while (samples.Any(s => s.Failure is null && s.AppliedMs is null) && clock.Elapsed < drainDeadline)
			{
				if (!failures.IsEmpty)
				{
					break;
				}

				await Task.Delay(10, stop.Token).ConfigureAwait(false);
			}

			elapsed = clock.Elapsed.TotalMilliseconds;
			Volatile.Write(ref finishWorkers, value: true);
			await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30), stop.Token).ConfigureAwait(false);
			if (string.Equals(fault, "late-append", StringComparison.Ordinal))
			{
				samples[0].AcknowledgedMs = drainDeadline.TotalMilliseconds + 1;
			}
			else if (string.Equals(fault, "late-apply", StringComparison.Ordinal))
			{
				samples[0].AppliedMs = drainDeadline.TotalMilliseconds + 1;
			}

			foreach (var sample in samples.Where(s => s.Failure is null))
			{
				if (sample.AcknowledgedMs is null || sample.AppliedMs is null)
				{
					sample.Failure = "unresolved-at-deadline";
				}
				else if (sample.AcknowledgedMs > drainDeadline.TotalMilliseconds || sample.AppliedMs > drainDeadline.TotalMilliseconds)
				{
					sample.Failure = "completion-after-deadline";
				}
			}

			logAfter = await ReadLogAsync(target, CancellationToken.None).ConfigureAwait(false);
			if (failures.IsEmpty && samples.All(s => s.Failure is null && s.AcknowledgedMs is not null && s.AppliedMs is not null))
			{
				await InjectFaultAsync(target, fault).ConfigureAwait(false);
				await ValidateAsync(target, count, batch, arm).ConfigureAwait(false);
				validated = true;
			}

			async Task WriteAsync()
			{
				try
				{
					var connection = new SqlConnection(target);
					await using (connection.ConfigureAwait(false))
					{
						await connection.OpenAsync(stop.Token).ConfigureAwait(false);
						ready.Release();
						await start.Task.WaitAsync(stop.Token).ConfigureAwait(false);
						await foreach (var sample in queue.Reader.ReadAllAsync(stop.Token).ConfigureAwait(false))
						{
							sample.StartedMs = clock.Elapsed.TotalMilliseconds;
							if (sample.StartedMs - sample.ScheduledMs > 2000)
							{
								sample.Failure = "queue-age";
								continue;
							}

							try
							{
								var command = new SqlCommand("AppendBatch", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
								await using var commandScope = command.ConfigureAwait(false);
								command.Parameters.AddWithValue("@Request", sample.Id);
								command.Parameters.AddWithValue("@Batch", batch);
								command.Parameters.AddWithValue("@Payload", payload);
								await command.ExecuteNonQueryAsync(stop.Token).ConfigureAwait(false);
								sample.AcknowledgedMs = clock.Elapsed.TotalMilliseconds;
							}
							catch (Exception ex)
							{
								sample.Failure = Describe(ex);
							}
						}
					}
				}
				catch (OperationCanceledException) when (stop.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					failures.Enqueue("writer:" + Describe(ex));
					ready.Release();
				}
			}

			async Task PublishAsync()
			{
				try
				{
					var connection = new SqlConnection(target);
					await using (connection.ConfigureAwait(false))
					{
						await connection.OpenAsync(stop.Token).ConfigureAwait(false);
						ready.Release();
						await start.Task.WaitAsync(stop.Token).ConfigureAwait(false);
						while (!stop.IsCancellationRequested && !Volatile.Read(ref finishWorkers))
						{
							var command = new SqlCommand("DECLARE @b binary(8)=MIN_ACTIVE_ROWVERSION(); UPDATE Publication SET Bound=@b WHERE Id=1 AND Bound<@b;", connection);
							await using var commandScope = command.ConfigureAwait(false);
							await command.ExecuteNonQueryAsync(stop.Token).ConfigureAwait(false);
							Interlocked.Increment(ref publicationCalls);
							await Task.Delay(1, stop.Token).ConfigureAwait(false);
						}
					}
				}
				catch (OperationCanceledException) when (stop.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					failures.Enqueue("publisher:" + Describe(ex));
					ready.Release();
				}
			}

			async Task ReadAsync()
			{
				try
				{
					var connection = new SqlConnection(target);
					await using (connection.ConfigureAwait(false))
					{
						await connection.OpenAsync(stop.Token).ConfigureAwait(false);
						ready.Release();
						await start.Task.WaitAsync(stop.Token).ConfigureAwait(false);
						while (!stop.IsCancellationRequested && !Volatile.Read(ref finishWorkers))
						{
							var completed = new List<int>();
							{
								var command = new SqlCommand("ApplyPage", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
								await using var commandScope = command.ConfigureAwait(false);
								var reader = await command.ExecuteReaderAsync(stop.Token).ConfigureAwait(false);
								await using var readerScope = reader.ConfigureAwait(false);
								while (await reader.ReadAsync(stop.Token).ConfigureAwait(false))
								{
									completed.Add(reader.GetInt32(0));
								}
							}

							// SQL commits effects/checkpoint before returning completed whole-batch identities.
							var acknowledged = clock.Elapsed.TotalMilliseconds;
							foreach (var id in completed)
							{
								if (samples[id].AppliedMs is not null)
								{
									throw new InvalidOperationException("Duplicate application acknowledgement");
								}

								samples[id].AppliedMs = acknowledged;
							}

							Interlocked.Increment(ref pageCalls);
							if (completed.Count == 0)
							{
								await Task.Delay(1, stop.Token).ConfigureAwait(false);
							}
						}
					}
				}
				catch (OperationCanceledException) when (stop.IsCancellationRequested)
				{
				}
				catch (Exception ex)
				{
					failures.Enqueue("reader:" + Describe(ex));
					ready.Release();
				}
			}
		}
		catch (Exception ex)
		{
			failures.Enqueue("workload:" + Describe(ex));
		}
		finally
		{
			elapsed = clock.Elapsed.TotalMilliseconds;
			await stop.CancelAsync().ConfigureAwait(false);
			try
			{
				await Task.WhenAll(tasks).ConfigureAwait(false);
				logAfter ??= await ReadLogAsync(target, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				failures.Enqueue("drain:" + Describe(ex));
			}
		}

		return (validated, elapsed, publicationCalls, pageCalls, logAfter);
	}

	private static async Task InjectFaultAsync(string target, string? fault)
	{
		if (string.Equals(fault, "after-drain", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Deliberate instrument accounting failure");
		}

		var sql = fault switch
		{
			"payload" => "UPDATE Payloads SET Payload=0x00 WHERE EventId=0;",
			"counter" => "UPDATE Counter SET Value=0 WHERE Id=1;",
			"projection" => "UPDATE FeedCheckpoint SET Rolling=-1 WHERE Id=1;",
			_ => null,
		};
		if (sql is not null)
		{
			await ExecuteAsync(target, sql, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private static string Describe(Exception exception) => exception is SqlException sql
	? $"SqlException number={sql.Number} state={sql.State} class={sql.Class} procedure={sql.Procedure} line={sql.LineNumber}: {sql.Message}"
	: exception.GetType().Name;

	private static TaskCompletionSource CreateStartSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static Channel<Observation> CreateQueue(int writers, int count, int rate) =>
	Channel.CreateBounded<Observation>(new BoundedChannelOptions(Math.Max(writers, Math.Min(count, rate * 2)))
	{
		SingleWriter = true,
		FullMode = BoundedChannelFullMode.Wait,
	});

	private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken token)
	{
		var connection = new SqlConnection(connectionString);
		await using (connection.ConfigureAwait(false))
		{
			await connection.OpenAsync(token).ConfigureAwait(false);

			// SQL is private generated DDL: constants, parsed integers, and a locally generated GUID identifier.
			// Payloads and request identities in measured operations use command parameters.
#pragma warning disable CA2100
			var command = new SqlCommand(sql, connection) { CommandTimeout = 60 };
			await using var commandScope = command.ConfigureAwait(false);
#pragma warning restore CA2100
			await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
		}
	}

	private static async Task<object> ReadLogAsync(string connectionString, CancellationToken token)
	{
		var connection = new SqlConnection(connectionString);
		await using (connection.ConfigureAwait(false))
		{
			await connection.OpenAsync(token).ConfigureAwait(false);
			var command = new SqlCommand("SELECT SUM(num_of_writes),SUM(num_of_bytes_written),SUM(io_stall_write_ms) FROM sys.dm_io_virtual_file_stats(DB_ID(),NULL) v JOIN sys.database_files f ON f.file_id=v.file_id WHERE f.type=1", connection);
			await using var commandScope = command.ConfigureAwait(false);
			var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
			await using var readerScope = reader.ConfigureAwait(false);
			await reader.ReadAsync(token).ConfigureAwait(false);
			return new { writes = reader.GetInt64(0), bytes = reader.GetInt64(1), stallMs = reader.GetInt64(2) };
		}
	}

	private static async Task ValidateAsync(string connectionString, int requests, int batch, string arm)
	{
		var expected = checked(requests * batch);
		var sql = string.Join('\n', new string[]
			{
				$"IF (SELECT COUNT(*) FROM Ledger)<>{expected} OR (SELECT COUNT(*) FROM Applied)<>{expected}",
				$" OR (SELECT COUNT(*) FROM Outbox)<>{expected} OR (SELECT COUNT(*) FROM Payloads)<>{expected}",
				$" OR (SELECT COUNT(*) FROM Streams)<>{requests} THROW 51000,'Incomplete population',1;",
				"IF EXISTS(SELECT EventId FROM Ledger EXCEPT SELECT EventId FROM Applied)",
				" OR EXISTS(SELECT EventId FROM Applied EXCEPT SELECT EventId FROM Ledger) THROW 51000,'Identity mismatch',1;",
				$"IF EXISTS(SELECT 1 FROM Streams WHERE Version<>{batch - 1} OR Request<0 OR Request>={requests}) THROW 51000,'Stream version mismatch',1;",
				"IF EXISTS(SELECT 1 FROM Outbox o FULL JOIN Payloads p ON p.EventId=o.EventId WHERE o.EventId IS NULL OR p.EventId IS NULL",
				" OR o.Payload<>p.Payload OR p.Payload<>CONVERT(varbinary(max),REPLICATE('*',1024))) THROW 51000,'Outbox identity or payload mismatch',1;",
				"IF EXISTS(SELECT 1 FROM Applied a JOIN Ledger l ON l.EventId=a.EventId WHERE a.Position<>l.Position)",
				" THROW 51000,'Applied identity-position mismatch',1;",
				$"IF EXISTS(SELECT 1 FROM Ledger l JOIN Payloads p ON p.EventId=l.EventId WHERE l.Request<>p.Request OR l.LastEvent<>CASE WHEN p.Ordinal={batch - 1} THEN 1 ELSE 0 END)",
				" THROW 51000,'Ledger batch metadata mismatch',1;",
				$"IF {(string.Equals(arm, "gapless", StringComparison.Ordinal) ? "1" : "0")}=1 AND ((SELECT Value FROM Counter WHERE Id=1)<>{expected}",
				$" OR (SELECT MIN(CONVERT(bigint,Position)) FROM Ledger)<>1 OR (SELECT MAX(CONVERT(bigint,Position)) FROM Ledger)<>{expected})",
				" THROW 51000,'Gapless density mismatch',1;",
				$"IF EXISTS(SELECT 1 FROM Payloads WHERE EventId<0 OR EventId>={expected} OR EventId<>CONVERT(bigint,Request)*{batch}+Ordinal",
				$" OR Ordinal<0 OR Ordinal>={batch} OR DATALENGTH(Payload)<>1024) THROW 51000,'Unexpected request or event identity',1;",
				"IF EXISTS(SELECT 1 FROM (SELECT l.Position,LAG(l.Position) OVER(PARTITION BY p.Request ORDER BY p.Ordinal) Previous FROM Ledger l JOIN Payloads p ON p.EventId=l.EventId) x WHERE Previous>=Position)",
				" THROW 51000,'Within-batch allocation order mismatch',1;",
				"IF EXISTS(SELECT 1 FROM (SELECT Position,LAG(Position) OVER(ORDER BY ApplyOrder) Previous FROM Applied) x WHERE Previous>=Position)",
				" THROW 51000,'Application order mismatch',1;",
				"IF EXISTS(SELECT 1 FROM Ledger l JOIN FeedCheckpoint c ON c.Id=1 WHERE l.Position>c.Position OR(l.Position=c.Position AND c.Inclusive=1))",
				" THROW 51000,'FeedCheckpoint not complete',1;",
				"DECLARE @rolling bigint=0,@event bigint;",
				"DECLARE expected_order CURSOR LOCAL FAST_FORWARD FOR SELECT EventId FROM Ledger ORDER BY Position;",
				"OPEN expected_order; FETCH NEXT FROM expected_order INTO @event;",
				"WHILE @@FETCH_STATUS=0 BEGIN SET @rolling=(@rolling*31+@event%17)%1000000007; FETCH NEXT FROM expected_order INTO @event; END;",
				"CLOSE expected_order; DEALLOCATE expected_order;",
				"IF (SELECT Rolling FROM FeedCheckpoint WHERE Id=1)<>@rolling THROW 51000,'Projection state mismatch',1;",
			});
		await ExecuteAsync(connectionString, sql, CancellationToken.None).ConfigureAwait(false);
	}

	private static string SchemaSql(string arm)
	{
		var sparse = string.Equals(arm, "watermark", StringComparison.Ordinal);
		var append = string.Join('\n', new string[]
			{
				"CREATE PROCEDURE AppendBatch @Request int,@Batch int,@Payload varbinary(max) AS",
				"BEGIN",
				" SET NOCOUNT ON; SET XACT_ABORT ON;",
				" BEGIN TRAN;",
				" BEGIN TRY",
				"  -- Workload is independent new streams, expected version -1; PK enforces that precondition.",
				"  INSERT Streams VALUES(@Request,@Batch-1);",
				"  DECLARE @i int=0,@event bigint,@last bigint;",
				"  WHILE @i<@Batch",
				"  BEGIN",
				"   SET @event=CONVERT(bigint,@Request)*@Batch+@i;",
				"   INSERT Payloads VALUES(@event,@Request,@i,@Payload);",
				"   INSERT Outbox VALUES(@event,@Payload);",
				"   SET @i=@i+1;",
				"  END;",
				$"  {(sparse ? string.Empty : "UPDATE Counter SET @last=Value=Value+@Batch WHERE Id=1;")}",
				"  SET @i=0;",
				"  WHILE @i<@Batch",
				"  BEGIN",
				"   SET @event=CONVERT(bigint,@Request)*@Batch+@i;",
				$"   {(sparse ? "INSERT Ledger(EventId,Request,LastEvent) VALUES(@event,@Request,CASE WHEN @i=@Batch-1 THEN 1 ELSE 0 END);" : "INSERT Ledger(EventId,Request,LastEvent,Position) VALUES(@event,@Request,CASE WHEN @i=@Batch-1 THEN 1 ELSE 0 END,CONVERT(binary(8),@last-@Batch+@i+1));")}",
				"   SET @i=@i+1;",
				"  END;",
				"  COMMIT;",
				" END TRY",
				" BEGIN CATCH",
				"  IF XACT_STATE()<>0 ROLLBACK;",
				"  THROW;",
				" END CATCH;",
				"END;",
			});
		var apply = string.Join('\n', new string[]
			{
				"CREATE PROCEDURE ApplyPage AS",
				"BEGIN",
				" SET NOCOUNT ON; SET XACT_ABORT ON;",
				" BEGIN TRAN;",
				" BEGIN TRY",
				"  DECLARE @c binary(8),@inclusive bit,@b binary(8),@event bigint,@position binary(8);",
				"  SELECT @c=Position,@inclusive=Inclusive FROM FeedCheckpoint WITH(UPDLOCK,HOLDLOCK) WHERE Id=1;",
				$"  {(sparse ? "SELECT @b=Bound FROM Publication WHERE Id=1;" : "SELECT @b=COALESCE(MAX(Position),0x0000000000000000) FROM Ledger;")}",
				"  DECLARE @page TABLE(EventId bigint PRIMARY KEY,Request int,LastEvent bit,Position binary(8));",
				"  INSERT @page SELECT TOP(256) EventId,Request,LastEvent,Position FROM Ledger",
				$"   WHERE (Position>@c OR(Position=@c AND @inclusive=1)) AND Position{(sparse ? "<" : "<=")}@b ORDER BY Position;",
				"  IF EXISTS(SELECT 1 FROM @page p LEFT JOIN Payloads e ON e.EventId=p.EventId WHERE e.EventId IS NULL OR DATALENGTH(e.Payload)<>1024)",
				"   THROW 51000,'Missing payload',1;",
				"  DECLARE ordered_page CURSOR LOCAL FAST_FORWARD FOR SELECT EventId,Position FROM @page ORDER BY Position;",
				"  OPEN ordered_page;",
				"  FETCH NEXT FROM ordered_page INTO @event,@position;",
				"  WHILE @@FETCH_STATUS=0",
				"  BEGIN",
				"   INSERT Applied(EventId,Position) VALUES(@event,@position);",
				"   UPDATE FeedCheckpoint SET Rolling=(Rolling*31+@event%17)%1000000007 WHERE Id=1;",
				"   FETCH NEXT FROM ordered_page INTO @event,@position;",
				"  END;",
				"  CLOSE ordered_page; DEALLOCATE ordered_page;",
				"  IF (SELECT COUNT(*) FROM @page)=256",
				"   UPDATE FeedCheckpoint SET Position=(SELECT MAX(Position) FROM @page),Inclusive=0 WHERE Id=1;",
				$"  ELSE UPDATE FeedCheckpoint SET Position=@b,Inclusive={(sparse ? "1" : "0")} WHERE Id=1;",
				"  COMMIT;",
				"  SELECT Request FROM @page WHERE LastEvent=1;",
				" END TRY",
				" BEGIN CATCH",
				"  IF XACT_STATE()<>0 ROLLBACK;",
				"  THROW;",
				" END CATCH;",
				"END;",
			});
		return string.Join('\n', new string[]
			{
				"IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND is_read_committed_snapshot_on=1 AND delayed_durability_desc='DISABLED')",
				" THROW 51000,'Required durability/visibility absent',1;",
				"CREATE TABLE Streams(Request int PRIMARY KEY,Version int NOT NULL);",
				"CREATE TABLE Payloads(EventId bigint PRIMARY KEY,Request int NOT NULL,Ordinal int NOT NULL,Payload varbinary(max) NOT NULL,UNIQUE(Request,Ordinal));",
				"CREATE TABLE Outbox(EventId bigint PRIMARY KEY,Payload varbinary(max) NOT NULL);",
				$"CREATE TABLE Ledger(EventId bigint PRIMARY KEY REFERENCES Payloads(EventId),Request int NOT NULL,LastEvent bit NOT NULL,Position {(sparse ? "rowversion" : "binary(8)")} NOT NULL);",
				"CREATE UNIQUE INDEX IX_Ledger_Position ON Ledger(Position);",
				"CREATE TABLE Counter(Id int PRIMARY KEY,Value bigint NOT NULL); INSERT Counter VALUES(1,0);",
				"CREATE TABLE Publication(Id int PRIMARY KEY,Bound binary(8) NOT NULL); INSERT Publication VALUES(1,0x0000000000000000);",
				"CREATE TABLE FeedCheckpoint(Id int PRIMARY KEY,Position binary(8) NOT NULL,Inclusive bit NOT NULL,Rolling bigint NOT NULL); INSERT FeedCheckpoint VALUES(1,0x0000000000000000,1,0);",
				"CREATE TABLE Applied(ApplyOrder bigint IDENTITY PRIMARY KEY,EventId bigint NOT NULL UNIQUE,Position binary(8) NOT NULL);",
				$"EXEC(N'{append.Replace("'", "''", StringComparison.Ordinal)}');",
				$"EXEC(N'{apply.Replace("'", "''", StringComparison.Ordinal)}');",
			});
	}
}
