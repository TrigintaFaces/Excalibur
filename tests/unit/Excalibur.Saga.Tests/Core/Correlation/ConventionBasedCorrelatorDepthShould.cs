// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Core.Correlation;

// Test message types for correlator testing
public sealed class MessageWithSagaId
{
	public string SagaId { get; set; } = "saga-123";
	public string Data { get; set; } = "test";
}

public sealed class MessageWithCorrelationId
{
	public string CorrelationId { get; set; } = "corr-456";
	public string Data { get; set; } = "test";
}

public sealed class MessageWithAttribute
{
	[SagaMessageCorrelation]
	public string CustomId { get; set; } = "attr-789";

	public string Data { get; set; } = "test";
}

public sealed class MessageWithAttributeAndConvention
{
	[SagaMessageCorrelation]
	public string CustomId { get; set; } = "attr-value";

	public string SagaId { get; set; } = "convention-value";
}

public sealed class MessageWithNoCorrelation
{
	public string Data { get; set; } = "test";
	public int Count { get; set; } = 42;
}

public sealed class MessageWithNullSagaId
{
	public string? SagaId { get; set; }
}

public sealed class MessageWithIntSagaId
{
	public int SagaId { get; set; } = 123;
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConventionBasedCorrelatorDepthShould
{
	private readonly ConventionBasedCorrelator _sut = new();

	[Fact]
	public void ThrowWhenMessageIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.TryGetCorrelationId(null!, out _));
	}

	[Fact]
	public void CorrelateUsingSagaIdConvention()
	{
		// Arrange
		var message = new MessageWithSagaId { SagaId = "saga-123" };

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeTrue();
		correlationId.ShouldBe("saga-123");
	}

	[Fact]
	public void CorrelateUsingCorrelationIdConvention()
	{
		// Arrange
		var message = new MessageWithCorrelationId { CorrelationId = "corr-456" };

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeTrue();
		correlationId.ShouldBe("corr-456");
	}

	[Fact]
	public void CorrelateUsingAttribute()
	{
		// Arrange
		var message = new MessageWithAttribute { CustomId = "attr-789" };

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeTrue();
		correlationId.ShouldBe("attr-789");
	}

	[Fact]
	public void PreferAttributeOverConvention()
	{
		// Arrange
		var message = new MessageWithAttributeAndConvention
		{
			CustomId = "attr-value",
			SagaId = "convention-value",
		};

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeTrue();
		correlationId.ShouldBe("attr-value");
	}

	[Fact]
	public void ReturnFalseWhenNoCorrelationPropertyExists()
	{
		// Arrange
		var message = new MessageWithNoCorrelation();

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeFalse();
		correlationId.ShouldBeNull();
	}

	[Fact]
	public void ReturnFalseWhenSagaIdPropertyIsNull()
	{
		// Arrange
		var message = new MessageWithNullSagaId { SagaId = null };

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeFalse();
		correlationId.ShouldBeNull();
	}

	[Fact]
	public void IgnoreNonStringConventionProperties()
	{
		// Arrange - SagaId is int, not string
		var message = new MessageWithIntSagaId { SagaId = 123 };

		// Act
		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		// Assert
		result.ShouldBeFalse();
		correlationId.ShouldBeNull();
	}

	[Fact]
	public void CacheAccessorBetweenCalls()
	{
		// Arrange
		var message1 = new MessageWithSagaId { SagaId = "first" };
		var message2 = new MessageWithSagaId { SagaId = "second" };

		// Act
		_sut.TryGetCorrelationId(message1, out var id1);
		_sut.TryGetCorrelationId(message2, out var id2);

		// Assert - both should resolve correctly (caching works)
		id1.ShouldBe("first");
		id2.ShouldBe("second");
	}

	[Fact]
	public void HandleDifferentMessageTypes()
	{
		// Arrange
		var sagaMsg = new MessageWithSagaId { SagaId = "saga-id" };
		var corrMsg = new MessageWithCorrelationId { CorrelationId = "corr-id" };
		var attrMsg = new MessageWithAttribute { CustomId = "attr-id" };
		var noMsg = new MessageWithNoCorrelation();

		// Act & Assert
		_sut.TryGetCorrelationId(sagaMsg, out var id1).ShouldBeTrue();
		id1.ShouldBe("saga-id");

		_sut.TryGetCorrelationId(corrMsg, out var id2).ShouldBeTrue();
		id2.ShouldBe("corr-id");

		_sut.TryGetCorrelationId(attrMsg, out var id3).ShouldBeTrue();
		id3.ShouldBe("attr-id");

		_sut.TryGetCorrelationId(noMsg, out _).ShouldBeFalse();
	}

	[Fact]
	public void SagaIdTakesPriorityOverCorrelationId()
	{
		// When a class has both SagaId and CorrelationId, SagaId comes first in convention order
		var message = new MessageWithBothConventions
		{
			SagaId = "saga-first",
			CorrelationId = "corr-second",
		};

		var result = _sut.TryGetCorrelationId(message, out var correlationId);

		result.ShouldBeTrue();
		correlationId.ShouldBe("saga-first");
	}
}

public sealed class MessageWithBothConventions
{
	public string SagaId { get; set; } = string.Empty;
	public string CorrelationId { get; set; } = string.Empty;
}
