#pragma warning disable IL2026, IL3050 // Suppress AOT warnings for IConfiguration binding tests

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CachingDispatchBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingDispatchBuilderExtensionsShould
{
	private static readonly string[] ExpectedTags = ["tag1", "tag2"];

	#region UseCaching

	[Fact]
	public void UseCaching_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseCaching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseCaching_RegistersCachingServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.UseCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldNotBeNull();
		sp.GetService<ICacheTagTracker>().ShouldNotBeNull();
		sp.GetService<IResultCachePolicy>().ShouldNotBeNull();
	}

	[Fact]
	public void UseCaching_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.UseCaching(null!));
	}

	#endregion

	#region UseCaching with configure

	[Fact]
	public void UseCaching_WithoutConfigure_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseCaching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseCaching_WithoutConfigure_RegistersDefaultServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.UseCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldNotBeNull();
	}

	[Fact]
	public void UseCaching_WithConfigure_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.UseCaching(opts =>
		{
			opts.Enabled = true;
			opts.CacheMode = CacheMode.Memory;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void UseCaching_WithConfigure_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.UseCaching(opts => opts.Enabled = true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseCaching_WithNullBuilder_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.UseCaching(null!));
	}

	#endregion

	#region WithCachingOptions (Action delegate)

	[Fact]
	public void WithCachingOptions_Action_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithCachingOptions_Action_ConfiguresEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void WithCachingOptions_Action_ConfiguresCacheMode()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.CacheMode = CacheMode.Distributed);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.CacheMode.ShouldBe(CacheMode.Distributed);
	}

	[Fact]
	public void WithCachingOptions_Action_ConfiguresDefaultExpiration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var expiration = TimeSpan.FromMinutes(30);

		// Act
		builder.WithCachingOptions(opts => opts.Behavior.DefaultExpiration = expiration);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Behavior.DefaultExpiration.ShouldBe(expiration);
	}

	[Fact]
	public void WithCachingOptions_Action_ConfiguresCacheTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var timeout = TimeSpan.FromMilliseconds(500);

		// Act
		builder.WithCachingOptions(opts => opts.Behavior.CacheTimeout = timeout);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Behavior.CacheTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void WithCachingOptions_Action_ConfiguresDefaultTags()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.DefaultTags = ["tag1", "tag2"]);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.DefaultTags.ShouldBe(ExpectedTags);
	}

	[Fact]
	public void WithCachingOptions_Action_RegistersGlobalPolicy_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var policy = A.Fake<IResultCachePolicy>();

		// Act
		builder.WithCachingOptions(opts => opts.GlobalPolicy = policy);

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetRequiredService<IResultCachePolicy>().ShouldBeSameAs(policy);
	}

	[Fact]
	public void WithCachingOptions_Action_RegistersDefaultPolicy_WhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert — configuring caching ALSO enables it (fh3bzk pit-of-success default-on), so a default
		// IResultCachePolicy is registered even when the action leaves GlobalPolicy null.
		services.ShouldContain(sd => sd.ServiceType == typeof(IResultCachePolicy));
	}

	[Fact]
	public void WithCachingOptions_Action_RegistersCacheKeyBuilder_WhenProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var keyBuilder = A.Fake<ICacheKeyBuilder>();

		// Act
		builder.WithCachingOptions(opts => opts.CacheKeyBuilder = keyBuilder);

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetRequiredService<ICacheKeyBuilder>().ShouldBeSameAs(keyBuilder);
	}

	[Fact]
	public void WithCachingOptions_Action_InvokesConfigureExactlyOnce()
	{
		// Arrange — regression: WithCachingOptions previously re-invoked the delegate on a throwaway probe
		// to extract collaborators, so a non-idempotent configure fired twice. It must fire exactly once.
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var invocationCount = 0;

		// Act
		_ = builder.WithCachingOptions(opts =>
		{
			invocationCount++;
			opts.GlobalPolicy = A.Fake<IResultCachePolicy>();
			opts.CacheKeyBuilder = A.Fake<ICacheKeyBuilder>();
		});

		// Assert — the delegate fires once, synchronously, during registration.
		invocationCount.ShouldBe(1);

		// And the collaborators captured from that single invocation are still wired into DI.
		var sp = services.BuildServiceProvider();
		sp.GetRequiredService<IResultCachePolicy>().ShouldNotBeNull();
		sp.GetRequiredService<ICacheKeyBuilder>().ShouldNotBeNull();
		invocationCount.ShouldBe(1, "resolving options/collaborators must not re-run the delegate");
	}

	[Fact]
	public void WithCachingOptions_Action_RegistersDefaultCacheKeyBuilder_WhenEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert — enabling caching wires the default ICacheKeyBuilder (DefaultCacheKeyBuilder, which resolves
		// DispatchJsonSerializer from the container in real usage) even when the action leaves CacheKeyBuilder null.
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheKeyBuilder));
	}

	[Fact]
	public void WithCachingOptions_Action_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithCachingOptions(null!, _ => { }));
	}

	[Fact]
	public void WithCachingOptions_Action_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithCachingOptions((Action<CacheOptions>)null!));
	}

	#endregion

	#region WithCachingOptions (IConfiguration)

	[Fact]
	public void WithCachingOptions_Configuration_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
			})
			.Build();

		// Act
		var result = builder.WithCachingOptions(config);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithCachingOptions_Configuration_BindsValues()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Enabled"] = "true",
				["CacheMode"] = "Memory",
			})
			.Build();

		// Act
		builder.WithCachingOptions(config);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void WithCachingOptions_Configuration_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithCachingOptions(null!, config));
	}

	#endregion

	#region WithResultCachePolicy (delegate)

	[Fact]
	public void WithResultCachePolicy_Delegate_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.WithResultCachePolicy((_, _) => true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithResultCachePolicy_Delegate_RegistersPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithResultCachePolicy((_, _) => false);

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy>();
		policy.ShouldNotBeNull();
		policy.ShouldBeOfType<DefaultResultCachePolicy>();
	}

	[Fact]
	public void WithResultCachePolicy_Delegate_InvokesDelegate()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var message = A.Fake<IDispatchMessage>();

		// Act
		builder.WithResultCachePolicy((msg, result) => result is not null);

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy>();
		policy.ShouldCache(message, "non-null").ShouldBeTrue();
		policy.ShouldCache(message, null).ShouldBeFalse();
	}

	[Fact]
	public void WithResultCachePolicy_Delegate_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy(null!, (_, _) => true));
	}

	#endregion

	#region WithResultCachePolicy<TMessage> (typed delegate)

	[Fact]
	public void WithResultCachePolicy_Typed_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.WithResultCachePolicy<TestMessage>((_, _) => true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithResultCachePolicy_Typed_RegistersTypedPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithResultCachePolicy<TestMessage>((_, _) => false);

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy<TestMessage>>();
		policy.ShouldNotBeNull();
	}

	[Fact]
	public void WithResultCachePolicy_Typed_InvokesDelegate()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);
		var message = new TestMessage();

		// Act
		builder.WithResultCachePolicy<TestMessage>((msg, result) => result is string);

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy<TestMessage>>();
		policy.ShouldCache(message, "string-result").ShouldBeTrue();
		policy.ShouldCache(message, 42).ShouldBeFalse();
		policy.ShouldCache(message, null).ShouldBeFalse();
	}

	[Fact]
	public void WithResultCachePolicy_Typed_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy<TestMessage>(null!, (_, _) => true));
	}

	[Fact]
	public void WithResultCachePolicy_Typed_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithResultCachePolicy<TestMessage>((Func<TestMessage, object?, bool>)null!));
	}

	#endregion

	#region WithResultCachePolicy<TMessage, TPolicy> (type registration)

	[Fact]
	public void WithResultCachePolicy_TypeRegistration_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.WithResultCachePolicy<TestMessage, TestResultCachePolicy>();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void WithResultCachePolicy_TypeRegistration_RegistersTypedPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithResultCachePolicy<TestMessage, TestResultCachePolicy>();

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy<TestMessage>>();
		policy.ShouldNotBeNull();
		policy.ShouldBeOfType<TestResultCachePolicy>();
	}

	[Fact]
	public void WithResultCachePolicy_TypeRegistration_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.WithResultCachePolicy<TestMessage, TestResultCachePolicy>(null!));
	}

	#endregion

	#region Helpers & Test Types

	private static IDispatchBuilder CreateFakeDispatchBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	private sealed class TestMessage : IDispatchMessage;

	private sealed class TestResultCachePolicy : IResultCachePolicy<TestMessage>
	{
		public bool ShouldCache(TestMessage message, object? result) => true;
	}

	#endregion
}

#pragma warning restore IL2026, IL3050
