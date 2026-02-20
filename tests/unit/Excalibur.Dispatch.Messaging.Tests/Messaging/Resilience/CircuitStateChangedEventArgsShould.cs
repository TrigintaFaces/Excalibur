// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
///     Tests for the <see cref="CircuitStateChangedEventArgs" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CircuitStateChangedEventArgsShould
{
	[Fact]
	public void SetPreviousState()
	{
		var sut = new CircuitStateChangedEventArgs
		{
			PreviousState = CircuitState.Closed,
		};

		sut.PreviousState.ShouldBe(CircuitState.Closed);
	}

	[Fact]
	public void SetNewState()
	{
		var sut = new CircuitStateChangedEventArgs
		{
			NewState = CircuitState.Open,
		};

		sut.NewState.ShouldBe(CircuitState.Open);
	}

	[Fact]
	public void HaveDefaultTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var sut = new CircuitStateChangedEventArgs();
		var after = DateTimeOffset.UtcNow;

		sut.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		sut.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowCustomTimestamp()
	{
		var customTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var sut = new CircuitStateChangedEventArgs
		{
			Timestamp = customTime,
		};

		sut.Timestamp.ShouldBe(customTime);
	}

	[Fact]
	public void HaveNullCircuitNameByDefault()
	{
		var sut = new CircuitStateChangedEventArgs();

		sut.CircuitName.ShouldBeNull();
	}

	[Fact]
	public void SetCircuitName()
	{
		var sut = new CircuitStateChangedEventArgs
		{
			CircuitName = "RabbitMQ",
		};

		sut.CircuitName.ShouldBe("RabbitMQ");
	}

	[Fact]
	public void HaveNullTriggeringExceptionByDefault()
	{
		var sut = new CircuitStateChangedEventArgs();

		sut.TriggeringException.ShouldBeNull();
	}

	[Fact]
	public void SetTriggeringException()
	{
		var exception = new InvalidOperationException("connection failed");
		var sut = new CircuitStateChangedEventArgs
		{
			TriggeringException = exception,
		};

		sut.TriggeringException.ShouldBeSameAs(exception);
	}

	[Fact]
	public void InheritFromEventArgs()
	{
		var sut = new CircuitStateChangedEventArgs();

		sut.ShouldBeAssignableTo<EventArgs>();
	}

	[Fact]
	public void RepresentFullStateTransition()
	{
		var exception = new TimeoutException("timed out");
		var sut = new CircuitStateChangedEventArgs
		{
			PreviousState = CircuitState.Closed,
			NewState = CircuitState.Open,
			CircuitName = "Kafka",
			TriggeringException = exception,
		};

		sut.PreviousState.ShouldBe(CircuitState.Closed);
		sut.NewState.ShouldBe(CircuitState.Open);
		sut.CircuitName.ShouldBe("Kafka");
		sut.TriggeringException.ShouldBeSameAs(exception);
	}
}
