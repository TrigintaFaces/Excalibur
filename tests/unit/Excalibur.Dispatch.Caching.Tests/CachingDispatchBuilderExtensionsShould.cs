// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress AOT warnings for IConfiguration binding tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Caching;

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

	#region AddDispatchCaching

	[Fact]
	public void AddDispatchCaching_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddDispatchCaching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatchCaching_RegistersCachingServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldNotBeNull();
		sp.GetService<ICacheTagTracker>().ShouldNotBeNull();
		sp.GetService<IResultCachePolicy>().ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchCaching_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.AddDispatchCaching(null!));
	}

	#endregion

	#region AddCaching

	[Fact]
	public void AddCaching_WithoutConfigure_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddCaching();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddCaching_WithoutConfigure_RegistersDefaultServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.AddCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldNotBeNull();
	}

	[Fact]
	public void AddCaching_WithConfigure_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.AddCaching(opts =>
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
	public void AddCaching_WithConfigure_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		var result = builder.AddCaching(opts => opts.Enabled = true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddCaching_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CachingDispatchBuilderExtensions.AddCaching(null!));
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
	public void WithCachingOptions_Action_DoesNotRegisterGlobalPolicy_WhenNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert — no IResultCachePolicy singleton should be registered (only from Options)
		var sp = services.BuildServiceProvider();
		sp.GetService<IResultCachePolicy>().ShouldBeNull();
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
	public void WithCachingOptions_Action_DoesNotRegisterCacheKeyBuilder_WhenNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeDispatchBuilder(services);

		// Act
		builder.WithCachingOptions(opts => opts.Enabled = true);

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldBeNull();
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
