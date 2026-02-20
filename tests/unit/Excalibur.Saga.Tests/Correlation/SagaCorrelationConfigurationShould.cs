// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Correlation;

/// <summary>
/// Unit tests for <see cref="SagaCorrelationConfiguration{TSaga}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaCorrelationConfigurationShould
{
	[Fact]
	public void TryGetCorrelationId_ShouldReturnTrue_WhenMessageTypeIsRegistered()
	{
		// Arrange
		var correlators = new Dictionary<Type, Func<object, string>>
		{
			[typeof(TestOrderMessage)] = msg => ((TestOrderMessage)msg).OrderId,
		};
		var config = new SagaCorrelationConfiguration<TestSaga>(correlators);
		var message = new TestOrderMessage { OrderId = "order-1" };

		// Act
		var found = config.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("order-1");
	}

	[Fact]
	public void TryGetCorrelationId_ShouldReturnFalse_WhenMessageTypeIsNotRegistered()
	{
		// Arrange
		var correlators = new Dictionary<Type, Func<object, string>>();
		var config = new SagaCorrelationConfiguration<TestSaga>(correlators);
		var message = new TestOrderMessage { OrderId = "order-1" };

		// Act
		var found = config.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeFalse();
		correlationId.ShouldBeNull();
	}

	[Fact]
	public void TryGetCorrelationId_ShouldThrow_WhenMessageIsNull()
	{
		// Arrange
		var config = new SagaCorrelationConfiguration<TestSaga>(new Dictionary<Type, Func<object, string>>());

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => config.TryGetCorrelationId(null!, out _));
	}

	[Fact]
	public void RuleCount_ShouldReflectRegisteredRules()
	{
		// Arrange
		var correlators = new Dictionary<Type, Func<object, string>>
		{
			[typeof(TestOrderMessage)] = _ => "1",
			[typeof(TestPaymentMessage)] = _ => "2",
		};
		var config = new SagaCorrelationConfiguration<TestSaga>(correlators);

		// Assert
		config.RuleCount.ShouldBe(2);
	}

	[Fact]
	public void RuleCount_ShouldBeZero_WhenNoRulesRegistered()
	{
		// Arrange
		var config = new SagaCorrelationConfiguration<TestSaga>(new Dictionary<Type, Func<object, string>>());

		// Assert
		config.RuleCount.ShouldBe(0);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenCorrelatorsIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => new SagaCorrelationConfiguration<TestSaga>(null!));
	}

	[Fact]
	public void ShouldSupportMultipleMessageTypes()
	{
		// Arrange
		var correlators = new Dictionary<Type, Func<object, string>>
		{
			[typeof(TestOrderMessage)] = msg => ((TestOrderMessage)msg).OrderId,
			[typeof(TestPaymentMessage)] = msg => ((TestPaymentMessage)msg).PaymentRef,
		};
		var config = new SagaCorrelationConfiguration<TestSaga>(correlators);

		// Act
		config.TryGetCorrelationId(new TestOrderMessage { OrderId = "A" }, out var id1);
		config.TryGetCorrelationId(new TestPaymentMessage { PaymentRef = "B" }, out var id2);

		// Assert
		id1.ShouldBe("A");
		id2.ShouldBe("B");
	}

	#region Test Types

	private sealed class TestSaga;

	private sealed class TestOrderMessage
	{
		public string OrderId { get; set; } = string.Empty;
	}

	private sealed class TestPaymentMessage
	{
		public string PaymentRef { get; set; } = string.Empty;
	}

	#endregion
}
