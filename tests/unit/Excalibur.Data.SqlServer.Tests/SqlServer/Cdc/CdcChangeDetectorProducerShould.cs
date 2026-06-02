// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Threading.Channels;

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

using Polly;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests for <see cref="CdcChangeDetector"/> producer loop behavior:
/// ProducerLoopAsync exception handling, channel lifecycle management,
/// and SQL error propagation.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcChangeDetectorProducerShould : UnitTestBase
{
	private static readonly FieldInfo ChangeDetectorField = typeof(CdcProcessor)
		.GetField("_changeDetector", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected _changeDetector field on CdcProcessor.");

	private static readonly MethodInfo ProducerLoopAsyncMethod = typeof(CdcChangeDetector)
		.GetMethod("ProducerLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected internal ProducerLoopAsync method.");

	[Fact]
	public async Task ProducerLoopAsync_CompletesChannel_WhenNullStartLsn()
	{
		// Arrange — null lowestStartLsn: ProducerLoopCoreAsync checks
		// currentGlobalLsn != null first, so exits immediately without DB calls
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act — null LSN means loop body never executes, but finally block completes channel
		// ProducerLoopCoreAsync calls GetMaxPositionAsync which needs DB,
		// so ProducerLoopAsync catches the exception and completes the channel
		try
		{
			await InvokeProducerLoopAsync(detector, null, channel.Writer, 32).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// GetMaxPositionAsync may fail on non-connected SqlConnection
		}

		// Assert — channel should be completed in the finally block regardless
		channel.Reader.Completion.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public async Task ProducerLoopAsync_CompletesChannel_OnCancellation()
	{
		// Arrange
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act — should handle cancellation gracefully and still complete the channel
		// The repository call may fail before cancellation check, so catch both paths
		try
		{
			await InvokeProducerLoopAsync(detector, new byte[] { 0x01 }, channel.Writer, 32, cts.Token).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Either SqlException (repo fails) or OperationCanceledException (caught internally)
		}

		// Assert — channel should always be completed in the finally block
		channel.Reader.Completion.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public async Task ProducerLoopAsync_CompletesChannel_OnSqlException()
	{
		// Arrange — the ProducerLoopCoreAsync will call into the repository which
		// uses a real SqlConnection that will fail, exercising the SqlException path
		using var processor = CreateProcessor();
		var detector = GetChangeDetector(processor);
		var channel = Channel.CreateUnbounded<DataChangeEvent>();

		// Act & Assert — SqlException should propagate but channel must be completed
		try
		{
			await InvokeProducerLoopAsync(detector, new byte[] { 0x01 }, channel.Writer, 32).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Expected — repository call fails with real (non-connected) SqlConnection
		}

		// Channel must always be completed regardless of exception
		channel.Reader.Completion.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public void ByteArrayToHex_ConvertsMaxValueBytesCorrectly()
	{
		// Edge case — all 0xFF bytes
		var bytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0xFFFFFFFF");
	}

	[Fact]
	public void ByteArrayToHex_LargeArray_FormatsCorrectly()
	{
		// Edge case — typical LSN-sized array (10 bytes)
		var bytes = new byte[] { 0x00, 0x00, 0x00, 0x2A, 0x00, 0x00, 0x01, 0xF4, 0x00, 0x01 };

		var result = CdcChangeDetector.ByteArrayToHex(bytes);

		result.ShouldBe("0x0000002A000001F40001");
	}

	// --- Helpers ---

	private static CdcProcessor CreateProcessor()
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

		return new CdcProcessor(
			appLifetime,
			dbConfig,
			new CdcRepository(new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true")),
			new SqlConnection("Server=localhost;Database=master;Encrypt=false;TrustServerCertificate=true"),
			stateStoreOptions: null,
			policyFactory,
			logger);
	}

	private static object GetChangeDetector(CdcProcessor processor) =>
		ChangeDetectorField.GetValue(processor)
		?? throw new InvalidOperationException("_changeDetector field was null.");

	private static async Task InvokeProducerLoopAsync(
		object detector,
		byte[]? lowestStartLsn,
		ChannelWriter<DataChangeEvent> writer,
		int queueSize,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var task = (Task)ProducerLoopAsyncMethod.Invoke(
				detector,
				[lowestStartLsn, writer, queueSize, cancellationToken])!;
			await task.ConfigureAwait(false);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			throw ex.InnerException;
		}
	}
}