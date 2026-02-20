// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Projections;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

internal sealed class BuilderTestState
{
	public int Counter { get; set; }
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiStreamProjectionBuilderShould
{
	[Fact]
	public void BuildProjectionWithStreams()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act
		var projection = builder
			.FromStream("stream-1")
			.FromStream("stream-2")
			.Build();

		// Assert
		projection.Streams.Count.ShouldBe(2);
		projection.Streams.ShouldContain("stream-1");
		projection.Streams.ShouldContain("stream-2");
	}

	[Fact]
	public void BuildProjectionWithCategories()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act
		var projection = builder
			.FromCategory("orders")
			.FromCategory("customers")
			.Build();

		// Assert
		projection.Categories.Count.ShouldBe(2);
		projection.Categories.ShouldContain("orders");
		projection.Categories.ShouldContain("customers");
	}

	[Fact]
	public void BuildProjectionWithEventHandler()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act - use concrete event type so Apply handler dispatch works
		var projection = builder
			.When<MultiStreamConcreteTestEvent>((state, _) => state.Counter++)
			.Build();

		// Assert
		projection.HandledEventTypes.ShouldContain(typeof(MultiStreamConcreteTestEvent));
	}

	[Fact]
	public void ThrowWhenStreamIdIsNull()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.FromStream(null!));
	}

	[Fact]
	public void ThrowWhenStreamIdIsEmpty()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.FromStream(""));
	}

	[Fact]
	public void ThrowWhenCategoryIsNull()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.FromCategory(null!));
	}

	[Fact]
	public void ThrowWhenCategoryIsEmpty()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.FromCategory(""));
	}

	[Fact]
	public void ThrowWhenHandlerIsNull()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.When<MultiStreamConcreteTestEvent>(null!));
	}

	[Fact]
	public void ReturnSameBuilderForFluentChaining()
	{
		// Arrange
		var builder = new MultiStreamProjectionBuilder<BuilderTestState>();

		// Act
		var result = builder.FromStream("s1");

		// Assert
		result.ShouldBeSameAs(builder);
	}
}
