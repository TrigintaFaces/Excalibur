// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Ordering;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class OrderingKeyInfoShould
{
	[Fact]
	public void CreateWithRequiredOrderingKey()
	{
		// Act
		var info = new OrderingKeyInfo { OrderingKey = "key-1" };

		// Assert
		info.OrderingKey.ShouldBe("key-1");
		info.MessageCount.ShouldBe(0);
		info.LastSequence.ShouldBe(0);
		info.ExpectedSequence.ShouldBe(0);
		info.IsFailed.ShouldBeFalse();
		info.FailureReason.ShouldBeNull();
		info.LastActivity.ShouldBe(default);
		info.OutOfSequenceCount.ShouldBe(0);
	}

	[Fact]
	public void SetAllProperties()
	{
		// Arrange
		var lastActivity = DateTimeOffset.UtcNow;

		// Act
		var info = new OrderingKeyInfo
		{
			OrderingKey = "order-key-42",
			MessageCount = 100,
			LastSequence = 99,
			ExpectedSequence = 100,
			IsFailed = true,
			FailureReason = "Processing error",
			LastActivity = lastActivity,
			OutOfSequenceCount = 3,
		};

		// Assert
		info.OrderingKey.ShouldBe("order-key-42");
		info.MessageCount.ShouldBe(100);
		info.LastSequence.ShouldBe(99);
		info.ExpectedSequence.ShouldBe(100);
		info.IsFailed.ShouldBeTrue();
		info.FailureReason.ShouldBe("Processing error");
		info.LastActivity.ShouldBe(lastActivity);
		info.OutOfSequenceCount.ShouldBe(3);
	}
}
