// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Timing;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Timing;

/// <summary>
///     Tests for the <see cref="DefaultTimePolicy" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultTimePolicyShould
{
	private static IOptions<TimePolicyOptions> CreateOptions(Action<TimePolicyOptions>? configure = null)
	{
		var options = new TimePolicyOptions();
		configure?.Invoke(options);
		return Microsoft.Extensions.Options.Options.Create(options);
	}

	[Fact]
	public void CreateWithDefaultOptions()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowForNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new DefaultTimePolicy(null!));
	}

	[Fact]
	public void ExposeDefaultTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ExposeMaxTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.MaxTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void ExposeHandlerTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.HandlerTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ExposeSerializationTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.SerializationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void ExposeTransportTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.TransportTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void ExposeValidationTimeout()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.ValidationTimeout.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void ReturnHandlerTimeoutForHandlerOperationType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutFor(TimeoutOperationType.Handler).ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ReturnSerializationTimeoutForSerializationOperationType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutFor(TimeoutOperationType.Serialization).ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void ReturnTransportTimeoutForTransportOperationType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutFor(TimeoutOperationType.Transport).ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void ReturnValidationTimeoutForValidationOperationType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutFor(TimeoutOperationType.Validation).ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void ReturnDefaultTimeoutForMiddlewareOperationType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutFor(TimeoutOperationType.Middleware).ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ReturnCustomTimeoutWhenConfigured()
	{
		var customTimeout = TimeSpan.FromSeconds(42);
		var sut = new DefaultTimePolicy(CreateOptions(o =>
		{
			o.CustomTimeouts[TimeoutOperationType.Handler] = customTimeout;
		}));

		sut.GetTimeoutFor(TimeoutOperationType.Handler).ShouldBe(customTimeout);
	}

	[Fact]
	public void NotApplyTimeoutWhenEnforceTimeoutsIsDisabled()
	{
		var sut = new DefaultTimePolicy(CreateOptions(o => o.EnforceTimeouts = false));

		sut.ShouldApplyTimeout(TimeoutOperationType.Handler).ShouldBeFalse();
	}

	[Fact]
	public void ApplyTimeoutWhenEnforceTimeoutsIsEnabled()
	{
		var sut = new DefaultTimePolicy(CreateOptions(o => o.EnforceTimeouts = true));

		sut.ShouldApplyTimeout(TimeoutOperationType.Handler).ShouldBeTrue();
	}

	[Fact]
	public void GetTimeoutForMessageType()
	{
		var customTimeout = TimeSpan.FromSeconds(99);
		var sut = new DefaultTimePolicy(CreateOptions(o =>
		{
			o.MessageTypeTimeouts[typeof(string).FullName!] = customTimeout;
		}));

		sut.GetTimeoutForMessage(TimeoutOperationType.Handler, typeof(string)).ShouldBe(customTimeout);
	}

	[Fact]
	public void FallBackToOperationTimeoutForUnknownMessageType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.GetTimeoutForMessage(TimeoutOperationType.Handler, typeof(string))
			.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ThrowForNullMessageType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		Should.Throw<ArgumentNullException>(() =>
			sut.GetTimeoutForMessage(TimeoutOperationType.Handler, null!));
	}

	[Fact]
	public void GetTimeoutForHandlerType()
	{
		var customTimeout = TimeSpan.FromSeconds(77);
		var sut = new DefaultTimePolicy(CreateOptions(o =>
		{
			o.HandlerTypeTimeouts[typeof(string).FullName!] = customTimeout;
		}));

		sut.GetTimeoutForHandler(TimeoutOperationType.Handler, typeof(string)).ShouldBe(customTimeout);
	}

	[Fact]
	public void ThrowForNullHandlerType()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		Should.Throw<ArgumentNullException>(() =>
			sut.GetTimeoutForHandler(TimeoutOperationType.Handler, null!));
	}

	[Fact]
	public void ThrowForNullContextOnGetTimeoutForContext()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		Should.Throw<ArgumentNullException>(() =>
			sut.GetTimeoutForContext(TimeoutOperationType.Handler, null!));
	}

	[Fact]
	public void ImplementITimePolicy()
	{
		var sut = new DefaultTimePolicy(CreateOptions());

		sut.ShouldBeAssignableTo<ITimePolicy>();
	}
}
