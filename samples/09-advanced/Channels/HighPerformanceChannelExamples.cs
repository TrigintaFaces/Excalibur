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
using System.Threading.Channels;

namespace examples.Channels;

/// <summary>
/// Examples demonstrating how to use HighPerformanceChannel for various scenarios.
/// </summary>
public static class HighPerformanceChannelExamples
{
	/// <summary>
	/// Example: High-frequency trading message processor.
	/// </summary>
	public static async Task HighFrequencyTradingExample()
	{
		// Create an aggressive channel for ultra-low latency
		var channel = HighPerformanceChannel.CreateSingleProducerConsumer<MarketData>(1000);

		// Producer - Market data feed
		var producerTask = Task.Run(async () =>
		{
			var random = new Random();
			var symbols = new[] { "AAPL", "GOOGL", "MSFT", "AMZN" };

			for (var i = 0; i < 100000; i++)
			{
				var data = new MarketData
				{
					Symbol = symbols[random.Next(symbols.Length)],
					Price = 100 + (random.Next(0, 256) / 255.0 * 50),
					Volume = random.Next(1000, 10000),
					Timestamp = DateTime.UtcNow
				};

				// Spin until we can write - no blocking
				while (!channel.Writer.TryWrite(data))
				{
					await Task.Yield();
				}
			}

			_ = channel.Writer.TryComplete();
		});

		// Consumer - Trading strategy
		var consumerTask = Task.Run(async () =>
		{
			var processedCount = 0;
			var stopwatch = Stopwatch.StartNew();

			await foreach (var data in channel.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false)
							 .ConfigureAwait(false).ConfigureAwait(false))
			{
				// Process market data with minimal latency
				ProcessMarketData(data);
				processedCount++;
			}

			stopwatch.Stop();
			Console.WriteLine($"Processed {processedCount} market data items in {stopwatch.ElapsedMilliseconds}ms");
			Console.WriteLine($"Average latency: {stopwatch.ElapsedMilliseconds * 1000.0 / (double)processedCount:F2} microseconds");
		});

