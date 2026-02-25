// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

public sealed class GlobalStreamTestState
{
	public int Count { get; set; }
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GlobalStreamProjectionHostShould
{
	private readonly IGlobalStreamQuery _globalStreamQuery = A.Fake<IGlobalStreamQuery>();
	private readonly IGlobalStreamProjection<GlobalStreamTestState> _projection = A.Fake<IGlobalStreamProjection<GlobalStreamTestState>>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();

	[Fact]
	public void ThrowWhenGlobalStreamQueryIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new GlobalStreamProjectionHost<GlobalStreamTestState>(
			null!,
			_projection,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions()),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance));
	}

	[Fact]
	public void ThrowWhenProjectionIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			null!,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions()),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance));
	}

	[Fact]
	public void ThrowWhenEventSerializerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			null!,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions()),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			_eventSerializer,
			null!,
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions()),
			null!));
	}

	[Fact]
	public async Task StopGracefullyWhenCancelled()
	{
		// Arrange
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		var host = new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
			}),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance);

		using var cts = new CancellationTokenSource();

		// Act
		await host.StartAsync(cts.Token);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert - no exception thrown
	}

	[Fact]
	public async Task ProcessEventsFromGlobalStream()
	{
		// Arrange
		var storedEvent = new StoredEvent("evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false);
		var domainEvent = A.Fake<IDomainEvent>();
		var applyObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var callCount = 0;
		A.CallTo(() => _globalStreamQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				callCount++;
				return callCount == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);
		A.CallTo(() => _projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.ReturnsLazily((_call) =>
			{
				applyObserved.TrySetResult();
				return Task.CompletedTask;
			});

		var host = new GlobalStreamProjectionHost<GlobalStreamTestState>(
			_globalStreamQuery,
			_projection,
			_eventSerializer,
			Microsoft.Extensions.Options.Options.Create(new GlobalStreamProjectionOptions
			{
				IdlePollingInterval = TimeSpan.FromMilliseconds(10),
			}),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance);

		using var cts = new CancellationTokenSource();

		// Act
		await host.StartAsync(cts.Token);
		await AwaitApplyObservedAsync(applyObserved.Task);
		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	private static Task AwaitApplyObservedAsync(Task signal)
	{
		return global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			signal,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)),
			cancellationToken: CancellationToken.None);
	}
}

#pragma warning restore CA2012
