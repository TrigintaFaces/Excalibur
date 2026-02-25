// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for MessageContext creation and operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class MessageContextBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private TestMessage _message = null!;
	private MessageContext _existingContext = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();

		_message = new TestMessage { Id = Guid.NewGuid(), Data = "BenchmarkData" };
		_existingContext = new MessageContext(_message, _serviceProvider);

		// Pre-populate with items for lookup benchmarks
		_existingContext.Items["Key1"] = "Value1";
		_existingContext.Items["Key2"] = 42;
		_existingContext.Items["Key3"] = new TestPayload { Name = "Test" };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	[Benchmark(Baseline = true)]
	public MessageContext CreateContext()
	{
		return new MessageContext(_message, _serviceProvider);
	}

	[Benchmark]
	public object? ItemsLookup_StringKey()
	{
		return _existingContext.Items["Key1"];
	}

	[Benchmark]
	public object? ItemsLookup_MissingKey()
	{
		return _existingContext.Items.TryGetValue("MissingKey", out var value) ? value : null;
	}

	[Benchmark]
	public void ItemsAdd_StringValue()
	{
		var ctx = new MessageContext(_message, _serviceProvider);
		ctx.Items["NewKey"] = "NewValue";
	}

	[Benchmark]
	public void ItemsAdd_MultiplePairs()
	{
		var ctx = new MessageContext(_message, _serviceProvider);
		ctx.Items["Key1"] = "Value1";
		ctx.Items["Key2"] = "Value2";
		ctx.Items["Key3"] = "Value3";
		ctx.Items["Key4"] = "Value4";
		ctx.Items["Key5"] = "Value5";
	}

	[Benchmark]
	public bool ItemsContainsKey()
	{
		return _existingContext.Items.ContainsKey("Key1");
	}

	[Benchmark]
	public int ItemsCount()
	{
		return _existingContext.Items.Count;
	}

	private sealed record TestMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	private sealed class TestPayload
	{
		public string Name { get; init; } = string.Empty;
	}
}
