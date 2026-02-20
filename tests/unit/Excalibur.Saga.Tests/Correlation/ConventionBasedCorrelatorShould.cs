// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Correlation;

/// <summary>
/// Unit tests for <see cref="ConventionBasedCorrelator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ConventionBasedCorrelatorShould
{
	private readonly ConventionBasedCorrelator _sut = new();

	#region Attribute-Based Correlation

	[Fact]
	public void TryGetCorrelationId_ShouldResolveAttributeDecoratedProperty()
	{
		// Arrange
		var message = new AttributeCorrelatedMessage { OrderId = "order-123", Name = "Test" };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("order-123");
	}

	[Fact]
	public void TryGetCorrelationId_ShouldPreferAttributeOverConvention()
	{
		// Arrange — message has both [SagaMessageCorrelation] and CorrelationId property
		var message = new AttributeAndConventionMessage
		{
			CustomId = "custom-id",
			CorrelationId = "convention-id",
		};

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("custom-id");
	}

	#endregion

	#region Convention-Based Correlation (SagaId)

	[Fact]
	public void TryGetCorrelationId_ShouldResolveSagaIdProperty()
	{
		// Arrange
		var message = new SagaIdMessage { SagaId = "saga-456" };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("saga-456");
	}

	#endregion

	#region Convention-Based Correlation (CorrelationId)

	[Fact]
	public void TryGetCorrelationId_ShouldResolveCorrelationIdProperty()
	{
		// Arrange
		var message = new CorrelationIdMessage { CorrelationId = "corr-789" };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("corr-789");
	}

	[Fact]
	public void TryGetCorrelationId_ShouldPreferSagaIdOverCorrelationId()
	{
		// Arrange — SagaId comes first in convention order
		var message = new BothConventionMessage { SagaId = "saga-1", CorrelationId = "corr-1" };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeTrue();
		correlationId.ShouldBe("saga-1");
	}

	#endregion

	#region No Match

	[Fact]
	public void TryGetCorrelationId_ShouldReturnFalse_WhenNoPropertyMatches()
	{
		// Arrange
		var message = new NoCorrelationMessage { Name = "Test", Value = 42 };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeFalse();
		correlationId.ShouldBeNull();
	}

	[Fact]
	public void TryGetCorrelationId_ShouldReturnFalse_WhenPropertyValueIsNull()
	{
		// Arrange
		var message = new SagaIdMessage { SagaId = null! };

		// Act
		var found = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		found.ShouldBeFalse();
	}

	#endregion

	#region Null Argument

	[Fact]
	public void TryGetCorrelationId_ShouldThrow_WhenMessageIsNull()
	{
		Should.Throw<ArgumentNullException>(
			() => _sut.TryGetCorrelationId(null!, out _));
	}

	#endregion

	#region Caching

	[Fact]
	public void TryGetCorrelationId_ShouldCacheAccessorAcrossCalls()
	{
		// Arrange
		var message1 = new SagaIdMessage { SagaId = "id-1" };
		var message2 = new SagaIdMessage { SagaId = "id-2" };

		// Act — both calls hit the same cached accessor
		_sut.TryGetCorrelationId(message1, out var result1);
		_sut.TryGetCorrelationId(message2, out var result2);

		// Assert
		result1.ShouldBe("id-1");
		result2.ShouldBe("id-2");
	}

	#endregion

	#region Test Message Types

	private sealed class AttributeCorrelatedMessage
	{
		[SagaMessageCorrelation]
		public string OrderId { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;
	}

	private sealed class AttributeAndConventionMessage
	{
		[SagaMessageCorrelation]
		public string CustomId { get; set; } = string.Empty;

		public string CorrelationId { get; set; } = string.Empty;
	}

	private sealed class SagaIdMessage
	{
		public string SagaId { get; set; } = string.Empty;
	}

	private sealed class CorrelationIdMessage
	{
		public string CorrelationId { get; set; } = string.Empty;
	}

	private sealed class BothConventionMessage
	{
		public string SagaId { get; set; } = string.Empty;

		public string CorrelationId { get; set; } = string.Empty;
	}

	private sealed class NoCorrelationMessage
	{
		public string Name { get; set; } = string.Empty;

		public int Value { get; set; }
	}

	#endregion
}
