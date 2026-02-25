// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.DeadLetter;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DeadLetterFilterShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var filter = new DeadLetterFilter();

		// Assert
		filter.MessageType.ShouldBeNull();
		filter.MinAge.ShouldBeNull();
		filter.MaxAge.ShouldBeNull();
		filter.Reason.ShouldBeNull();
		filter.AttributeFilters.ShouldBeEmpty();
	}

	[Fact]
	public void MatchMessageWithNoFilters()
	{
		// Arrange
		var filter = new DeadLetterFilter();
		var message = new PubSubMessage
		{
			MessageId = "msg-1",
			PublishTime = DateTimeOffset.UtcNow,
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void MatchMessageByMessageType()
	{
		// Arrange
		var filter = new DeadLetterFilter { MessageType = "OrderCreated" };
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow,
			Attributes = new Dictionary<string, string> { ["messageType"] = "OrderCreated" },
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchMessageWithWrongMessageType()
	{
		// Arrange
		var filter = new DeadLetterFilter { MessageType = "OrderCreated" };
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow,
			Attributes = new Dictionary<string, string> { ["messageType"] = "OrderDeleted" },
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void MatchMessageByPoisonReason()
	{
		// Arrange
		var filter = new DeadLetterFilter { Reason = PoisonReason.ProcessingTimeout };
		var message = new PubSubMessage { PublishTime = DateTimeOffset.UtcNow };

		// Act
		var result = filter.Matches(message, PoisonReason.ProcessingTimeout);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchMessageWithWrongPoisonReason()
	{
		// Arrange
		var filter = new DeadLetterFilter { Reason = PoisonReason.ProcessingTimeout };
		var message = new PubSubMessage { PublishTime = DateTimeOffset.UtcNow };

		// Act
		var result = filter.Matches(message, PoisonReason.InvalidFormat);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void MatchMessageByAttributeFilter()
	{
		// Arrange
		var filter = new DeadLetterFilter
		{
			AttributeFilters = new Dictionary<string, string> { ["region"] = "us-east" },
		};
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow,
			Attributes = new Dictionary<string, string> { ["region"] = "us-east" },
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchMessageWithMissingAttribute()
	{
		// Arrange
		var filter = new DeadLetterFilter
		{
			AttributeFilters = new Dictionary<string, string> { ["region"] = "us-east" },
		};
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow,
			Attributes = [],
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void NotMatchMessageTooYoungForMinAge()
	{
		// Arrange
		var filter = new DeadLetterFilter { MinAge = TimeSpan.FromHours(1) };
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow, // Just published, age < 1 hour
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void MatchMessageOldEnoughForMinAge()
	{
		// Arrange
		var filter = new DeadLetterFilter { MinAge = TimeSpan.FromHours(1) };
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow.AddHours(-2), // 2 hours old
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchMessageTooOldForMaxAge()
	{
		// Arrange
		var filter = new DeadLetterFilter { MaxAge = TimeSpan.FromHours(1) };
		var message = new PubSubMessage
		{
			PublishTime = DateTimeOffset.UtcNow.AddHours(-2), // 2 hours old
		};

		// Act
		var result = filter.Matches(message, PoisonReason.Unknown);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullMessage()
	{
		// Arrange
		var filter = new DeadLetterFilter();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => filter.Matches(null!, PoisonReason.Unknown));
	}
}
