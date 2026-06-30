// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Versioning;
using Excalibur.EventSourcing;
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
[Trait("Component", "EventSourcing")]
public sealed class ProjectionRebuildServiceFunctionalShould
{
	private sealed class TestProjection
	{
		public int EventCount { get; set; }
	}

	private sealed record UpcastTestEventV1 : DomainEvent, IVersionedMessage
	{
		int IVersionedMessage.Version => 1;
		string IVersionedMessage.MessageType => "UpcastTestEvent";
	}

	[Fact]
	public async Task RebuildAsync_AppliesUpcastingPipeline_WhenAutoUpcastOnReplayEnabled()
	{
		// 4o3wzt: a rebuild must upcast events before applying them (as the write side does), or the read model
		// diverges from the write model when an event schema evolves. Pre-fix the pipeline was never invoked
		// during rebuild — this asserts it is, so the assertion is RED on the pre-fix code.
		var globalQuery = A.Fake<IGlobalStreamQuery>();
		var projection = new MultiStreamProjection<TestProjection>();
		var serializer = A.Fake<IEventSerializer>();
		var pipeline = A.Fake<IUpcastingPipeline>();
		var serviceProvider = A.Fake<IServiceProvider>();

		var v1 = new UpcastTestEventV1();
		var stored = new StoredEvent("e1", "agg-1", "Agg", "UpcastTestEvent", [0x01], null, 1, DateTimeOffset.UtcNow);

		A.CallTo(() => serviceProvider.GetService(typeof(IGlobalStreamQuery))).Returns(globalQuery);
		A.CallTo(() => serviceProvider.GetService(typeof(MultiStreamProjection<TestProjection>))).Returns(projection);
		A.CallTo(() => serviceProvider.GetService(typeof(IUpcastingPipeline))).Returns(pipeline);
		A.CallTo(() => serviceProvider.GetService(typeof(Microsoft.Extensions.Options.IOptions<UpcastingOptions>)))
			.Returns(Microsoft.Extensions.Options.Options.Create(new UpcastingOptions { EnableAutoUpcastOnReplay = true }));
		A.CallTo(() => serializer.ResolveType("UpcastTestEvent")).Returns(typeof(UpcastTestEventV1));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, typeof(UpcastTestEventV1))).Returns(v1);
		A.CallTo(() => pipeline.Upcast(v1)).Returns(v1);
		A.CallTo(() => globalQuery.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				new List<StoredEvent> { stored },
				new List<StoredEvent>());

		var sut = new ProjectionRebuildService(
			serviceProvider,
			serializer,
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions { BatchSize = 100 }),
			NullLogger<ProjectionRebuildService>.Instance);

		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		A.CallTo(() => pipeline.Upcast(v1)).MustHaveHappenedOnceExactly();
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
			A.Fake<IEventSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync<TestProjection>(CancellationToken.None);
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
			A.Fake<IEventSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync<TestProjection>(CancellationToken.None);
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
			A.Fake<IEventSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions { BatchSize = 100 }),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		await sut.RebuildAsync<TestProjection>(CancellationToken.None);

		// Assert
		var status = await sut.GetStatusAsync<TestProjection>(CancellationToken.None);
		status.State.ShouldBe(ProjectionRebuildState.Completed);
		status.Progress.ShouldBe(100);
	}

	[Fact]
	public async Task GetStatusAsync_WithNoRebuilds_ShouldReturnIdle()
	{
		// Arrange
		var sut = new ProjectionRebuildService(
			A.Fake<IServiceProvider>(),
			A.Fake<IEventSerializer>(),
			Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
			NullLogger<ProjectionRebuildService>.Instance);

		// Act
		var status = await sut.GetStatusAsync<TestProjection>(CancellationToken.None);

		// Assert
		status.State.ShouldBe(ProjectionRebuildState.Idle);
		status.ProjectionName.ShouldBe("TestProjection");
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullServiceProvider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRebuildService(
				null!,
				A.Fake<IEventSerializer>(),
				Microsoft.Extensions.Options.Options.Create(new ProjectionRebuildOptions()),
				NullLogger<ProjectionRebuildService>.Instance));
	}
}
