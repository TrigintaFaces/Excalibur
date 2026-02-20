// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// ISP conformance tests verifying that cache interfaces and options classes
/// respect the project's quality gates (≤5 methods for interfaces, ≤10 properties for options).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheIspConformanceShould
{
	#region Interface Method Count Gates

	[Fact]
	public void ICacheProvider_HaveAtMostFiveMethods()
	{
		// Arrange
		var interfaceType = typeof(ICacheProvider);

		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICacheProvider has {methods.Length} methods, exceeding the 5-method ISP gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ICacheProviderManagement_HaveAtMostFiveMethods()
	{
		// Arrange
		var interfaceType = typeof(ICacheProviderManagement);

		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICacheProviderManagement has {methods.Length} methods, exceeding the 5-method ISP gate");
	}

	[Fact]
	public void ICacheTagTracker_HaveAtMostFiveMethods()
	{
		// Arrange
		var interfaceType = typeof(ICacheTagTracker);

		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICacheTagTracker has {methods.Length} methods, exceeding the 5-method ISP gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ICacheKeyBuilder_HaveAtMostFiveMethods()
	{
		// Arrange
		var interfaceType = typeof(ICacheKeyBuilder);

		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICacheKeyBuilder has {methods.Length} methods, exceeding the 5-method ISP gate. " +
			$"Methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	#endregion

	#region Options Property Count Gates

	[Fact]
	public void CacheBehaviorOptions_HaveAtMostTenProperties()
	{
		// Arrange
		var optionsType = typeof(CacheBehaviorOptions);

		// Act
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"CacheBehaviorOptions has {properties.Length} properties, exceeding the 10-property ISP gate. " +
			$"Properties: {string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void CacheCircuitBreakerOptions_HaveAtMostTenProperties()
	{
		// Arrange
		var optionsType = typeof(CacheCircuitBreakerOptions);

		// Act
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"CacheCircuitBreakerOptions has {properties.Length} properties, exceeding the 10-property ISP gate. " +
			$"Properties: {string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void CacheTypeNameOptions_HaveAtMostTenProperties()
	{
		// Arrange
		var optionsType = typeof(CacheTypeNameOptions);

		// Act
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"CacheTypeNameOptions has {properties.Length} properties, exceeding the 10-property ISP gate. " +
			$"Properties: {string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void MemoryCacheConfiguration_HaveAtMostTenProperties()
	{
		// Arrange
		var optionsType = typeof(MemoryCacheConfiguration);

		// Act
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"MemoryCacheConfiguration has {properties.Length} properties, exceeding the 10-property ISP gate. " +
			$"Properties: {string.Join(", ", properties.Select(p => p.Name))}");
	}

	[Fact]
	public void DistributedCacheConfiguration_HaveAtMostTenProperties()
	{
		// Arrange
		var optionsType = typeof(DistributedCacheConfiguration);

		// Act
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"DistributedCacheConfiguration has {properties.Length} properties, exceeding the 10-property ISP gate. " +
			$"Properties: {string.Join(", ", properties.Select(p => p.Name))}");
	}

	#endregion

	#region Sub-Options Composition: CacheResilienceOptions

	[Fact]
	public void CacheResilienceOptions_CircuitBreaker_SubOptionIsConfigurable()
	{
		// Arrange
		var options = new CacheResilienceOptions();

		// Act
		options.CircuitBreaker.Enabled = false;

		// Assert — sub-option properties are accessible via composition
		options.CircuitBreaker.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void CacheResilienceOptions_CircuitBreaker_FailureThresholdIsConfigurable()
	{
		// Arrange
		var options = new CacheResilienceOptions();

		// Act
		options.CircuitBreaker.FailureThreshold = 10;

		// Assert
		options.CircuitBreaker.FailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void CacheResilienceOptions_TypeNameCache_SubOptionsAreConfigurable()
	{
		// Arrange
		var options = new CacheResilienceOptions();

		// Act
		options.TypeNameCache.MaxCacheSize = 5000;
		options.TypeNameCache.CacheTtl = TimeSpan.FromMinutes(30);

		// Assert
		options.TypeNameCache.MaxCacheSize.ShouldBe(5000);
		options.TypeNameCache.CacheTtl.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region CacheOptions.Behavior sub-options

	[Fact]
	public void CacheOptions_Behavior_DefaultExpiration_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(30);

		// Assert
		options.Behavior.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void CacheOptions_Behavior_UseSlidingExpiration_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.UseSlidingExpiration = false;

		// Assert
		options.Behavior.UseSlidingExpiration.ShouldBeFalse();
	}

	[Fact]
	public void CacheOptions_Behavior_CacheTimeout_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.CacheTimeout = TimeSpan.FromSeconds(1);

		// Assert
		options.Behavior.CacheTimeout.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void CacheOptions_Behavior_JitterRatio_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.JitterRatio = 0.20;

		// Assert
		options.Behavior.JitterRatio.ShouldBe(0.20);
	}

	[Fact]
	public void CacheOptions_Behavior_EnableStatistics_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.EnableStatistics = true;

		// Assert
		options.Behavior.EnableStatistics.ShouldBeTrue();
	}

	[Fact]
	public void CacheOptions_Behavior_EnableCompression_IsSettable()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		options.Behavior.EnableCompression = true;

		// Assert
		options.Behavior.EnableCompression.ShouldBeTrue();
	}

	#endregion
}
