// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Caching;
using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CachingDispatchBuilderExtensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CachingDispatchBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDispatchCaching_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddDispatchCaching();

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddCaching_WithNullConfigure_RegistersDefaultCaching()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddCaching(configure: null);

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddCaching_WithConfigureDelegate_RegistersWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var configureCalled = false;

		// Act
		var result = builder.AddCaching(options =>
		{
			configureCalled = true;
			options.Enabled = true;
			options.CacheMode = CacheMode.Memory;
		});

		// Assert
		result.ShouldBe(builder);
		configureCalled.ShouldBeTrue();
	}

	[Fact]
	public void WithCachingOptions_WithGlobalPolicy_RegistersPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		IResultCachePolicy? registeredPolicy = null;

		// Act
		var result = builder.WithCachingOptions(options =>
		{
			options.GlobalPolicy = A.Fake<IResultCachePolicy>();
			registeredPolicy = options.GlobalPolicy;
		});

		// Assert
		result.ShouldBe(builder);
		registeredPolicy.ShouldNotBeNull();
		services.ShouldContain(sd => sd.ServiceType == typeof(IResultCachePolicy));
	}

	[Fact]
	public void WithCachingOptions_WithCacheKeyBuilder_RegistersBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		ICacheKeyBuilder? registeredBuilder = null;

		// Act
		var result = builder.WithCachingOptions(options =>
		{
			options.CacheKeyBuilder = A.Fake<ICacheKeyBuilder>();
			registeredBuilder = options.CacheKeyBuilder;
		});

		// Assert
		result.ShouldBe(builder);
		registeredBuilder.ShouldNotBeNull();
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheKeyBuilder));
	}

	[Fact]
	public void WithCachingOptions_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.WithCachingOptions(options =>
		{
			options.Enabled = true;
			options.CacheMode = CacheMode.Hybrid;
			options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
		});

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<CacheOptions>));
	}

	[Fact]
	public void WithCachingOptions_FromConfiguration_BindsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var configurationBuilder = new ConfigurationBuilder();
		configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["Enabled"] = "true",
			["CacheMode"] = "Memory"
		});
		var configuration = configurationBuilder.Build();

		// Act
		var result = builder.WithCachingOptions(configuration);

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Options.IConfigureOptions<CacheOptions>));
	}

	[Fact]
	public void WithResultCachePolicy_RegistersGlobalPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.WithResultCachePolicy((msg, res) => true);

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(IResultCachePolicy));
	}

	[Fact]
	public void WithResultCachePolicyGeneric_RegistersTypedPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.WithResultCachePolicy<TestMessage>((msg, res) => true);

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd => sd.ServiceType == typeof(IResultCachePolicy<TestMessage>));
	}

	[Fact]
	public void WithResultCachePolicyGeneric_ResolvedPolicy_InvokesShouldCacheDelegate()
	{
		// Arrange — verify the TypedResultCachePolicy<T> lambda is actually invoked
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var delegateCalled = false;

		builder.WithResultCachePolicy<TestMessage>((msg, res) =>
		{
			delegateCalled = true;
			return res != null;
		});

		// Act — resolve and invoke the policy
		var provider = services.BuildServiceProvider();
		var policy = provider.GetRequiredService<IResultCachePolicy<TestMessage>>();
		var shouldCache = policy.ShouldCache(new TestMessage(), "some-result");

		// Assert
		delegateCalled.ShouldBeTrue();
		shouldCache.ShouldBeTrue();
	}

	[Fact]
	public void WithResultCachePolicyGeneric_ResolvedPolicy_ReturnsFalseWhenDelegateSaysFalse()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.WithResultCachePolicy<TestMessage>((msg, res) => false);

		// Act
		var provider = services.BuildServiceProvider();
		var policy = provider.GetRequiredService<IResultCachePolicy<TestMessage>>();
		var shouldCache = policy.ShouldCache(new TestMessage(), "result");

		// Assert
		shouldCache.ShouldBeFalse();
	}

	[Fact]
	public void WithResultCachePolicyImplementation_RegistersImplementationType()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.WithResultCachePolicy<TestMessage, TestCachePolicy>();

		// Assert
		result.ShouldBe(builder);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IResultCachePolicy<TestMessage>) &&
			sd.ImplementationType == typeof(TestCachePolicy));
	}

	// Test helper classes
	private sealed class TestMessage : Abstractions.IDispatchMessage
	{
		public string MessageId { get; set; } = Guid.NewGuid().ToString();
	}

	private sealed class TestCachePolicy : IResultCachePolicy<TestMessage>
	{
		public bool ShouldCache(TestMessage message, object? result) => true;
	}
}