		await Task.WhenAll(producerTask, consumerTask);
	}

	/// <summary>
	/// Example: Load balancing with drop-oldest strategy.
	/// </summary>
	public static async Task LoadBalancingExample()
	{
		// Create channel that drops oldest requests when overloaded
		var requestChannel = HighPerformanceChannel.CreateBounded<WebRequest>(new HighPerformanceBoundedChannelOptions(50)
		{
			FullMode = BoundedChannelFullMode.DropOldest
		});

		// Simulate incoming requests
		var requestGenerator = Task.Run(async () =>
		{
			var requestId = 0;
			while (requestId < 1000)
			{
				var request = new WebRequest { Id = requestId++, Url = $"/api/data/{requestId}", ReceivedAt = DateTime.UtcNow };

				if (requestChannel.Writer.TryWrite(request))
				{
					// Request queued
				}
				else
				{
					Console.WriteLine($"System overloaded - dropped request {request.Id}");
				}

				// Simulate variable request rate
				await Task.Delay(new Random().Next(1, 10)).ConfigureAwait(false);
			}

			_ = requestChannel.Writer.TryComplete();
		});

		// Worker pool
		var workers = Enumerable.Range(0, 4).Select(workerId => Task.Run(async () =>
		{
			var processed = 0;

			await foreach (var request in requestChannel.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false)
							 .ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false))
			{
				// Simulate request processing
				await ProcessWebRequest(request, workerId).ConfigureAwait(false);
				processed++;
			}

			Console.WriteLine($"Worker {workerId} processed {processed} requests");
		})).ToArray();

		await requestGenerator.ConfigureAwait(false);
		await Task.WhenAll(workers).ConfigureAwait(false);
	}

	/// <summary>
	/// Example: Real-time sensor data processing with custom spin options.
	/// </summary>
	public static async Task SensorDataProcessingExample()
	{
		// Configure custom spin-wait behavior for sensor data
		var channelOptions = new BoundedChannelOptions(100)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleWriter = true,
			SingleReader = false
		};

		var spinOptions = new SpinWaitOptions
		{
			SpinIterations = 200, // Spin more before yielding
			MaxSpinCycles = 20, // Try harder before blocking
			AggressiveSpin = false // Still yield to other threads
		};

		var sensorChannel = HighPerformanceChannel.CreateCustom<SensorReading>(channelOptions, spinOptions);

		// Sensor data producer
		var sensorTask = Task.Run(async () =>
		{
			var sensorId = "TEMP-001";
			var reading = 20.0;

			for (var i = 0; i < 10000; i++)
			{
				// Simulate sensor readings
				reading += ((Random.Shared.Next(0, 256) / 255.0) - 0.5) * 0.1;

				var data = new SensorReading { SensorId = sensorId, Value = reading, Timestamp = DateTime.UtcNow, Unit = "Ã‚Â°C" };

				await sensorChannel.Writer.WriteAsync(data).ConfigureAwait(false);

				// 100Hz sensor rate
				await Task.Delay(10).ConfigureAwait(false);
			}

			_ = sensorChannel.Writer.TryComplete();
		});

		// Multiple consumers for redundancy
		var consumers = new[] { ProcessSensorStream(sensorChannel.Reader, "Primary"), ProcessSensorStream(sensorChannel.Reader, "Backup") };

		await sensorTask.ConfigureAwait(false);
		await Task.WhenAll(consumers).ConfigureAwait(false);
	}

	/// <summary>
	/// Example: Batch processing with channel.
	/// </summary>
	public static async Task BatchProcessingExample()
	{
		var batchChannel = HighPerformanceChannel.CreateBounded<LogEntry>(new HighPerformanceBoundedChannelOptions(1000)
		{
			FullMode = BoundedChannelFullMode.DropNewest
		});

		// Log entry producer
		var logProducer = Task.Run(async () =>
		{
			for (var i = 0; i < 50000; i++)
			{
				var entry = new LogEntry
				{
					Level = i % 100 == 0 ? "ERROR" : "INFO",
					Message = $"Log message {i}",
					Timestamp = DateTime.UtcNow
				};

				// Try to write, drop if channel is full (DropNewest mode)
				_ = batchChannel.Writer.TryWrite(entry);

				if (i % 1000 == 0)
				{
					await Task.Delay(1).ConfigureAwait(false); // Occasional delay
				}
			}

			_ = batchChannel.Writer.TryComplete();
		});

		// Batch consumer
		var batchConsumer = Task.Run(async () =>
		{
			var batch = new List<LogEntry>(100);
			var totalProcessed = 0;

			while (await batchChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
			{
				// Collect batch
				while (batch.Count < 100 && batchChannel.Reader.TryRead(out var entry))
				{
					batch.Add(entry);
				}

				if (batch.Count > 0)
				{
					// Process batch
					await ProcessLogBatch(batch).ConfigureAwait(false);
					totalProcessed += batch.Count;
					batch.Clear();
				}
			}

			// Process remaining
			if (batch.Count > 0)
			{
				await ProcessLogBatch(batch).ConfigureAwait(false);
				totalProcessed += batch.Count;
			}

			Console.WriteLine($"Processed {totalProcessed} log entries in batches");
		});

		await Task.WhenAll(logProducer, batchConsumer).ConfigureAwait(false);
	}

	/// <summary>
	/// Example: Pipeline with multiple stages using channels.
	/// </summary>
	public static async Task PipelineExample()
	{
		// Create pipeline stages with different performance characteristics
		var stage1 = HighPerformanceChannel.CreateBounded<RawData>(100);
		var stage2 = HighPerformanceChannel.CreateBounded<ParsedData>(50);
		var stage3 = HighPerformanceChannel.CreateSingleProducerConsumer<ProcessedData>(25);

		// Stage 1: Data ingestion
		var ingestionTask = Task.Run(async () =>
		{
			for (var i = 0; i < 1000; i++)
			{
				var raw = new RawData { Id = i, Content = $"Raw content {i}" };
				await stage1.Writer.WriteAsync(raw).ConfigureAwait(false);
			}

			_ = stage1.Writer.TryComplete();
		});

		// Stage 2: Parsing
		var parsingTask = Task.Run(async () =>
		{
			await foreach (var raw in stage1.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false)
							 .ConfigureAwait(false).ConfigureAwait(false))
			{
				var parsed = new ParsedData { Id = raw.Id, Fields = raw.Content.Split(' ') };

				while (!stage2.Writer.TryWrite(parsed))
				{
					await Task.Yield();
				}
			}

			_ = stage2.Writer.TryComplete();
		});

		// Stage 3: Processing
		var processingTask = Task.Run(async () =>
		{
			await foreach (var parsed in stage2.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false)
							 .ConfigureAwait(false).ConfigureAwait(false))
			{
				var processed = new ProcessedData { Id = parsed.Id, Result = string.Join("-", parsed.Fields) };

				while (!stage3.Writer.TryWrite(processed))
				{
					Thread.SpinWait(10);
				}
			}

			_ = stage3.Writer.TryComplete();
		});

		// Stage 4: Output
		var outputTask = Task.Run(async () =>
		{
			var count = 0;
			await foreach (var processed in stage3.Reader.ReadAllAsync().ConfigureAwait(false).ConfigureAwait(false).ConfigureAwait(false)
							 .ConfigureAwait(false).ConfigureAwait(false))
			{
				// Final output
				count++;
			}

			Console.WriteLine($"Pipeline processed {count} items");
		});

		await Task.WhenAll(ingestionTask, parsingTask, processingTask, outputTask).ConfigureAwait(false);
	}

	#region Helper Methods and Types

	private record MarketData
	{
		public required string Symbol { get; init; }
		public required double Price { get; init; }
		public required int Volume { get; init; }
		public required DateTime Timestamp { get; init; }
	}

	private record WebRequest
	{
		public required int Id { get; init; }
		public required string Url { get; init; }
		public required DateTime ReceivedAt { get; init; }
	}

	private record SensorReading
	{
		public required string SensorId { get; init; }
		public required double Value { get; init; }
		public required DateTime Timestamp { get; init; }
		public required string Unit { get; init; }
	}

	private record LogEntry
	{
		public required string Level { get; init; }
		public required string Message { get; init; }
		public required DateTime Timestamp { get; init; }
	}

	private record RawData
	{
		public required int Id { get; init; }
		public required string Content { get; init; }
	}

	private record ParsedData
	{
		public required int Id { get; init; }
		public required string[] Fields { get; init; }
	}

	private record ProcessedData
	{
		public required int Id { get; init; }
		public required string Result { get; init; }
	}

	private static void ProcessMarketData(MarketData data)
	{
		// Simulate ultra-fast processing
		if (data is { Price: > 120, Volume: > 5000 })
		{
			// Trading signal
		}
	}

	private static async Task ProcessWebRequest(WebRequest request, int workerId)
	{
		// Simulate request processing
		await Task.Delay(new Random().Next(10, 50)).ConfigureAwait(false);
		var latency = (DateTime.UtcNow - request.ReceivedAt).TotalMilliseconds;
		if (latency > 100)
		{
			Console.WriteLine($"Worker {workerId}: High latency {latency:F2}ms for request {request.Id}");
		}
	}

	private static async Task ProcessSensorStream(ChannelReader<SensorReading> reader, string consumerName)
	{
		var count = 0;
		var sum = 0.0;

		await foreach (var reading in reader.ReadAllAsync().ConfigureAwait(false))
		{
			count++;
			sum += reading.Value;

			if (count % 1000 == 0)
			{
				var avg = sum / count;
				Console.WriteLine($"{consumerName}: Processed {count} readings, average: {avg:F2}{reading.Unit}");
			}
		}
	}

	private static async Task ProcessLogBatch(List<LogEntry> batch)
	{
		// Simulate batch processing
		await Task.Delay(5).ConfigureAwait(false);
		var errors = batch.Count(e => e.Level == "ERROR");
		if (errors > 0)
		{
			Console.WriteLine($"Batch of {batch.Count} logs contains {errors} errors");
		}
	}

	#endregion Helper Methods and Types
}
