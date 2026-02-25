// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportCapabilities"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportCapabilitiesShould
{
	#region Enum Value Tests

	[Fact]
	public void None_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.None).ShouldBe(0);
	}

	[Fact]
	public void TransportAdapter_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.TransportAdapter).ShouldBe(1);
	}

	[Fact]
	public void MessageBusAdapter_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.MessageBusAdapter).ShouldBe(2);
	}

	[Fact]
	public void Batching_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.Batching).ShouldBe(4);
	}

	[Fact]
	public void Transactions_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.Transactions).ShouldBe(8);
	}

	[Fact]
	public void DeadLetterQueue_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.DeadLetterQueue).ShouldBe(16);
	}

	[Fact]
	public void Priority_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.Priority).ShouldBe(32);
	}

	[Fact]
	public void Scheduling_HasExpectedValue()
	{
		// Assert
		((int)TransportCapabilities.Scheduling).ShouldBe(64);
	}

	[Fact]
	public void All_HasExpectedValue()
	{
		// Assert - sum of all flags: 1 + 2 + 4 + 8 + 16 + 32 + 64 = 127
		((int)TransportCapabilities.All).ShouldBe(127);
	}

	#endregion

	#region Flags Attribute Tests

	[Fact]
	public void HasFlagsAttribute()
	{
		// Assert
		typeof(TransportCapabilities).GetCustomAttributes(typeof(FlagsAttribute), false)
			.ShouldNotBeEmpty();
	}

	#endregion

	#region All Contains Individual Flags Tests

	[Fact]
	public void All_ContainsTransportAdapter()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.TransportAdapter).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsMessageBusAdapter()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.MessageBusAdapter).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsBatching()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.Batching).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsTransactions()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.Transactions).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsDeadLetterQueue()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.DeadLetterQueue).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsPriority()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.Priority).ShouldBeTrue();
	}

	[Fact]
	public void All_ContainsScheduling()
	{
		// Assert
		TransportCapabilities.All.HasFlag(TransportCapabilities.Scheduling).ShouldBeTrue();
	}

	#endregion

	#region Flag Combination Tests

	[Fact]
	public void CanCombineMultipleCapabilities()
	{
		// Arrange
		var combined = TransportCapabilities.Batching |
		               TransportCapabilities.Transactions |
		               TransportCapabilities.DeadLetterQueue;

		// Assert
		combined.HasFlag(TransportCapabilities.Batching).ShouldBeTrue();
		combined.HasFlag(TransportCapabilities.Transactions).ShouldBeTrue();
		combined.HasFlag(TransportCapabilities.DeadLetterQueue).ShouldBeTrue();
		combined.HasFlag(TransportCapabilities.Priority).ShouldBeFalse();
	}

	[Fact]
	public void CombiningAllFlags_EqualsAll()
	{
		// Arrange
		var combined = TransportCapabilities.TransportAdapter |
		               TransportCapabilities.MessageBusAdapter |
		               TransportCapabilities.Batching |
		               TransportCapabilities.Transactions |
		               TransportCapabilities.DeadLetterQueue |
		               TransportCapabilities.Priority |
		               TransportCapabilities.Scheduling;

		// Assert
		combined.ShouldBe(TransportCapabilities.All);
	}

	[Fact]
	public void BitwiseAnd_WithMatchingFlags_ReturnsCommonFlags()
	{
		// Arrange
		var a = TransportCapabilities.Batching | TransportCapabilities.Transactions;
		var b = TransportCapabilities.Transactions | TransportCapabilities.DeadLetterQueue;

		// Act
		var result = a & b;

		// Assert
		result.ShouldBe(TransportCapabilities.Transactions);
	}

	[Fact]
	public void BitwiseXor_ExcludesCommonFlags()
	{
		// Arrange
		var all = TransportCapabilities.All;
		var toExclude = TransportCapabilities.Priority | TransportCapabilities.Scheduling;

		// Act
		var result = all ^ toExclude;

		// Assert
		result.HasFlag(TransportCapabilities.Priority).ShouldBeFalse();
		result.HasFlag(TransportCapabilities.Scheduling).ShouldBeFalse();
		result.HasFlag(TransportCapabilities.Batching).ShouldBeTrue();
	}

	#endregion

	#region String Conversion Tests

	[Fact]
	public void None_ToString_ReturnsNone()
	{
		// Assert
		TransportCapabilities.None.ToString().ShouldBe("None");
	}

	[Fact]
	public void SingleCapability_ToString_ReturnsCapabilityName()
	{
		// Assert
		TransportCapabilities.Batching.ToString().ShouldBe("Batching");
	}

	[Fact]
	public void CombinedCapabilities_ToString_ReturnsCommaSeparatedNames()
	{
		// Arrange
		var combined = TransportCapabilities.Batching | TransportCapabilities.Transactions;

		// Act
		var result = combined.ToString();

		// Assert
		result.ShouldContain("Batching");
		result.ShouldContain("Transactions");
	}

	[Fact]
	public void All_ToString_ReturnsAll()
	{
		// Assert
		TransportCapabilities.All.ToString().ShouldBe("All");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsNone()
	{
		// Arrange
		TransportCapabilities capability = default;

		// Assert
		capability.ShouldBe(TransportCapabilities.None);
	}

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<TransportCapabilities>();

		// Assert
		values.ShouldContain(TransportCapabilities.None);
		values.ShouldContain(TransportCapabilities.TransportAdapter);
		values.ShouldContain(TransportCapabilities.MessageBusAdapter);
		values.ShouldContain(TransportCapabilities.Batching);
		values.ShouldContain(TransportCapabilities.Transactions);
		values.ShouldContain(TransportCapabilities.DeadLetterQueue);
		values.ShouldContain(TransportCapabilities.Priority);
		values.ShouldContain(TransportCapabilities.Scheduling);
		values.ShouldContain(TransportCapabilities.All);
	}

	[Fact]
	public void HasExactlyNineValues()
	{
		// Arrange
		var values = Enum.GetValues<TransportCapabilities>();

		// Assert
		values.Length.ShouldBe(9);
	}

	#endregion
}
