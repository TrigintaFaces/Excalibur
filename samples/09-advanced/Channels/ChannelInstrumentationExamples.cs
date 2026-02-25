// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

#pragma warning disable CA5394 // Do not use insecure randomness - This is example code, not security-sensitive

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading.Channels;
using Excalibur.Dispatch.Channels.Diagnostics;

namespace examples.Channels;

/// <summary>
/// Examples demonstrating ETW instrumentation for channel performance monitoring.
/// </summary>
public static class ChannelInstrumentationExamples
{
	/// <summary>
	/// Example: Basic ETW instrumentation setup and usage.
	/// </summary>
	public static async Task BasicInstrumentationExample()
	{
		// Create an instrumented channel
		var channel = Channel.CreateBounded<string>(100)
			.WithInstrumentation("OrderProcessingChannel", 100);

		// Start periodic metrics reporting
		var metricsTask = channel.ReportMetricsPeriodicallyAsync(
			TimeSpan.FromSeconds(5),
			CancellationToken.None);

		// Simulate order processing
		var producer = ProduceOrders(channel.Writer);
		var consumer = ConsumeOrders(channel.Reader);

		await Task.WhenAll(producer, consumer).ConfigureAwait(false);

		// The metrics will be automatically reported via ETW
		Console.WriteLine("Order processing completed. Check ETW logs for metrics.");
	}

	/// <summary>
	/// Example: Custom ETW listener for monitoring channel performance.
	/// </summary>
	public static async Task CustomEventListenerExample()
	{
		// Create custom event listener
		using var listener = new ChannelPerformanceListener();

		// Create instrumented high-performance channel
		var channel = HighPerformanceChannelExtensions.InstrumentedHighPerformanceChannel
			.CreateSingleProducerConsumer<MarketData>(1000, aggressive: true);

		// Process market data
		var producer = Task.Run(async () =>
		{
			var random = new Random();
			for (var i = 0; i < 100000; i++)
			{
				var data = new MarketData
				{
					Symbol = $"STOCK{random.Next(100)}",
					Price = 100 + Random.Shared.NextDouble() * 50,
					Volume = random.Next(1000, 10000)
				};

				await channel.Writer.WriteAsync(data).ConfigureAwait(false);

				if (i % 10000 == 0)
				{
					Console.WriteLine($"Produced {i} market data items");
				}
			}

			_ = channel.Writer.TryComplete();
		});

		var consumer = Task.Run(async () =>
		{
			var count = 0;
			await foreach (var data in channel.Reader.ReadAllAsync().ConfigureAwait(false))
			{
				// Process market data
				ProcessMarketData(data);
				count++;
			}

			Console.WriteLine($"Processed {count} market data items");
		});

		await Task.WhenAll(producer, consumer).ConfigureAwait(false);

		// Print collected statistics
		listener.PrintStatistics();
	}

