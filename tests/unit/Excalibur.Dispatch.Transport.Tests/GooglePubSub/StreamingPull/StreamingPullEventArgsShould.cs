// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.StreamingPull;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class StreamingPullEventArgsShould
{
	[Fact]
	public void CreateAckDeadlineExtensionEventArgs()
	{
		// Act
		var args = new AckDeadlineExtensionEventArgs("ack-123", 30);

		// Assert
		args.AckId.ShouldBe("ack-123");
		args.ExtensionSeconds.ShouldBe(30);
	}

	[Fact]
	public void DeriveAckDeadlineExtensionFromEventArgs()
	{
		// Act
		var args = new AckDeadlineExtensionEventArgs("ack-1", 60);

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}

	[Fact]
	public void CreateMessageEnqueuedEventArgs()
	{
		// Act
		var args = new MessageEnqueuedEventArgs("stream-1", "msg-42");

		// Assert
		args.StreamId.ShouldBe("stream-1");
		args.MessageId.ShouldBe("msg-42");
	}

	[Fact]
	public void DeriveMessageEnqueuedFromEventArgs()
	{
		// Act
		var args = new MessageEnqueuedEventArgs("s", "m");

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}

	[Fact]
	public void CreateMessageProcessedEventArgsForSuccess()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(50);

		// Act
		var args = new MessageProcessedEventArgs("msg-1", true, duration);

		// Assert
		args.MessageId.ShouldBe("msg-1");
		args.Success.ShouldBeTrue();
		args.Duration.ShouldBe(duration);
		args.Error.ShouldBeNull();
	}

	[Fact]
	public void CreateMessageProcessedEventArgsForFailure()
	{
		// Arrange
		var duration = TimeSpan.FromMilliseconds(100);
		var error = new InvalidOperationException("Processing failed");

		// Act
		var args = new MessageProcessedEventArgs("msg-2", false, duration, error);

		// Assert
		args.MessageId.ShouldBe("msg-2");
		args.Success.ShouldBeFalse();
		args.Duration.ShouldBe(duration);
		args.Error.ShouldBe(error);
	}

	[Fact]
	public void DeriveMessageProcessedFromEventArgs()
	{
		// Act
		var args = new MessageProcessedEventArgs("m", true, TimeSpan.Zero);

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}

	[Fact]
	public void CreateBackoffOptionsWithDefaults()
	{
		// Act
		var options = new BackoffOptions();

		// Assert
		options.InitialDelay.ShouldBe(TimeSpan.Zero);
		options.MaxDelay.ShouldBe(TimeSpan.Zero);
		options.Multiplier.ShouldBe(0);
		options.MaxAttempts.ShouldBe(0);
	}

	[Fact]
	public void SetBackoffOptionsProperties()
	{
		// Act
		var options = new BackoffOptions
		{
			InitialDelay = TimeSpan.FromMilliseconds(100),
			MaxDelay = TimeSpan.FromSeconds(30),
			Multiplier = 2.0,
			MaxAttempts = 10,
		};

		// Assert
		options.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
		options.Multiplier.ShouldBe(2.0);
		options.MaxAttempts.ShouldBe(10);
	}
}
