// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Timing;

/// <summary>
/// Unit tests for <see cref="TimeoutOperationType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Timing")]
[Trait("Priority", "0")]
public sealed class TimeoutOperationTypeShould
{
	#region Enum Value Tests

	[Fact]
	public void Default_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Default).ShouldBe(0);
	}

	[Fact]
	public void Handler_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Handler).ShouldBe(1);
	}

	[Fact]
	public void Serialization_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Serialization).ShouldBe(2);
	}

	[Fact]
	public void Transport_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Transport).ShouldBe(3);
	}

	[Fact]
	public void Validation_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Validation).ShouldBe(4);
	}

	[Fact]
	public void Middleware_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Middleware).ShouldBe(5);
	}

	[Fact]
	public void Pipeline_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Pipeline).ShouldBe(6);
	}

	[Fact]
	public void Outbox_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Outbox).ShouldBe(7);
	}

	[Fact]
	public void Inbox_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Inbox).ShouldBe(8);
	}

	[Fact]
	public void Scheduling_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Scheduling).ShouldBe(9);
	}

	[Fact]
	public void Database_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Database).ShouldBe(10);
	}

	[Fact]
	public void Http_HasExpectedValue()
	{
		// Assert
		((int)TimeoutOperationType.Http).ShouldBe(11);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<TimeoutOperationType>();

		// Assert
		values.ShouldContain(TimeoutOperationType.Default);
		values.ShouldContain(TimeoutOperationType.Handler);
		values.ShouldContain(TimeoutOperationType.Serialization);
		values.ShouldContain(TimeoutOperationType.Transport);
		values.ShouldContain(TimeoutOperationType.Validation);
		values.ShouldContain(TimeoutOperationType.Middleware);
		values.ShouldContain(TimeoutOperationType.Pipeline);
		values.ShouldContain(TimeoutOperationType.Outbox);
		values.ShouldContain(TimeoutOperationType.Inbox);
		values.ShouldContain(TimeoutOperationType.Scheduling);
		values.ShouldContain(TimeoutOperationType.Database);
		values.ShouldContain(TimeoutOperationType.Http);
	}

	[Fact]
	public void HasExactlyTwelveValues()
	{
		// Arrange
		var values = Enum.GetValues<TimeoutOperationType>();

		// Assert
		values.Length.ShouldBe(12);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(TimeoutOperationType.Default, "Default")]
	[InlineData(TimeoutOperationType.Handler, "Handler")]
	[InlineData(TimeoutOperationType.Serialization, "Serialization")]
	[InlineData(TimeoutOperationType.Transport, "Transport")]
	[InlineData(TimeoutOperationType.Validation, "Validation")]
	[InlineData(TimeoutOperationType.Middleware, "Middleware")]
	[InlineData(TimeoutOperationType.Pipeline, "Pipeline")]
	[InlineData(TimeoutOperationType.Outbox, "Outbox")]
	[InlineData(TimeoutOperationType.Inbox, "Inbox")]
	[InlineData(TimeoutOperationType.Scheduling, "Scheduling")]
	[InlineData(TimeoutOperationType.Database, "Database")]
	[InlineData(TimeoutOperationType.Http, "Http")]
	public void ToString_ReturnsExpectedValue(TimeoutOperationType type, string expected)
	{
		// Act & Assert
		type.ToString().ShouldBe(expected);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsDefault()
	{
		// Arrange
		TimeoutOperationType type = default;

		// Assert
		type.ShouldBe(TimeoutOperationType.Default);
	}

	#endregion
}
