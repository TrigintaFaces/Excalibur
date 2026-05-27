// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Tests for <see cref="DefaultIndexMappingConvention"/> (bd-jxho7n).
/// Covers singleton pattern, pass-through behavior, null guards, and ISP contract.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class DefaultIndexMappingConventionShould
{
	// ═══════════════════════════════════════════════════
	// Singleton
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ExposeSingletonInstance()
	{
		// Assert
		DefaultIndexMappingConvention.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameInstanceOnMultipleAccesses()
	{
		// Act
		var first = DefaultIndexMappingConvention.Instance;
		var second = DefaultIndexMappingConvention.Instance;

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void ImplementIIndexMappingConvention()
	{
		// Assert
		DefaultIndexMappingConvention.Instance.ShouldBeAssignableTo<IIndexMappingConvention>();
	}

	// ═══════════════════════════════════════════════════
	// Pass-through behavior
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ReturnInferredPropertiesUnchanged()
	{
		// Arrange
		var convention = DefaultIndexMappingConvention.Instance;
		var inferred = new Properties
		{
			{ "name", new KeywordProperty() },
			{ "count", new LongNumberProperty() },
		};

		// Act
		var result = convention.ConfigureMappings(typeof(TestProjection), inferred);

		// Assert — default convention must return input unchanged
		result.ShouldBeSameAs(inferred);
	}

	[Fact]
	public void ReturnEmptyPropertiesUnchanged()
	{
		// Arrange
		var convention = DefaultIndexMappingConvention.Instance;
		var empty = new Properties();

		// Act
		var result = convention.ConfigureMappings(typeof(TestProjection), empty);

		// Assert
		result.ShouldBeSameAs(empty);
	}

	// ═══════════════════════════════════════════════════
	// Null guards
	// ═══════════════════════════════════════════════════

	[Fact]
	public void ThrowWhenProjectionTypeIsNull()
	{
		var convention = DefaultIndexMappingConvention.Instance;

		Should.Throw<ArgumentNullException>(() =>
			convention.ConfigureMappings(null!, new Properties()));
	}

	[Fact]
	public void ThrowWhenInferredPropertiesIsNull()
	{
		var convention = DefaultIndexMappingConvention.Instance;

		Should.Throw<ArgumentNullException>(() =>
			convention.ConfigureMappings(typeof(TestProjection), null!));
	}

	// ═══════════════════════════════════════════════════
	// Interface shape
	// ═══════════════════════════════════════════════════

	[Fact]
	public void IIndexMappingConvention_BeAnInterface()
	{
		typeof(IIndexMappingConvention).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void IIndexMappingConvention_DefineExactlyOneMethod()
	{
		var methods = typeof(IIndexMappingConvention).GetMethods(
			System.Reflection.BindingFlags.Public |
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.DeclaredOnly);

		methods.Length.ShouldBe(1, "ISP: IIndexMappingConvention should declare exactly one method");
		methods[0].Name.ShouldBe(nameof(IIndexMappingConvention.ConfigureMappings));
	}

	[Fact]
	public void IIndexMappingConvention_ConfigureMappingsReturnsProperties()
	{
		var method = typeof(IIndexMappingConvention).GetMethod(
			nameof(IIndexMappingConvention.ConfigureMappings));

		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Properties));
	}

	[Fact]
	public void IIndexMappingConvention_ConfigureMappingsHasCorrectParameters()
	{
		var method = typeof(IIndexMappingConvention).GetMethod(
			nameof(IIndexMappingConvention.ConfigureMappings));

		method.ShouldNotBeNull();
		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Type));
		parameters[0].Name.ShouldBe("projectionType");
		parameters[1].ParameterType.ShouldBe(typeof(Properties));
		parameters[1].Name.ShouldBe("inferredProperties");
	}

	// ═══════════════════════════════════════════════════
	// Custom implementation
	// ═══════════════════════════════════════════════════

	[Fact]
	public void AllowCustomConventionImplementation()
	{
		// Arrange — a custom convention that adds a field
		IIndexMappingConvention convention = new AddTimestampConvention();
		var inferred = new Properties
		{
			{ "name", new KeywordProperty() },
		};

		// Act
		var result = convention.ConfigureMappings(typeof(TestProjection), inferred);

		// Assert — custom convention added a field
		result.ShouldContainKey("name");
		result.ShouldContainKey("_timestamp");
	}

	[Fact]
	public void OptionsPropertyDefaultsToNull()
	{
		// Arrange
		var options = new ElasticSearchProjectionStoreOptions();

		// Assert — null means use DefaultIndexMappingConvention
		options.IndexMappingConvention.ShouldBeNull();
	}

	[Fact]
	public void OptionsPropertyAcceptsCustomConvention()
	{
		// Arrange
		var options = new ElasticSearchProjectionStoreOptions();
		var custom = new AddTimestampConvention();

		// Act
		options.IndexMappingConvention = custom;

		// Assert
		options.IndexMappingConvention.ShouldBeSameAs(custom);
	}

	// ═══════════════════════════════════════════════════
	// Test helpers
	// ═══════════════════════════════════════════════════

	private sealed class TestProjection
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	private sealed class AddTimestampConvention : IIndexMappingConvention
	{
		public Properties ConfigureMappings(Type projectionType, Properties inferredProperties)
		{
			inferredProperties.Add("_timestamp", new DateProperty());
			return inferredProperties;
		}
	}
}
