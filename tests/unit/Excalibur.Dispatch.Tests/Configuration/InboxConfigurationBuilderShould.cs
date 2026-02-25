// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxConfigurationBuilderShould
{
	[Fact]
	public void ForHandler_ReturnConfiguration()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var config = builder.ForHandler<TestHandler>();

		// Assert
		config.ShouldNotBeNull();
	}

	[Fact]
	public void ForHandler_ReturnSameConfigurationForSameHandler()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var config1 = builder.ForHandler<TestHandler>();
		var config2 = builder.ForHandler<TestHandler>();

		// Assert
		config1.ShouldBeSameAs(config2);
	}

	[Fact]
	public void Build_WithExactTypeConfig_ResolveForHandler()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(2))
			.UseInMemory();

		// Act
		var provider = builder.Build([typeof(TestHandler)]);

		// Assert
		provider.HasConfiguration(typeof(TestHandler)).ShouldBeTrue();
		var settings = provider.GetConfiguration(typeof(TestHandler));
		settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(2));
		settings.UseInMemory.ShouldBeTrue();
	}

	[Fact]
	public void Build_WithNoConfig_ReturnNoConfiguration()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act
		var provider = builder.Build([typeof(TestHandler)]);

		// Assert
		provider.HasConfiguration(typeof(TestHandler)).ShouldBeFalse();
		provider.GetConfiguration(typeof(TestHandler)).ShouldBeNull();
	}

	[Fact]
	public void Build_WithPredicateMatch_ResolveForMatchingHandler()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		builder.ForHandlersMatching(
			t => t.Name.Contains("Test", StringComparison.Ordinal),
			config => config.WithRetention(TimeSpan.FromMinutes(30)));

		// Act
		var provider = builder.Build([typeof(TestHandler), typeof(OtherHandler)]);

		// Assert
		provider.HasConfiguration(typeof(TestHandler)).ShouldBeTrue();
		var settings = provider.GetConfiguration(typeof(TestHandler));
		settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Build_WithNamespaceConfig_ResolveForMatchingNamespace()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		builder.ForNamespace(
			"Excalibur.Dispatch.Tests",
			config => config.UsePersistent());

		// Act
		var provider = builder.Build([typeof(TestHandler)]);

		// Assert
		provider.HasConfiguration(typeof(TestHandler)).ShouldBeTrue();
		var settings = provider.GetConfiguration(typeof(TestHandler));
		settings.ShouldNotBeNull();
		settings.UseInMemory.ShouldBeFalse();
	}

	[Fact]
	public void Build_ExactTypeTakesPrecedenceOverPredicate()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();
		builder.ForHandler<TestHandler>()
			.WithRetention(TimeSpan.FromHours(1));
		builder.ForHandlersMatching(
			_ => true,
			config => config.WithRetention(TimeSpan.FromHours(2)));

		// Act
		var provider = builder.Build([typeof(TestHandler)]);

		// Assert
		var settings = provider.GetConfiguration(typeof(TestHandler));
		settings.ShouldNotBeNull();
		settings.Retention.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void ForHandlersMatching_ThrowOnNullPredicate()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.ForHandlersMatching(null!, _ => { }));
	}

	[Fact]
	public void ForHandlersMatching_ThrowOnNullConfigure()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.ForHandlersMatching(_ => true, null!));
	}

	[Fact]
	public void ForNamespace_ThrowOnNullOrEmptyPrefix()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(
			() => builder.ForNamespace("", _ => { }));
		Should.Throw<ArgumentException>(
			() => builder.ForNamespace("  ", _ => { }));
	}

	[Fact]
	public void ForNamespace_ThrowOnNullConfigure()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.ForNamespace("Some.Namespace", null!));
	}

	[Fact]
	public void Build_ThrowOnNullHandlerTypes()
	{
		// Arrange
		var builder = new InboxConfigurationBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => builder.Build(null!));
	}

	// Test helper types
	private sealed class TestHandler;
	private sealed class OtherHandler;
}
