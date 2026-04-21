// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Data.Tests.DataProcessing;

/// <summary>
/// Unit tests for the <see cref="DataProcessingServiceCollectionExtensions.RecordHandlerFactories"/>
/// delegate-capture factory cache populated by the <c>AddRecordHandler</c> overloads.
/// </summary>
[UnitTest]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RecordHandlerFactoryCacheShould : UnitTestBase
{
	// Each test uses a unique record type to avoid cross-test contamination
	// because RecordHandlerFactories is a static ConcurrentDictionary.

	private sealed record RecordAlpha;

	private sealed record RecordBeta;

	private sealed record RecordGamma;

	private sealed record RecordDelta;

	private sealed record RecordEpsilon;

	private sealed record RecordZeta;

	private sealed record RecordEta;

	private sealed record RecordTheta;

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

	private sealed class EpsilonHandler : IRecordHandler<RecordEpsilon>
	{
		public Task ProcessAsync(RecordEpsilon record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class ZetaHandler : IRecordHandler<RecordZeta>
	{
		public Task ProcessAsync(RecordZeta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class AlternateAlphaHandler : IRecordHandler<RecordEta>
	{
		public Task ProcessAsync(RecordEta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class SecondEtaHandler : IRecordHandler<RecordEta>
	{
		public Task ProcessAsync(RecordEta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class ThetaHandler : IRecordHandler<RecordTheta>
	{
		public Task ProcessAsync(RecordTheta record, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	[Fact]
	public void PopulateFactoryCacheForRecordType_WhenParameterlessOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRecordHandler<AlphaHandler, RecordAlpha>();

		// Assert
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ContainsKey(typeof(RecordAlpha))
			.ShouldBeTrue("AddRecordHandler<THandler, TRecord>() should add a factory entry for the record type");
	}

	[Fact]
	public void ResolveCachedFactoryToCorrectHandlerType()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddScoped<BetaHandler>();
		services.AddRecordHandler<BetaHandler, RecordBeta>();
		using var provider = services.BuildServiceProvider();

		// Act
		var hasFactory = DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.TryGetValue(typeof(RecordBeta), out var factory);

		// Assert
		hasFactory.ShouldBeTrue();
		var handler = factory!(provider);
		handler.ShouldBeOfType<BetaHandler>();
	}

	[Fact]
	public void PopulateIndependentEntriesForMultipleRecordTypes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRecordHandler<GammaHandler, RecordGamma>();
		services.AddRecordHandler<DeltaHandler, RecordDelta>();

		// Assert
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ContainsKey(typeof(RecordGamma))
			.ShouldBeTrue();
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ContainsKey(typeof(RecordDelta))
			.ShouldBeTrue();
	}

	[Fact]
	public void PopulateFactoryCacheForRecordType_WhenConfigOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingOptions { QueueSize = 64 };

		// Act
		services.AddRecordHandler<EpsilonHandler, RecordEpsilon>(config);

		// Assert
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ContainsKey(typeof(RecordEpsilon))
			.ShouldBeTrue("AddRecordHandler with DataProcessingOptions overload should populate the cache");
	}

	[Fact]
	public void PopulateFactoryCacheForRecordType_WhenIConfigurationOverloadUsed()
	{
		// Arrange
		var services = new ServiceCollection();
		var section = A.Fake<IConfigurationSection>();
		var configuration = A.Fake<IConfiguration>();
		A.CallTo(() => configuration.GetSection(A<string>._)).Returns(section);

		// Act
		services.AddRecordHandler<ZetaHandler, RecordZeta>(configuration, "DataProcessing");

		// Assert
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ContainsKey(typeof(RecordZeta))
			.ShouldBeTrue("AddRecordHandler with IConfiguration overload should populate the cache");
	}

	[Fact]
	public void PreserveFirstRegistration_WhenTryAddCalledTwice()
	{
		// Arrange -- TryAdd means first registration wins
		var services = new ServiceCollection();
		services.AddScoped<AlternateAlphaHandler>();
		services.AddScoped<SecondEtaHandler>();

		// Act -- register two different handlers for the same record type
		services.AddRecordHandler<AlternateAlphaHandler, RecordEta>();
		services.AddRecordHandler<SecondEtaHandler, RecordEta>();

		// Assert -- factory should resolve the first handler, not the second
		using var provider = services.BuildServiceProvider();
		var factory = DataProcessingServiceCollectionExtensions.RecordHandlerFactories[typeof(RecordEta)];
		var handler = factory(provider);
		handler.ShouldBeOfType<AlternateAlphaHandler>(
			"TryAdd semantics mean the first AddRecordHandler registration wins");
	}

	[Fact]
	public void ResolveCachedFactoryFromConfigOverload()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new DataProcessingOptions { QueueSize = 128 };
		services.AddScoped<ThetaHandler>();
		services.AddRecordHandler<ThetaHandler, RecordTheta>(config);
		using var provider = services.BuildServiceProvider();

		// Act
		var hasFactory = DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.TryGetValue(typeof(RecordTheta), out var factory);

		// Assert
		hasFactory.ShouldBeTrue();
		var handler = factory!(provider);
		handler.ShouldBeOfType<ThetaHandler>();
	}

	[Fact]
	public void ExposeCacheAsInternalConcurrentDictionary()
	{
		// Assert -- verify the static field is a ConcurrentDictionary (type safety for concurrent access)
		DataProcessingServiceCollectionExtensions.RecordHandlerFactories
			.ShouldBeAssignableTo<ConcurrentDictionary<Type, Func<IServiceProvider, object>>>();
	}
}
