// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Timing;

/// <summary>
/// Depth tests for TimePolicyOptionsValidator.
/// Covers all cross-property validation rules: DefaultTimeout vs MaxTimeout,
/// HandlerTimeout, TransportTimeout, SerializationTimeout, ValidationTimeout,
/// ComplexityMultiplier, and custom/message/handler timeout dictionaries.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimePolicyOptionsValidatorShould
{
	private readonly TimePolicyOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new TimePolicyOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			_validator.Validate(null, null!));
	}

	[Fact]
	public void FailWhenDefaultTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DefaultTimeout");
	}

	[Fact]
	public void FailWhenDefaultTimeoutEqualsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(5),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenHandlerTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			HandlerTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("HandlerTimeout");
	}

	[Fact]
	public void FailWhenTransportTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			TransportTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("TransportTimeout");
	}

	[Fact]
	public void FailWhenSerializationTimeoutExceedsHandlerTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			SerializationTimeout = TimeSpan.FromMinutes(3),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("SerializationTimeout");
	}

	[Fact]
	public void FailWhenSerializationTimeoutEqualsHandlerTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			SerializationTimeout = TimeSpan.FromMinutes(2),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenValidationTimeoutExceedsHandlerTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			ValidationTimeout = TimeSpan.FromMinutes(3),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ValidationTimeout");
	}

	[Fact]
	public void FailWhenComplexityMultiplierExceedsHeavyOperationMultiplier()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			ComplexityMultiplier = 5.0,
			HeavyOperationMultiplier = 3.0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("ComplexityMultiplier");
	}

	[Fact]
	public void SucceedWhenComplexityMultiplierEqualsHeavyOperationMultiplier()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			ComplexityMultiplier = 3.0,
			HeavyOperationMultiplier = 3.0,
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert — ComplexityMultiplier <= HeavyOperationMultiplier is OK
		result.FailureMessage?.ShouldNotContain("ComplexityMultiplier");
	}

	[Fact]
	public void FailWhenCustomTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			CustomTimeouts = new Dictionary<TimeoutOperationType, TimeSpan>
			{
				[TimeoutOperationType.Handler] = TimeSpan.FromMinutes(10),
			},
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Custom timeout");
	}

	[Fact]
	public void FailWhenMessageTypeTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			MessageTypeTimeouts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
			{
				["MyApp.OrderCommand"] = TimeSpan.FromMinutes(10),
			},
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Message type timeout");
	}

	[Fact]
	public void FailWhenHandlerTypeTimeoutExceedsMaxTimeout()
	{
		// Arrange
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			HandlerTypeTimeouts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
			{
				["MyApp.OrderHandler"] = TimeSpan.FromMinutes(10),
			},
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Handler type timeout");
	}

	[Fact]
	public void CollectMultipleFailures()
	{
		// Arrange — violate multiple constraints
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
			HandlerTimeout = TimeSpan.FromMinutes(10),
			TransportTimeout = TimeSpan.FromMinutes(10),
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DefaultTimeout");
		result.FailureMessage.ShouldContain("HandlerTimeout");
		result.FailureMessage.ShouldContain("TransportTimeout");
	}
}
