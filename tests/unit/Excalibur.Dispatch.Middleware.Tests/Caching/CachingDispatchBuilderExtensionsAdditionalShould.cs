// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Additional unit tests for <see cref="CachingDispatchBuilderExtensions"/> covering
/// null argument validation and edge cases for uncovered branches.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingDispatchBuilderExtensionsAdditionalShould : UnitTestBase
{
	[Fact]
	public void AddDispatchCaching_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.AddDispatchCaching(null!));
	}

	[Fact]
	public void AddCaching_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.AddCaching(null!));
	}

	[Fact]
	public void WithCachingOptions_Action_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithCachingOptions(null!, _ => { }));
	}

	[Fact]
	public void WithCachingOptions_Action_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithCachingOptions((Action<CacheOptions>)null!));
	}

	[Fact]
	public void WithCachingOptions_Configuration_ThrowsOnNullBuilder()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithCachingOptions(null!, config));
	}

	[Fact]
	public void WithResultCachePolicy_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy(null!, (_, _) => true));
	}

	[Fact]
	public void WithResultCachePolicyGeneric_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy<TestDispatchMessage>(null!, (_, _) => true));
	}

	[Fact]
	public void WithResultCachePolicyGeneric_ThrowsOnNullShouldCache()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithResultCachePolicy((Func<TestDispatchMessage, object?, bool>)null!));
	}

	[Fact]
	public void WithResultCachePolicyImplementation_ThrowsOnNullBuilder()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy<TestDispatchMessage, TestCachePolicy>(null!));
	}

	[Fact]
	public void WithCachingOptions_WithoutGlobalPolicyOrKeyBuilder_DoesNotRegisterSingletons()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act -- configure without setting GlobalPolicy or CacheKeyBuilder
		builder.WithCachingOptions(options =>
		{
			options.Enabled = true;
			options.CacheMode = CacheMode.Memory;
		});

		// Assert -- should not have IResultCachePolicy or ICacheKeyBuilder singletons
		services.ShouldNotContain(sd => sd.ServiceType == typeof(IResultCachePolicy));
		services.ShouldNotContain(sd => sd.ServiceType == typeof(ICacheKeyBuilder));
	}

	[Fact]
	public void WithCachingOptions_CopiesAllOptionsToConfigureDelegate()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.WithCachingOptions(options =>
		{
			options.Enabled = true;
			options.CacheMode = CacheMode.Distributed;
			options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(30);
			options.DefaultTags = ["tag1"];
			options.Behavior.CacheTimeout = TimeSpan.FromSeconds(5);
		});

		// Assert -- verify IConfigureOptions<CacheOptions> is registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<CacheOptions>));
	}

	// Helper types

	private sealed class TestDispatchMessage : IDispatchMessage
	{
		public string MessageId { get; set; } = Guid.NewGuid().ToString();
	}

	private sealed class TestCachePolicy : IResultCachePolicy<TestDispatchMessage>
	{
		public bool ShouldCache(TestDispatchMessage message, object? result) => true;
	}
}
