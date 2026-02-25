// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Splits transport concurrency penalty into publish-only, receive-only, and combined paths.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class TransportConcurrencyBreakdownBenchmarks
{
	private ServiceProvider? _provider;
	private IDispatcher? _dispatcher;
	private InMemoryTransportAdapter? _adapter;
	private TransportConcurrencyCommand _command = null!;

	[Params(256, 4096)]
	public int PayloadSizeBytes { get; set; }

	[Params(1, 2, 4, 8, 16)]
	public int Concurrency { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBenchmarkDispatch();
		_ = services.AddTransient<IActionHandler<TransportConcurrencyCommand>, TransportConcurrencyCommandHandler>();

		_provider = services.BuildServiceProvider();
		_dispatcher = _provider.GetRequiredService<IDispatcher>();
		_adapter = new InMemoryTransportAdapter(
			NullLogger<InMemoryTransportAdapter>.Instance,
			new InMemoryTransportOptions { ChannelCapacity = 2048 });
		_adapter.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

		var payload = new string('x', PayloadSizeBytes);
		_command = new TransportConcurrencyCommand(payload);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_adapter?.ClearSentMessages();
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

	[Benchmark(Baseline = true, Description = "Transport publish path concurrent")]
	public async Task<int> PublishPathConcurrent()
	{
		var tasks = new Task[Concurrency];
		for (var i = 0; i < tasks.Length; i++)
		{
			tasks[i] = _adapter!.SendAsync(_command, "local", CancellationToken.None);
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
		return _adapter!.SentMessages.Count;
	}

	[Benchmark(Description = "Transport receive path concurrent")]
	public async Task<int> ReceivePathConcurrent()
	{
		var tasks = new Task<IMessageResult>[Concurrency];
		for (var i = 0; i < tasks.Length; i++)
		{
			tasks[i] = _adapter!.ReceiveAsync(_command, _dispatcher!, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);
		return results.Count(static result => result.Succeeded);
	}

	[Benchmark(Description = "Transport publish+receive combined concurrent")]
	public async Task<int> PublishReceiveCombinedConcurrent()
	{
		var tasks = new Task<IMessageResult>[Concurrency];
		for (var i = 0; i < tasks.Length; i++)
		{
			tasks[i] = PublishThenReceiveAsync();
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);
		return results.Count(static result => result.Succeeded);
	}

	private async Task<IMessageResult> PublishThenReceiveAsync()
	{
		await _adapter!.SendAsync(_command, "local", CancellationToken.None).ConfigureAwait(false);
		return await _adapter.ReceiveAsync(_command, _dispatcher!, CancellationToken.None).ConfigureAwait(false);
	}

	private sealed record TransportConcurrencyCommand(string Payload) : IDispatchAction;

	private sealed class TransportConcurrencyCommandHandler : IActionHandler<TransportConcurrencyCommand>
	{
		public Task HandleAsync(TransportConcurrencyCommand action, CancellationToken cancellationToken)
		{
			_ = action.Payload.Length;
			return Task.CompletedTask;
		}
	}
}