	/// <summary>
	/// Example: Multi-channel monitoring with correlation.
	/// </summary>
	public static async Task MultiChannelMonitoringExample()
	{
		// Create multiple instrumented channels for a pipeline
		var rawDataChannel = Channel.CreateBounded<RawData>(1000)
			.WithInstrumentation("RawDataChannel", 1000);

		var processedDataChannel = Channel.CreateBounded<ProcessedData>(500)
			.WithInstrumentation("ProcessedDataChannel", 500);

		var outputChannel = Channel.CreateBounded<FinalOutput>(100)
			.WithInstrumentation("OutputChannel", 100);

		// Create pipeline stages with activity tracking
		var stage1 = Task.Run(async () =>
		{
			for (var i = 0; i < 10000; i++)
			{
				using (ChannelInstrumentationExtensions.BeginActivity("DataIngestion"))
				{
					var raw = new RawData { Id = i, Content = $"Data-{i}" };
					await rawDataChannel.Writer.WriteAsync(raw).ConfigureAwait(false);
				}
			}

			_ = rawDataChannel.Writer.TryComplete();
		});

		var stage2 = Task.Run(async () =>
		{
			await foreach (var raw in rawDataChannel.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false)
				 .ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false))
			{
				using (ChannelInstrumentationExtensions.BeginActivity("DataProcessing"))
				{
					var processed = new ProcessedData { Id = raw.Id, ProcessedContent = raw.Content.ToUpperInvariant() };
					await processedDataChannel.Writer.WriteAsync(processed).ConfigureAwait(false);
				}
			}

			_ = processedDataChannel.Writer.TryComplete();
		});

		var stage3 = Task.Run(async () =>
		{
			await foreach (var processed in processedDataChannel.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false)
				 .ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false))
			{
				using (ChannelInstrumentationExtensions.BeginActivity("FinalTransformation"))
				{
					var output = new FinalOutput { Id = processed.Id, Result = $"[{processed.ProcessedContent}]" };
					await outputChannel.Writer.WriteAsync(output).ConfigureAwait(false);
				}
			}

			_ = outputChannel.Writer.TryComplete();
		});

		var outputTask = Task.Run(async () =>
		{
			var count = 0;
			await foreach (var output in outputChannel.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false)
				 .ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false))
			{
				count++;
			}

			Console.WriteLine($"Pipeline processed {count} items");
		});

		await Task.WhenAll(stage1, stage2, stage3, outputTask).ConfigureAwait(false);
	}

	/// <summary>
	/// Example: Performance comparison using ETW metrics.
	/// </summary>
	public static async Task PerformanceComparisonExample()
	{
		Console.WriteLine("Comparing channel implementations using ETW metrics...\n");

		// Test configuration
		const int messageCount = 100000;
		const int capacity = 1000;

		// Standard channel
		Console.WriteLine("Testing standard bounded channel...");
		var standardChannel = Channel.CreateBounded<TestMessage>(capacity)
			.WithInstrumentation("StandardChannel", capacity);

		await RunPerformanceTest(standardChannel, messageCount).ConfigureAwait(false);
		var standardMetrics = ((InstrumentedChannel<TestMessage>)standardChannel).GetMetrics();
		PrintMetrics("Standard Channel", standardMetrics);

		Console.WriteLine("\nTesting high-performance channel (default)...");
		var hpChannel = HighPerformanceChannelExtensions.InstrumentedHighPerformanceChannel
			.CreateBounded<TestMessage>(capacity, BoundedChannelFullMode.Wait);

		await RunPerformanceTest(hpChannel, messageCount).ConfigureAwait(false);
		var hpMetrics = ((InstrumentedChannel<TestMessage>)hpChannel).GetMetrics();
		PrintMetrics("High-Performance Channel", hpMetrics);

		Console.WriteLine("\nTesting high-performance channel (aggressive)...");
		var aggressiveChannel = HighPerformanceChannelExtensions.InstrumentedHighPerformanceChannel
			.CreateSingleProducerConsumer<TestMessage>(capacity);

		await RunPerformanceTest(aggressiveChannel, messageCount).ConfigureAwait(false);
		var aggressiveMetrics = ((InstrumentedChannel<TestMessage>)aggressiveChannel).GetMetrics();
		PrintMetrics("Aggressive HP Channel", aggressiveMetrics);

		// Compare results
		Console.WriteLine("\nPerformance Comparison:");
		Console.WriteLine(
			$"Standard ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ HP Default: {hpMetrics.MessagesPerSecond / (double)standardMetrics.MessagesPerSecond:P0} throughput");
		Console.WriteLine(
			$"Standard ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ HP Aggressive: {aggressiveMetrics.MessagesPerSecond / (double)standardMetrics.MessagesPerSecond:P0} throughput");
		Console.WriteLine($"Latency reduction: {1 - aggressiveMetrics.AverageLatencyMs / standardMetrics.AverageLatencyMs:P0}");
	}

	#region Helper Methods and Types

	private static async Task ProduceOrders(ChannelWriter<string> writer)
	{
		for (var i = 0; i < 100; i++)
		{
			await writer.WriteAsync($"Order-{i}").ConfigureAwait(false);
			await Task.Delay(10).ConfigureAwait(false);
		}

		_ = writer.TryComplete();
	}

	private static async Task ConsumeOrders(ChannelReader<string> reader)
	{
		await foreach (var order in reader.ReadAllAsync().ConfigureAwait(false))
		{
			// Process order
			await Task.Delay(5).ConfigureAwait(false);
		}
	}

	private static void ProcessMarketData(MarketData data)
	{
		// Simulate processing
		if (data.Price > 120)
		{
			// Trading signal
		}
	}

	private static async Task RunPerformanceTest<T>(Channel<T> channel, int messageCount) where T : new()
	{
		var sw = Stopwatch.StartNew();

		var producer = Task.Run(async () =>
		{
			for (var i = 0; i < messageCount; i++)
			{
				await channel.Writer.WriteAsync(new T()).ConfigureAwait(false);
			}

			_ = channel.Writer.TryComplete();
		});

		var consumer = Task.Run(async () =>
		{
			var count = 0;
			await foreach (var _ in channel.Reader.ReadAllAsync().ConfigureAwait(false))
			{
				count++;
			}

			return count;
		});

		await Task.WhenAll(producer, consumer).ConfigureAwait(false);
		sw.Stop();

		Console.WriteLine($" Processed {messageCount} messages in {sw.ElapsedMilliseconds}ms");
	}

	private static void PrintMetrics(string name, ChannelMetrics metrics)
	{
		Console.WriteLine($"{name} Metrics:");
		Console.WriteLine($" Throughput: {metrics.MessagesPerSecond:N0} msg/sec");
		Console.WriteLine($" Avg Latency: {metrics.AverageLatencyMs:F3} ms");
		Console.WriteLine($" P99 Latency: {metrics.P99LatencyMs:F3} ms");
	}

	private record MarketData
	{
		public required string Symbol { get; init; }
		public required double Price { get; init; }
		public required int Volume { get; init; }
	}

	private record RawData
	{
		public required int Id { get; init; }
		public required string Content { get; init; }
	}

	private record ProcessedData
	{
		public required int Id { get; init; }
		public required string ProcessedContent { get; init; }
	}

	private record FinalOutput
	{
		public required int Id { get; init; }
		public required string Result { get; init; }
	}

	private record TestMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public DateTime Timestamp { get; } = DateTime.UtcNow;
	}

	#endregion Helper Methods and Types

	/// <summary>
	/// Custom ETW event listener for channel performance monitoring.
	/// </summary>
	private class ChannelPerformanceListener : EventListener
	{
		private readonly Lock _lock = new();
		private readonly Dictionary<string, long> _channelMessages = [];
		private long _totalMessages;
		private long _droppedMessages;
		private double _totalLatency;
		private int _latencyCount;

		public void PrintStatistics()
		{
			lock (_lock)
			{
				Console.WriteLine("\n=== Channel Performance Statistics ===");
				Console.WriteLine($"Total Messages: {_totalMessages:N0}");
				Console.WriteLine($"Dropped Messages: {_droppedMessages:N0}");

				if (_latencyCount > 0)
				{
					Console.WriteLine($"Average Latency: {_totalLatency / _latencyCount:F3} ms");
				}

				Console.WriteLine("\nMessages by Channel:");
				foreach (var (channel, count) in _channelMessages.OrderByDescending(kvp => kvp.Value))
				{
					Console.WriteLine($" {channel}: {count:N0}");
				}
			}
		}

		protected override void OnEventSourceCreated(EventSource eventSource)
		{
			if (eventSource.Name == "Excalibur.Dispatch.Transport.Common")
			{
				EnableEvents(eventSource, EventLevel.Verbose,
					ChannelEventSource.Keywords.Performance | ChannelEventSource.Keywords.Read | ChannelEventSource.Keywords.Write);
			}
		}

		protected override void OnEventWritten(EventWrittenEventArgs eventData)
		{
			if (eventData.EventSource.Name != "Excalibur.Dispatch.Transport.Common")
			{
				return;
			}

			lock (_lock)
			{
				switch (eventData.EventId)
				{
					case 10: // MessageWritten
					case 12: // MessageRead
						_totalMessages++;
						var channelType = (string)(eventData.Payload?[0] ?? "Unknown");
						_channelMessages[channelType] = _channelMessages.GetValueOrDefault(channelType) + 1;
						break;

					case 14: // MessageDropped
						_droppedMessages++;
						break;

					case 21: // ChannelLatency
						var avgLatency = eventData.Payload?[1] is double d ? d : 0.0;
						_totalLatency += avgLatency;
						_latencyCount++;
						break;
				}
			}
		}
	}
}