// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="DeadLetterReason"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class DeadLetterReasonShould
{
	[Fact]
	public void HaveExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<DeadLetterReason>();

		// Assert
		values.Length.ShouldBe(11);
	}

	[Fact]
	public void MaxRetriesExceeded_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.MaxRetriesExceeded).ShouldBe(0);
	}

	[Fact]
	public void CircuitBreakerOpen_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.CircuitBreakerOpen).ShouldBe(1);
	}

	[Fact]
	public void DeserializationFailed_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.DeserializationFailed).ShouldBe(2);
	}

	[Fact]
	public void HandlerNotFound_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.HandlerNotFound).ShouldBe(3);
	}

	[Fact]
	public void ValidationFailed_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.ValidationFailed).ShouldBe(4);
	}

	[Fact]
	public void ManualRejection_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.ManualRejection).ShouldBe(5);
	}

	[Fact]
	public void MessageExpired_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.MessageExpired).ShouldBe(6);
	}

	[Fact]
	public void AuthorizationFailed_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.AuthorizationFailed).ShouldBe(7);
	}

	[Fact]
	public void UnhandledException_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.UnhandledException).ShouldBe(8);
	}

	[Fact]
	public void PoisonMessage_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.PoisonMessage).ShouldBe(9);
	}

	[Fact]
	public void Unknown_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterReason.Unknown).ShouldBe(99);
	}

	[Fact]
	public void MaxRetriesExceeded_IsDefaultValue()
	{
		// Arrange
		DeadLetterReason defaultReason = default;

		// Assert
		defaultReason.ShouldBe(DeadLetterReason.MaxRetriesExceeded);
	}

	[Theory]
	[InlineData(DeadLetterReason.MaxRetriesExceeded)]
	[InlineData(DeadLetterReason.CircuitBreakerOpen)]
	[InlineData(DeadLetterReason.DeserializationFailed)]
	[InlineData(DeadLetterReason.HandlerNotFound)]
	[InlineData(DeadLetterReason.ValidationFailed)]
	[InlineData(DeadLetterReason.ManualRejection)]
	[InlineData(DeadLetterReason.MessageExpired)]
	[InlineData(DeadLetterReason.AuthorizationFailed)]
	[InlineData(DeadLetterReason.UnhandledException)]
	[InlineData(DeadLetterReason.PoisonMessage)]
	[InlineData(DeadLetterReason.Unknown)]
	public void BeDefinedForAllValues(DeadLetterReason reason)
	{
		// Assert
		Enum.IsDefined(reason).ShouldBeTrue();
	}
}
