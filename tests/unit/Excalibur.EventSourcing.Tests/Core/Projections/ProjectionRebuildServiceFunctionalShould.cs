// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// Functional tests for <see cref="ProjectionRebuildService"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProjectionRebuildServiceFunctionalShould
{
	private sealed class TestProjection
	{
		public int EventCount { get; set; }
	}

	[Fact]
	public async Task RebuildAsync_WithNoGlobalQuery_ShouldSetStatusToFailed()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(null);

		var sut = new ProjectionRebuildService(
			serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Failed);
	}

	[Fact]
	public async Task RebuildAsync_WithNoProjection_ShouldSetStatusToFailed()
	{
		// Arrange
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var serviceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(globalQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<TestProjection>)))
			.Returns(null);

		var sut = new ProjectionRebuildService(
			serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Failed);
	}

	[Fact]
	public async Task RebuildAsync_WithEmptyGlobalStream_ShouldCompleteSuccessfully()
	{
		// Arrange
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<TestProjection>();
		var serviceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery)))
			.Returns(globalQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<TestProjection>)))
			.Returns(projection);
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent>());

		var sut = new ProjectionRebuildService(
			serviceProvider,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions { BatchSize = 100 }),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
		status.Progress.ShouldBe(100);
	}

	[Fact]
	public async Task GetStatusAsync_WithNoRebuilds_ShouldReturnIdle()
	{
		// Arrange
		var sut = new ProjectionRebuildService(
			A.Fake<IServiceProvider>(),
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		var status = await sut.GetStatusAsync(CancellationToken.None);

		// Assert
		status.State.ShouldBe(ProjectionRebuildState.Idle);
		status.ProjectionName.ShouldBe("None");
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullServiceProvider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRebuildService(
				null!,
				Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
				NullLogger<ProjectionRebuildService>.Instance));
	}
}
