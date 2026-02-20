// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProjectionRebuildServiceShould
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly ProjectionRebuildOptions _options = new();
	private readonly ProjectionRebuildService _sut;

	public ProjectionRebuildServiceShould()
	{
		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
		_sut = new ProjectionRebuildService(
			_serviceProvider,
			optionsWrapper,
			NullLogger<ProjectionRebuildService>.Instance);
	}

	[Fact]
	public async Task ReturnIdleStatusWhenNoRebuildHasRun()
	{
		// Act
		var status = await _sut.GetStatusAsync(CancellationToken.None);

		// Assert
		status.ShouldNotBeNull();
		status.State.ShouldBe(ProjectionRebuildState.Idle);
		status.ProjectionName.ShouldBe("None");
		status.Progress.ShouldBe(0);
	}

	[Fact]
	public async Task FailWhenNoGlobalStreamQueryIsRegistered()
	{
		// Arrange
		A.CallTo(() => _serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(null);

		// Act
		await _sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None);

		// Assert
		var status = await _sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Failed);
		status.ProjectionName.ShouldBe("ProjectionRebuildTestState");
	}

	[Fact]
	public async Task FailWhenNoMultiStreamProjectionIsRegistered()
	{
		// Arrange
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => _serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(globalQuery);
		A.CallTo(() => _serviceProvider.GetService(typeof(MultiStreamProjection<ProjectionRebuildTestState>)))
			.Returns(null);

		// Act
		await _sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None);

		// Assert
		var status = await _sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Failed);
	}

	[Fact]
	public async Task CompleteWhenNoEventsExist()
	{
		// Arrange
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<ProjectionRebuildTestState>();
		A.CallTo(() => _serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(globalQuery);
		A.CallTo(() => _serviceProvider.GetService(typeof(MultiStreamProjection<ProjectionRebuildTestState>)))
			.Returns(projection);
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));

		// Act
		await _sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None);

		// Assert
		var status = await _sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
		status.Progress.ShouldBe(100);
	}

	[Fact]
	public async Task ProcessEventsInBatches()
	{
		// Arrange
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<ProjectionRebuildTestState>();
		A.CallTo(() => _serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(globalQuery);
		A.CallTo(() => _serviceProvider.GetService(typeof(MultiStreamProjection<ProjectionRebuildTestState>)))
			.Returns(projection);

		IReadOnlyList<StoredEvent> batch1 =
		[
			new StoredEvent("evt-1", "agg-1", "TestAggregate", "TestEvent", "data1"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false),
			new StoredEvent("evt-2", "agg-1", "TestAggregate", "TestEvent", "data2"u8.ToArray(), null, 1, DateTimeOffset.UtcNow, false),
		];

		var callCount = 0;
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((_) =>
			{
				callCount++;
				return callCount == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(batch1)
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		// Act
		await _sut.RebuildAsync<ProjectionRebuildTestState>(CancellationToken.None);

		// Assert
		var status = await _sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
		status.Progress.ShouldBe(100);
		status.LastRebuiltAt.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenServiceProviderIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ProjectionRebuildService(
			null!,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ProjectionRebuildService(
			_serviceProvider,
			null!,
			NullLogger<ProjectionRebuildService>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new ProjectionRebuildService(
			_serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			null!));
	}
}

internal sealed class ProjectionRebuildTestState
{
	public int Count { get; set; }
}

#pragma warning restore CA2012
