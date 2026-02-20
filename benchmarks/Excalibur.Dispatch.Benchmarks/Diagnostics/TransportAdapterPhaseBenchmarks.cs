// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures mapping, serialization, publish, and acknowledge-style phases for transport paths.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class TransportAdapterPhaseBenchmarks
{
	private ServiceProvider? _provider;
	private IDispatcher? _dispatcher;
	private InMemoryTransportAdapter? _adapter;
	private IMessageMapper? _mapper;
	private IUtf8JsonSerializer? _serializer;
	private ITransportMessageContext? _sourceContext;
	private TransportBenchmarkCommand _command = null!;
	private Dictionary<string, string> _serializationPayload = null!;
	private readonly ConcurrentDictionary<string, byte> _inflight = new(StringComparer.Ordinal);
	private int _ackSequence;
	private int _serializationChecksum;

	[Params(256, 4096, 65536)]
	public int PayloadSizeBytes { get; set; }

	[Params(1, 8)]
	public int Concurrency { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBenchmarkDispatch();
		_ = services.AddTransient<IActionHandler<TransportBenchmarkCommand>, TransportBenchmarkCommandHandler>();

		_provider = services.BuildServiceProvider();
		_dispatcher = _provider.GetRequiredService<IDispatcher>();
		_mapper = new DefaultMessageMapper("benchmark-default", sourceTransport: "inmemory", targetTransport: "kafka");
		_serializer = new DispatchJsonSerializer();
		_adapter = new InMemoryTransportAdapter(
			NullLogger<InMemoryTransportAdapter>.Instance,
			new InMemoryTransportOptions { ChannelCapacity = 1024 });
		_adapter.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

		var payload = new string('x', PayloadSizeBytes);
		_command = new TransportBenchmarkCommand(payload);
		_serializationPayload = new Dictionary<string, string>(capacity: 4, comparer: StringComparer.Ordinal)
		{
			["messageId"] = Guid.NewGuid().ToString("N"),
			["messageType"] = nameof(TransportBenchmarkCommand),
			["contentType"] = "application/json",
			["payload"] = payload,
		};
		_sourceContext = new TransportMessageContext(Guid.NewGuid().ToString("N"))
		{
			SourceTransport = "inmemory",
			TargetTransport = "kafka",
			ContentType = "application/json",
			CorrelationId = Guid.NewGuid().ToString("N"),
			CausationId = Guid.NewGuid().ToString("N"),
		};
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_adapter is not null)
		{
			_adapter.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
			_adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
		}

		_provider?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Map transport context")]
	public ITransportMessageContext MapTransportContext()
	{
		return _mapper!.Map(_sourceContext!, "kafka");
	}

	[Benchmark(Description = "Serialize payload")]
	public int SerializePayload()
	{
		var payload = _serializer!.SerializeToUtf8Bytes(_serializationPayload, typeof(Dictionary<string, string>));
		var checksum = 17;
		for (var i = 0; i < payload.Length; i += Math.Max(1, payload.Length / 16))
		{
			checksum = unchecked((checksum * 31) + payload[i]);
		}

		_serializationChecksum = checksum;
		return _serializationChecksum;
	}

	[Benchmark(Description = "Publish to in-memory adapter")]
	public async Task<int> PublishToInMemoryAdapter()
	{
		await _adapter!.SendAsync(_command, "local", CancellationToken.None).ConfigureAwait(false);
		return _adapter.SentMessages.Count;
	}

	[Benchmark(Description = "Receive + dispatch")]
	public async Task<IMessageResult> ReceiveAndDispatch()
	{
		return await _adapter!.ReceiveAsync(_command, _dispatcher!, CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Publish+receive concurrent")]
	public async Task<int> PublishAndReceiveConcurrent()
	{
		var tasks = new Task<IMessageResult>[Concurrency];
		for (var i = 0; i < Concurrency; i++)
		{
			tasks[i] = _adapter!.ReceiveAsync(_command, _dispatcher!, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);
		return results.Count(static r => r.Succeeded);
	}

	[Benchmark(Description = "Ack tracking (in-memory)")]
	public bool AcknowledgeTracking()
	{
		var key = Interlocked.Increment(ref _ackSequence).ToString();
		_inflight[key] = 0;
		return _inflight.TryRemove(key, out _);
	}

	private sealed record TransportBenchmarkCommand(string Payload) : IDispatchAction;

	private sealed class TransportBenchmarkCommandHandler : IActionHandler<TransportBenchmarkCommand>
	{
		public Task HandleAsync(TransportBenchmarkCommand action, CancellationToken cancellationToken)
		{
			_ = action.Payload.Length;
			return Task.CompletedTask;
		}
	}
}
