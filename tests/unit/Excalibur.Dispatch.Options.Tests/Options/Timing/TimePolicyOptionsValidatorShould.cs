// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Timing;

/// <summary>
/// Verifies cross-property validation in <see cref="TimePolicyOptionsValidator"/>.
/// Sprint 564 S564.55: TimePolicy IValidateOptions tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TimePolicyOptionsValidatorShould
{
	private readonly TimePolicyOptionsValidator _sut = new();

	[Fact]
	public void Succeed_WithValidDefaults()
	{
		var options = new TimePolicyOptions();
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenDefaultTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.DefaultTimeout));
	}

	[Fact]
	public void Fail_WhenDefaultTimeoutEqualsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(5),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.DefaultTimeout));
	}

	[Fact]
	public void Fail_WhenHandlerTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			HandlerTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.HandlerTimeout));
	}

	[Fact]
	public void Fail_WhenTransportTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			TransportTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.TransportTimeout));
	}

	[Fact]
	public void Fail_WhenSerializationTimeoutExceedsHandlerTimeout()
	{
		var options = new TimePolicyOptions
		{
			SerializationTimeout = TimeSpan.FromMinutes(3),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.SerializationTimeout));
	}

	[Fact]
	public void Fail_WhenSerializationTimeoutEqualsHandlerTimeout()
	{
		var options = new TimePolicyOptions
		{
			SerializationTimeout = TimeSpan.FromMinutes(2),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.SerializationTimeout));
	}

	[Fact]
	public void Fail_WhenValidationTimeoutExceedsHandlerTimeout()
	{
		var options = new TimePolicyOptions
		{
			ValidationTimeout = TimeSpan.FromMinutes(3),
			HandlerTimeout = TimeSpan.FromMinutes(2),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.ValidationTimeout));
	}

	[Fact]
	public void Fail_WhenComplexityMultiplierExceedsHeavyOperationMultiplier()
	{
		var options = new TimePolicyOptions
		{
			ComplexityMultiplier = 5.0,
			HeavyOperationMultiplier = 3.0,
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.ComplexityMultiplier));
	}

	[Fact]
	public void Succeed_WhenComplexityMultiplierEqualsHeavyOperationMultiplier()
	{
		var options = new TimePolicyOptions
		{
			ComplexityMultiplier = 3.0,
			HeavyOperationMultiplier = 3.0,
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenCustomTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			CustomTimeouts = new Dictionary<TimeoutOperationType, TimeSpan>
			{
				[TimeoutOperationType.Outbox] = TimeSpan.FromMinutes(10),
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Custom timeout");
	}

	[Fact]
	public void Fail_WhenMessageTypeTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			MessageTypeTimeouts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
			{
				["MyMessage"] = TimeSpan.FromMinutes(10),
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Message type timeout");
	}

	[Fact]
	public void Fail_WhenHandlerTypeTimeoutExceedsMaxTimeout()
	{
		var options = new TimePolicyOptions
		{
			MaxTimeout = TimeSpan.FromMinutes(5),
			HandlerTypeTimeouts = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
			{
				["MyHandler"] = TimeSpan.FromMinutes(10),
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Handler type timeout");
	}

	[Fact]
	public void CollectMultipleFailures()
	{
		var options = new TimePolicyOptions
		{
			DefaultTimeout = TimeSpan.FromMinutes(10),
			MaxTimeout = TimeSpan.FromMinutes(5),
			HandlerTimeout = TimeSpan.FromMinutes(10),
			TransportTimeout = TimeSpan.FromMinutes(10),
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.DefaultTimeout));
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.HandlerTimeout));
		result.FailureMessage.ShouldContain(nameof(TimePolicyOptions.TransportTimeout));
	}
}
