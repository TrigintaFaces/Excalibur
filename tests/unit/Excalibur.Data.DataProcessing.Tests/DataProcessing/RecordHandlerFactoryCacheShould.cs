// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests verifying that <c>AddRecordHandler</c> correctly registers
/// <see cref="IRecordHandler{TRecord}"/> with the DI container.
/// </summary>
/// <remarks>
/// The static <c>RecordHandlerFactories</c> cache was removed in favor of
/// DI-only resolution. These tests now verify DI resolution instead.
/// </remarks>
[UnitTest]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class RecordHandlerFactoryCacheShould : UnitTestBase
{
	private sealed record RecordAlpha;

	private sealed record RecordBeta;

	private sealed record RecordGamma;

	private sealed record RecordDelta;

	private sealed class AlphaHandler : IRecordHandler<RecordAlpha>
	{
		public Task ProcessAsync(RecordAlpha record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class BetaHandler : IRecordHandler<RecordBeta>
	{
		public Task ProcessAsync(RecordBeta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class GammaHandler : IRecordHandler<RecordGamma>
	{
		public Task ProcessAsync(RecordGamma record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class DeltaHandler : IRecordHandler<RecordDelta>
	{
		public Task ProcessAsync(RecordDelta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	[Fact]
	public void RegisterHandlerViaDI_WhenParameterlessOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRecordHandler<AlphaHandler, RecordAlpha>();
		using var provider = services.BuildServiceProvider();

		// Assert
		var handler = provider.GetService<IRecordHandler<RecordAlpha>>();
		handler.ShouldNotBeNull();
		handler.ShouldBeOfType<AlphaHandler>();
	}

	[Fact]
	public void RegisterHandlerViaDI_WhenConfigOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingOptions { QueueSize = 64 };

		// Act
		services.AddRecordHandler<BetaHandler, RecordBeta>(config);
		using var provider = services.BuildServiceProvider();

		// Assert
		var handler = provider.GetService<IRecordHandler<RecordBeta>>();
		handler.ShouldNotBeNull();
		handler.ShouldBeOfType<BetaHandler>();
	}

	[Fact]
	public void RegisterIndependentHandlersForMultipleRecordTypes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRecordHandler<GammaHandler, RecordGamma>();
		services.AddRecordHandler<DeltaHandler, RecordDelta>();
		using var provider = services.BuildServiceProvider();

		// Assert
		provider.GetService<IRecordHandler<RecordGamma>>().ShouldNotBeNull();
		provider.GetService<IRecordHandler<RecordDelta>>().ShouldNotBeNull();
	}
}