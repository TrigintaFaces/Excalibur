// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.ElasticSearch;

/// <summary>
/// Tests for <see cref="ElasticSearchProjectionsEventSourcingBuilderExtensions"/>.
/// Covers null guards, fluent chaining, and DI registration for all overloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ElasticSearchProjectionsEventSourcingBuilderExtensionsShould
{
	private static IEventSourcingBuilder CreateBuilder()
	{
		return new ExcaliburEventSourcingBuilder(new ServiceCollection());
	}

	private static IEventSourcingBuilder CreateBuilder(out IServiceCollection services)
	{
		services = new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(services);
	}

	// ═══════════════════════════════════════════════════
	// AddElasticSearchProjections(nodeUri, configure)
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ThrowWhenBuilderIsNull_NodeUriOverload()
	{
		IEventSourcingBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.AddElasticSearchProjections("http://localhost:9200", _ => { }));
	}

	[Fact]
	public void ReturnSameBuilderForFluentChaining_NodeUriOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.AddElasticSearchProjections(
			"http://localhost:9200",
			reg => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// ═══════════════════════════════════════════════════
	// AddElasticSearchProjections(configureShared, configure)
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ThrowWhenBuilderIsNull_SharedOptionsOverload()
	{
		IEventSourcingBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.AddElasticSearchProjections(_ => { }, _ => { }));
	}

	[Fact]
	public void ReturnSameBuilderForFluentChaining_SharedOptionsOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.AddElasticSearchProjections(
			opts => opts.NodeUri = "http://localhost:9200",
			reg => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// ═══════════════════════════════════════════════════
	// AddElasticSearchProjectionStore<T>(configureOptions)
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ThrowWhenBuilderIsNull_ConfigureOptionsOverload()
	{
		IEventSourcingBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.AddElasticSearchProjectionStore<TestProjection>(
				opts => opts.NodeUri = "http://localhost:9200"));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentNullException>(() =>
			builder.AddElasticSearchProjectionStore<TestProjection>(
				(Action<ElasticSearchProjectionStoreOptions>)null!));
	}

	[Fact]
	public void ReturnSameBuilderForFluentChaining_ConfigureOptionsOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.AddElasticSearchProjectionStore<TestProjection>(
			opts => opts.NodeUri = "http://localhost:9200");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterProjectionStore_ConfigureOptionsOverload()
	{
		// Arrange
		var builder = CreateBuilder(out var services);

		// Act
		builder.AddElasticSearchProjectionStore<TestProjection>(
			opts => opts.NodeUri = "http://localhost:9200");

		// Assert
		var descriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(Excalibur.EventSourcing.Abstractions.IProjectionStore<TestProjection>));
		descriptor.ShouldNotBeNull();
	}

	// ═══════════════════════════════════════════════════
	// AddElasticSearchProjectionStore<T>(nodeUri, configureOptions?)
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ThrowWhenBuilderIsNull_NodeUriProjectionOverload()
	{
		IEventSourcingBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.AddElasticSearchProjectionStore<TestProjection>("http://localhost:9200"));
	}

	[Fact]
	public void ThrowWhenNodeUriIsNull()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.AddElasticSearchProjectionStore<TestProjection>((string)null!));
	}

	[Fact]
	public void ThrowWhenNodeUriIsWhitespace()
	{
		var builder = CreateBuilder();

		Should.Throw<ArgumentException>(() =>
			builder.AddElasticSearchProjectionStore<TestProjection>("   "));
	}

	[Fact]
	public void ReturnSameBuilderForFluentChaining_NodeUriProjectionOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.AddElasticSearchProjectionStore<TestProjection>(
			"http://localhost:9200");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterProjectionStore_NodeUriOverload()
	{
		// Arrange
		var builder = CreateBuilder(out var services);

		// Act
		builder.AddElasticSearchProjectionStore<TestProjection>("http://localhost:9200");

		// Assert
		var descriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(Excalibur.EventSourcing.Abstractions.IProjectionStore<TestProjection>));
		descriptor.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptNullConfigureOptions_NodeUriOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act — null configureOptions is valid (optional parameter)
		var result = builder.AddElasticSearchProjectionStore<TestProjection>(
			"http://localhost:9200",
			null);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	// ═══════════════════════════════════════════════════
	// Full composition chain
	// ═══════════════════════════════════════════════════

	[Fact]
	public void SupportFullCompositionChain()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddOptions();

		var esBuilder = new ExcaliburEventSourcingBuilder(services);

		// Act — full chain as a consumer would write it
		var result = esBuilder
			.AddElasticSearchProjectionStore<TestProjection>(
				opts => opts.NodeUri = "http://localhost:9200")
			.AddElasticSearchProjectionStore<AnotherProjection>(
				"http://localhost:9200");

		// Assert — both stores registered, builder returned
		result.ShouldBeSameAs(esBuilder);

		services.Where(s =>
			s.ServiceType == typeof(Excalibur.EventSourcing.Abstractions.IProjectionStore<TestProjection>))
			.ShouldNotBeEmpty();

		services.Where(s =>
			s.ServiceType == typeof(Excalibur.EventSourcing.Abstractions.IProjectionStore<AnotherProjection>))
			.ShouldNotBeEmpty();
	}

	// ═══════════════════════════════════════════════════
	// Test helper types
	// ═══════════════════════════════════════════════════

	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	private sealed class AnotherProjection
	{
		public string Id { get; set; } = string.Empty;
		public int Count { get; set; }
	}
}
