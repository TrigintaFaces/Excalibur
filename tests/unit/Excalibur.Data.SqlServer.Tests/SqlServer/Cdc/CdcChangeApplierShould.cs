// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Threading.Channels;

using Excalibur.Cdc.SqlServer;
using Excalibur.Domain;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcChangeApplier"/> — the consumer-side component
/// extracted from CdcProcessor during the CDC SqlServer decomposition.
/// Tests exercise ConsumerLoopAsync, ProcessBatchAsync, error handling,
/// checkpoint alignment, and fatal error delegation.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcChangeApplierShould : UnitTestBase
{
	// Access the internal CdcChangeApplier via reflection from the CdcProcessor
	private static readonly FieldInfo ChangeApplierField = typeof(CdcProcessor)
		.GetField("_changeApplier", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected _changeApplier field on CdcProcessor.");

	private static readonly MethodInfo ConsumerLoopMethod = typeof(CdcChangeApplier)
		.GetMethod("ConsumerLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected internal ConsumerLoopAsync method.");

	private static readonly MethodInfo ProcessBatchMethod = typeof(CdcChangeApplier)
		.GetMethod("ProcessBatchAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private ProcessBatchAsync method.");

	[Fact]
	public async Task ConsumerLoopAsync_ProcessesEventsFromChannel()
	{
		// Arrange — after successful event processing, CdcChangeApplier calls
		// CheckpointManager.UpdateTableLastProcessedAsync which uses a real SqlConnection.
		// With our test setup using a non-connected SqlConnection, this fails.
		// We verify the eventHandler IS invoked for each event, then expect the
		// checkpoint update to throw (which ConsumerLoop rethrows).
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		var processedEvents = new List<DataChangeEvent>();

		var evt1 = CreateEvent("table1", [0x01], [0x01]);

		await channel.Writer.WriteAsync(evt1).ConfigureAwait(false);
		channel.Writer.Complete();

		// Act — the eventHandler will be called, but the checkpoint update after
		// will fail because it uses a real SqlConnection. We catch to verify the
		// handler was actually invoked.
		try
		{
			await InvokeConsumerLoopAsync(
				applier,
				channel.Reader,
				(evt, ct) =>
				{
					processedEvents.Add(evt);
					return Task.CompletedTask;
				},
				isDisposed: () => false,
				shouldWaitForProducer: () => false,
				isProducerStopped: () => true).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
		{
			// Expected — CdcStateStore fails on non-connected SqlConnection during checkpoint update
			// SqlException: "Login failed for user ''" from the real SqlConnection
		}

		// Assert — the event handler was invoked before the checkpoint failed
		processedEvents.Count.ShouldBe(1);
		processedEvents[0].ShouldBeSameAs(evt1);
	}

	[Fact]
	public async Task ConsumerLoopAsync_ReturnsZero_WhenChannelIsEmpty()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		channel.Writer.Complete();

		// Act
		var result = await InvokeConsumerLoopAsync(
			applier,
			channel.Reader,
			(_, _) => Task.CompletedTask,
			isDisposed: () => false,
			shouldWaitForProducer: () => false,
			isProducerStopped: () => true).ConfigureAwait(false);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task ConsumerLoopAsync_ExitsOnDisposal()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Don't complete the channel — disposal should break the loop
		await channel.Writer.WriteAsync(CreateEvent("t", [0x01], [0x01])).ConfigureAwait(false);

		// Act
		var result = await InvokeConsumerLoopAsync(
			applier,
			channel.Reader,
			(_, _) => Task.CompletedTask,
			isDisposed: () => true,
			shouldWaitForProducer: () => false,
			isProducerStopped: () => false).ConfigureAwait(false);

		// Assert — exits immediately due to disposal
		result.ShouldBe(0);
	}

	[Fact]
	public async Task ConsumerLoopAsync_ExitsWhenProducerStoppedAndChannelDrained()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		channel.Writer.Complete(); // Producer stopped, channel empty

		// Act
		var result = await InvokeConsumerLoopAsync(
			applier,
			channel.Reader,
			(_, _) => Task.CompletedTask,
			isDisposed: () => false,
			shouldWaitForProducer: () => false,
			isProducerStopped: () => true).ConfigureAwait(false);

		// Assert
		result.ShouldBe(0);
	}

	[Fact]
	public async Task ConsumerLoopAsync_RethrowsNonCancellationExceptions()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		await channel.Writer.WriteAsync(CreateEvent("t", [0x01], [0x01])).ConfigureAwait(false);
		channel.Writer.Complete();

		// Act & Assert — eventHandler throws, ConsumerLoop should rethrow
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await InvokeConsumerLoopAsync(
				applier,
				channel.Reader,
				(_, _) => throw new InvalidOperationException("Test failure"),
				isDisposed: () => false,
				shouldWaitForProducer: () => false,
				isProducerStopped: () => true).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ConsumerLoopAsync_HandlesOperationCanceledGracefully()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		using var cts = new CancellationTokenSource();

		// Write an event and then cancel before processing
		await channel.Writer.WriteAsync(CreateEvent("t", [0x01], [0x01])).ConfigureAwait(false);

		// Act — cancel immediately
		await cts.CancelAsync().ConfigureAwait(false);

		// The consumer loop should handle cancellation gracefully
		var result = await InvokeConsumerLoopAsync(
			applier,
			channel.Reader,
			(_, _) => Task.CompletedTask,
			isDisposed: () => false,
			shouldWaitForProducer: () => false,
			isProducerStopped: () => true,
			cancellationToken: cts.Token).ConfigureAwait(false);

		// Assert — exits with 0 processed since cancelled
		result.ShouldBe(0);
	}

	[Fact]
	public async Task ConsumerLoopAsync_ThrowsOnNullEventHandler()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await InvokeConsumerLoopAsync(
				applier,
				channel.Reader,
				null!,
				isDisposed: () => false,
				shouldWaitForProducer: () => false,
				isProducerStopped: () => false).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessBatchAsync_DelegatesErrorToFatalHandler_WhenConfigured()
	{
		// Arrange — create processor with fatal error handler
		var capturedErrors = new List<(Exception Ex, DataChangeEvent Evt)>();

		using var processor = CreateProcessor(onFatalError: async (ex, evt) =>
		{
			capturedErrors.Add((ex, evt));
			await Task.CompletedTask.ConfigureAwait(false);
		});

		var applier = GetChangeApplier(processor);
		var failingEvent = CreateEvent("orders", [0x10], [0x01]);
		var batch = new List<DataChangeEvent> { failingEvent };

		// Act — process a batch where the eventHandler fails
		await InvokeProcessBatchAsync(
			applier,
			batch,
			(_, _) => throw new InvalidOperationException("handler failure"),
			CancellationToken.None).ConfigureAwait(false);

		// Assert — fatal error handler captured the exception
		capturedErrors.Count.ShouldBe(1);
		capturedErrors[0].Ex.ShouldBeOfType<InvalidOperationException>();
		capturedErrors[0].Evt.ShouldBeSameAs(failingEvent);
	}

	[Fact]
	public async Task ProcessBatchAsync_ThrowsOnError_WhenNoFatalHandlerConfigured()
	{
		// Arrange — no fatal error handler
		using var processor = CreateProcessor(onFatalError: null);
		var applier = GetChangeApplier(processor);
		var batch = new List<DataChangeEvent> { CreateEvent("t", [0x01], [0x01]) };

		// Act & Assert — should rethrow since no onFatalError
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await InvokeProcessBatchAsync(
				applier,
				batch,
				(_, _) => throw new InvalidOperationException("unhandled"),
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessBatchAsync_ThrowsOnNullBatch()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await InvokeProcessBatchAsync(
				applier,
				null!,
				(_, _) => Task.CompletedTask,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ProcessBatchAsync_ThrowsOnNullEventHandler()
	{
		// Arrange
		using var processor = CreateProcessor();
		var applier = GetChangeApplier(processor);
		var batch = new List<DataChangeEvent>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await InvokeProcessBatchAsync(
				applier,
				batch,
				null!,
				CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	// --- Helpers ---

	private static CdcProcessor CreateProcessor(CdcFatalErrorHandler? onFatalError = null)
	{
		var appLifetime = A.Fake<IHostApplicationLifetime>();
		var dbConfig = A.Fake<IDatabaseOptions>();
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();
		var logger = A.Fake<ILogger<CdcProcessor>>();

		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.DatabaseConnectionIdentifier).Returns("test-connection");
		A.CallTo(() => dbConfig.DatabaseName).Returns("test-db");
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);

		var noOpPolicy = Policy.NoOpAsync();
		A.CallTo(() => policyFactory.GetComprehensivePolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.GetRetryPolicy()).Returns(noOpPolicy);
		A.CallTo(() => policyFactory.CreateCircuitBreakerPolicy()).Returns(noOpPolicy);

		IOptions<CdcFatalErrorOptions>? fatalErrorOptions = onFatalError is not null
			? Microsoft.Extensions.Options.Options.Create(new CdcFatalErrorOptions { OnFatalError = onFatalError })
			: null;

		return new CdcProcessor(
			appLifetime,
			dbConfig,
			new CdcRepository(new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true")),
			new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			logger,
			fatalErrorOptions);
	}

	private static object GetChangeApplier(CdcProcessor processor) =>
		ChangeApplierField.GetValue(processor)
		?? throw new InvalidOperationException("_changeApplier field was null.");

	private static DataChangeEvent CreateEvent(string tableName, byte[] lsn, byte[] seqVal) =>
		new()
		{
			TableName = tableName,
			Lsn = lsn,
			SeqVal = seqVal,
			CommitTime = DateTime.UtcNow,
			ChangeType = DataChangeType.Insert,
			Changes = [new DataChange { ColumnName = "Id", NewValue = 1, OldValue = null }],
		};

	private static async Task<int> InvokeConsumerLoopAsync(
		object applier,
		ChannelReader<DataChangeEvent> reader,
		Func<DataChangeEvent, CancellationToken, Task>? eventHandler,
		Func<bool> isDisposed,
		Func<bool> shouldWaitForProducer,
		Func<bool> isProducerStopped,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var task = (Task<int>)ConsumerLoopMethod.Invoke(
				applier,
				[reader, eventHandler, isDisposed, shouldWaitForProducer, isProducerStopped, cancellationToken])!;
			return await task.ConfigureAwait(false);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}

	private static async Task InvokeProcessBatchAsync(
		object applier,
		IReadOnlyList<DataChangeEvent>? batch,
		Func<DataChangeEvent, CancellationToken, Task>? eventHandler,
		CancellationToken cancellationToken)
	{
		try
		{
			var task = (Task)ProcessBatchMethod.Invoke(
				applier,
				[batch, eventHandler, cancellationToken])!;
			await task.ConfigureAwait(false);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}
}
