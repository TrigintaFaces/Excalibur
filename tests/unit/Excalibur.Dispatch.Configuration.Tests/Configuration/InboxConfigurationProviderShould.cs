// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for the <see cref="InboxConfigurationProvider"/> class.
/// </summary>
/// <remarks>
/// Sprint 446 S446.5: Unit tests for inbox configuration provider.
/// Tests runtime configuration lookup and caching behavior.
/// Note: InboxConfigurationProvider is internal, accessed via InternalsVisibleTo.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxConfigurationProviderShould
{
	#region GetConfiguration Tests

	[Fact]
	public void ReturnConfiguration_WhenHandlerTypeIsConfigured()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>
		{
			[typeof(TestHandler)] = new InboxHandlerSettings { Retention = TimeSpan.FromHours(12) }
		};
		var provider = new InboxConfigurationProvider(configs);

		// Act
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert
		_ = settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(12));
	}

	[Fact]
	public void ReturnNull_WhenHandlerTypeIsNotConfigured()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>();
		var provider = new InboxConfigurationProvider(configs);

		// Act
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert
		settings.ShouldBeNull();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenHandlerTypeIsNull()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>();
		var provider = new InboxConfigurationProvider(configs);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			provider.GetConfiguration(null!));
	}

	[Fact]
	public void ReturnCorrectConfiguration_WhenMultipleHandlersConfigured()
	{
		// Arrange
		var handler1Settings = new InboxHandlerSettings { Retention = TimeSpan.FromHours(6) };
		var handler2Settings = new InboxHandlerSettings { Retention = TimeSpan.FromHours(12) };
		var configs = new Dictionary<Type, InboxHandlerSettings>
		{
			[typeof(TestHandler)] = handler1Settings,
			[typeof(AnotherHandler)] = handler2Settings
		};
		var provider = new InboxConfigurationProvider(configs);

		// Act
		var settings1 = provider.GetConfiguration(typeof(TestHandler));
		var settings2 = provider.GetConfiguration(typeof(AnotherHandler));

		// Assert
		_ = settings1.ShouldNotBeNull();
		settings1.Retention.ShouldBe(TimeSpan.FromHours(6));
		_ = settings2.ShouldNotBeNull();
		settings2.Retention.ShouldBe(TimeSpan.FromHours(12));
	}

	#endregion

	#region HasConfiguration Tests

	[Fact]
	public void ReturnTrue_WhenHandlerTypeIsConfigured()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>
		{
			[typeof(TestHandler)] = new InboxHandlerSettings()
		};
		var provider = new InboxConfigurationProvider(configs);

		// Act
		var hasConfig = provider.HasConfiguration(typeof(TestHandler));

		// Assert
		hasConfig.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenHandlerTypeIsNotConfigured()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>();
		var provider = new InboxConfigurationProvider(configs);

		// Act
		var hasConfig = provider.HasConfiguration(typeof(TestHandler));

		// Assert
		hasConfig.ShouldBeFalse();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenHasConfigurationHandlerTypeIsNull()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>();
		var provider = new InboxConfigurationProvider(configs);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			provider.HasConfiguration(null!));
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenConfigurationsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InboxConfigurationProvider(null!));
	}

	[Fact]
	public void AcceptEmptyConfigurations()
	{
		// Arrange
		var configs = new Dictionary<Type, InboxHandlerSettings>();

		// Act
		var provider = new InboxConfigurationProvider(configs);

		// Assert
		_ = provider.ShouldNotBeNull();
	}

	#endregion

	#region Integration with Builder Tests

	[Fact]
	public void ReturnConfigurationFromBuilder_WhenForHandlerWasUsed()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(48));

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert
		_ = settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(48));
	}

	[Fact]
	public void ReturnNullForUnconfiguredHandler_WhenBuilderHadOtherConfigurations()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(48));

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler), typeof(AnotherHandler) });
		var settings = provider.GetConfiguration(typeof(AnotherHandler));

		// Assert
		settings.ShouldBeNull();
	}

	[Fact]
	public void ResolveConfigurationFromNamespace_WhenNamespacePrefixMatches()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForNamespace("Excalibur.Dispatch.Tests.Configuration",
			cfg => cfg.WithRetention(TimeSpan.FromHours(72)));

		// Act - TestHandler is in Excalibur.Dispatch.Tests.Configuration namespace
		var provider = builder.Build(new[] { typeof(TestHandler) });
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert
		_ = settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(72));
	}

	[Fact]
	public void ResolveConfigurationFromPredicate_WhenPredicateMatches()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandlersMatching(
			t => t.Name.EndsWith("Handler"),
			cfg => cfg.UseInMemory());

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert
		_ = settings.ShouldNotBeNull();
		settings.UseInMemory.ShouldBeTrue();
	}

	[Fact]
	public void PreferExactTypeMatch_OverNamespaceMatch()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(6));
		_ = builder.ForNamespace("Excalibur.Dispatch.Tests.Configuration",
			cfg => cfg.WithRetention(TimeSpan.FromHours(72)));

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert - Exact match takes precedence
		_ = settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void PreferExactTypeMatch_OverPredicateMatch()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		_ = builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(6));
		_ = builder.ForHandlersMatching(
			t => t.Name.EndsWith("Handler"),
			cfg => cfg.WithRetention(TimeSpan.FromHours(72)));

		// Act
		var provider = builder.Build(new[] { typeof(TestHandler) });
		var settings = provider.GetConfiguration(typeof(TestHandler));

		// Assert - Exact match takes precedence
		_ = settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(6));
	}

	#endregion

	#region Test Fixtures

	private sealed class TestHandler
	{
	}

	private sealed class AnotherHandler
	{
	}

	#endregion
}
