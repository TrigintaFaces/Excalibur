// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Correlation;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaCorrelationBuilderShould
{
	[Fact]
	public void BuildEmptyConfiguration()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();

		// Act
		var config = builder.Build();

		// Assert
		config.ShouldNotBeNull();
		config.RuleCount.ShouldBe(0);
	}

	[Fact]
	public void CorrelateByStringExpression()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();

		// Act
		builder.CorrelateBy<TestMessage>(m => m.OrderId);
		var config = builder.Build();

		// Assert
		config.ShouldNotBeNull();
		config.TryGetCorrelationId(new TestMessage { OrderId = "ABC" }, out var id).ShouldBeTrue();
		id.ShouldBe("ABC");
	}

	[Fact]
	public void CorrelateByMultipleProperties()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();

		// Act
		builder.CorrelateBy<TestMessage>(m => m.OrderId, m => m.CustomerId);
		var config = builder.Build();

		// Assert
		config.ShouldNotBeNull();
		config.TryGetCorrelationId(new TestMessage { OrderId = "A", CustomerId = "B" }, out var id).ShouldBeTrue();
		id.ShouldBe("A|B");
	}

	[Fact]
	public void ThrowWhenCorrelationExpressionIsNull()
	{
		var builder = new SagaCorrelationBuilder<TestSaga>();

		Should.Throw<ArgumentNullException>(() =>
			builder.CorrelateBy<TestMessage>((Expression<Func<TestMessage, string>>)null!));
	}

	[Fact]
	public void ThrowWhenPropertyExpressionsAreNull()
	{
		var builder = new SagaCorrelationBuilder<TestSaga>();

		Should.Throw<ArgumentNullException>(() =>
			builder.CorrelateBy<TestMessage>((Expression<Func<TestMessage, object>>[])null!));
	}

	[Fact]
	public void ThrowWhenPropertyExpressionsAreEmpty()
	{
		var builder = new SagaCorrelationBuilder<TestSaga>();

		Should.Throw<ArgumentException>(() =>
			builder.CorrelateBy<TestMessage>(Array.Empty<Expression<Func<TestMessage, object>>>()));
	}

	[Fact]
	public void SupportMethodChaining()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();

		// Act & Assert - should support chaining
		var result = builder
			.CorrelateBy<TestMessage>(m => m.OrderId)
			.CorrelateBy<AnotherMessage>(m => m.Id);

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void CorrelateMultipleMessageTypes()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();

		// Act
		builder.CorrelateBy<TestMessage>(m => m.OrderId);
		builder.CorrelateBy<AnotherMessage>(m => m.Id);
		var config = builder.Build();

		// Assert
		config.RuleCount.ShouldBe(2);
		config.TryGetCorrelationId(new TestMessage { OrderId = "T1" }, out var id1).ShouldBeTrue();
		id1.ShouldBe("T1");

		config.TryGetCorrelationId(new AnotherMessage { Id = "A1" }, out var id2).ShouldBeTrue();
		id2.ShouldBe("A1");
	}

	[Fact]
	public void ReturnFalseForUnregisteredMessageType()
	{
		// Arrange
		var builder = new SagaCorrelationBuilder<TestSaga>();
		builder.CorrelateBy<TestMessage>(m => m.OrderId);
		var config = builder.Build();

		// Act & Assert
		config.TryGetCorrelationId(new AnotherMessage { Id = "X" }, out _).ShouldBeFalse();
	}

	private sealed class TestSaga { }

	private sealed class TestMessage
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
	}

	private sealed class AnotherMessage
	{
		public string Id { get; init; } = string.Empty;
	}
}
