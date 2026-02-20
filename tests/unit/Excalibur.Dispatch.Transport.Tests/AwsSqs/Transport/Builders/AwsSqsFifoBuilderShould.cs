// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAwsSqsFifoBuilder"/> fluent builder pattern.
/// Part of S470.5 - Unit Tests for Fluent Builders (Sprint 470).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsFifoBuilderShould : UnitTestBase
{
	#region ContentBasedDeduplication Tests

	[Fact]
	public void ContentBasedDeduplication_EnableDeduplication()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.ContentBasedDeduplication(true);

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void ContentBasedDeduplication_DisableDeduplication()
	{
		// Arrange
		var options = new AwsSqsFifoOptions { ContentBasedDeduplication = true };
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.ContentBasedDeduplication(false);

		// Assert
		options.ContentBasedDeduplication.ShouldBeFalse();
	}

	[Fact]
	public void ContentBasedDeduplication_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.ContentBasedDeduplication(true);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region DeduplicationIdSelector<T> Tests

	[Fact]
	public void DeduplicationIdSelector_Generic_SetSelector()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.DeduplicationIdSelector<TestMessage>(msg => $"dedup-{msg.Id}");

		// Assert
		_ = options.DeduplicationIdSelector.ShouldNotBeNull();
	}

	[Fact]
	public void DeduplicationIdSelector_Generic_SelectorReturnCorrectValue()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);
		var message = new TestMessage { Id = "test-123" };

		// Act
		_ = builder.DeduplicationIdSelector<TestMessage>(msg => $"dedup-{msg.Id}");
		var result = options.DeduplicationIdSelector(message);

		// Assert
		result.ShouldBe("dedup-test-123");
	}

	[Fact]
	public void DeduplicationIdSelector_Generic_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.DeduplicationIdSelector<TestMessage>(msg => msg.Id);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DeduplicationIdSelector_Generic_ThrowWhenSelectorIsNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.DeduplicationIdSelector<TestMessage>(null!));
	}

	#endregion

	#region DeduplicationIdSelector (object) Tests

	[Fact]
	public void DeduplicationIdSelector_Object_SetSelector()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.DeduplicationIdSelector(msg => $"dedup-{msg.GetHashCode()}");

		// Assert
		_ = options.DeduplicationIdSelector.ShouldNotBeNull();
	}

	[Fact]
	public void DeduplicationIdSelector_Object_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.DeduplicationIdSelector(msg => "test");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DeduplicationIdSelector_Object_ThrowWhenSelectorIsNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.DeduplicationIdSelector((Func<object, string>)null!));
	}

	#endregion

	#region MessageGroupIdSelector<T> Tests

	[Fact]
	public void MessageGroupIdSelector_Generic_SetSelector()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.MessageGroupIdSelector<TestMessage>(msg => msg.TenantId);

		// Assert
		_ = options.MessageGroupIdSelector.ShouldNotBeNull();
	}

	[Fact]
	public void MessageGroupIdSelector_Generic_SelectorReturnCorrectValue()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);
		var message = new TestMessage { TenantId = "tenant-abc" };

		// Act
		_ = builder.MessageGroupIdSelector<TestMessage>(msg => msg.TenantId);
		var result = options.MessageGroupIdSelector(message);

		// Assert
		result.ShouldBe("tenant-abc");
	}

	[Fact]
	public void MessageGroupIdSelector_Generic_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.MessageGroupIdSelector<TestMessage>(msg => msg.TenantId);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void MessageGroupIdSelector_Generic_ThrowWhenSelectorIsNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.MessageGroupIdSelector<TestMessage>(null!));
	}

	#endregion

	#region MessageGroupIdSelector (object) Tests

	[Fact]
	public void MessageGroupIdSelector_Object_SetSelector()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder.MessageGroupIdSelector(_ => "global");

		// Assert
		_ = options.MessageGroupIdSelector.ShouldNotBeNull();
	}

	[Fact]
	public void MessageGroupIdSelector_Object_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		var result = builder.MessageGroupIdSelector(_ => "global");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void MessageGroupIdSelector_Object_ThrowWhenSelectorIsNull()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.MessageGroupIdSelector((Func<object, string>)null!));
	}

	#endregion

	#region Full Configuration Tests

	[Fact]
	public void Builder_SupportFullConfigurationWithContentBasedDeduplication()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.ContentBasedDeduplication(true)
			.MessageGroupIdSelector<TestMessage>(msg => msg.TenantId);

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
		options.HasValidDeduplication.ShouldBeTrue();
		options.HasMessageGroupIdSelector.ShouldBeTrue();
	}

	[Fact]
	public void Builder_SupportFullConfigurationWithCustomDeduplication()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.DeduplicationIdSelector<TestMessage>(msg => $"order-{msg.Id}")
			.MessageGroupIdSelector<TestMessage>(msg => msg.TenantId);

		// Assert
		options.ContentBasedDeduplication.ShouldBeFalse();
		options.HasValidDeduplication.ShouldBeTrue();
		options.HasMessageGroupIdSelector.ShouldBeTrue();
	}

	[Fact]
	public void Builder_SupportGlobalOrderingPattern()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act - Common pattern: all messages in same group for strict ordering
		_ = builder
			.ContentBasedDeduplication(true)
			.MessageGroupIdSelector(_ => "global");

		// Assert
		options.ContentBasedDeduplication.ShouldBeTrue();
		var result = options.MessageGroupIdSelector(new object());
		result.ShouldBe("global");
	}

	[Fact]
	public void Builder_AllowOverwritingSelectors()
	{
		// Arrange
		var options = new AwsSqsFifoOptions();
		IAwsSqsFifoBuilder builder = CreateBuilder(options);

		// Act
		_ = builder
			.MessageGroupIdSelector(_ => "first")
			.MessageGroupIdSelector(_ => "second");

		// Assert
		var result = options.MessageGroupIdSelector(new object());
		result.ShouldBe("second");
	}

	#endregion

	/// <summary>
	/// Creates a builder instance for testing.
	/// Uses reflection to instantiate the internal AwsSqsFifoBuilder.
	/// </summary>
	private static IAwsSqsFifoBuilder CreateBuilder(AwsSqsFifoOptions options)
	{
		var builderType = typeof(AwsSqsFifoOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Aws.AwsSqsFifoBuilder");
		_ = builderType.ShouldNotBeNull("AwsSqsFifoBuilder type should exist");

		var builder = Activator.CreateInstance(builderType, options);
		_ = builder.ShouldNotBeNull("Builder instance should be created");

		return (IAwsSqsFifoBuilder)builder;
	}

	/// <summary>
	/// Test message class for type-safe selector tests.
	/// </summary>
	private sealed class TestMessage
	{
		public string Id { get; set; } = string.Empty;
		public string TenantId { get; set; } = string.Empty;
	}
}
