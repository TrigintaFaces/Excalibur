// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LongPollingSnapshotShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var snapshot = new LongPollingSnapshot();

		// Assert
		snapshot.MessagesReceived.ShouldBe(0);
		snapshot.EmptyPolls.ShouldBe(0);
		snapshot.Errors.ShouldBe(0);
		snapshot.MessageRate.ShouldBe(0.0);
		snapshot.EmptyPollRate.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		// Arrange & Act
		var snapshot = new LongPollingSnapshot
		{
			MessagesReceived = 100,
			EmptyPolls = 5,
			Errors = 2,
			MessageRate = 50.5,
			EmptyPollRate = 0.05,
		};

		// Assert
		snapshot.MessagesReceived.ShouldBe(100);
		snapshot.EmptyPolls.ShouldBe(5);
		snapshot.Errors.ShouldBe(2);
		snapshot.MessageRate.ShouldBe(50.5);
		snapshot.EmptyPollRate.ShouldBe(0.05);
	}

	[Fact]
	public void SupportEqualityForEqualSnapshots()
	{
		// Arrange
		var a = new LongPollingSnapshot { MessagesReceived = 10, EmptyPolls = 1, Errors = 0, MessageRate = 5.0, EmptyPollRate = 0.1 };
		var b = new LongPollingSnapshot { MessagesReceived = 10, EmptyPolls = 1, Errors = 0, MessageRate = 5.0, EmptyPollRate = 0.1 };

		// Act & Assert
		a.Equals(b).ShouldBeTrue();
		(a == b).ShouldBeTrue();
		(a != b).ShouldBeFalse();
	}

	[Fact]
	public void SupportInequalityForDifferentSnapshots()
	{
		// Arrange
		var a = new LongPollingSnapshot { MessagesReceived = 10 };
		var b = new LongPollingSnapshot { MessagesReceived = 20 };

		// Act & Assert
		a.Equals(b).ShouldBeFalse();
		(a == b).ShouldBeFalse();
		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void SupportObjectEquals()
	{
		// Arrange
		var a = new LongPollingSnapshot { MessagesReceived = 10 };
		object b = new LongPollingSnapshot { MessagesReceived = 10 };

		// Act & Assert
		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForObjectEqualsWithDifferentType()
	{
		// Arrange
		var a = new LongPollingSnapshot();

		// Act & Assert
		a.Equals("not a snapshot").ShouldBeFalse();
	}

	[Fact]
	public void ReturnConsistentHashCode()
	{
		// Arrange
		var a = new LongPollingSnapshot { MessagesReceived = 10, EmptyPolls = 1 };
		var b = new LongPollingSnapshot { MessagesReceived = 10, EmptyPolls = 1 };

		// Act & Assert
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}
}
