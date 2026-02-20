// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Common;

/// <summary>
/// Unit tests for <see cref="ProcessedMessage"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ProcessedMessageShould
{
	[Fact]
	public void HaveEmptyMessageId_ByDefault()
	{
		// Arrange & Act
		var message = new ProcessedMessage();

		// Assert
		message.MessageId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveProcessedAtSetToUtcNow_ByDefault()
	{
		// Arrange
		var before = DateTime.UtcNow;

		// Act
		var message = new ProcessedMessage();

		// Assert
		var after = DateTime.UtcNow;
		message.ProcessedAt.ShouldBeGreaterThanOrEqualTo(before);
		message.ProcessedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveZeroDuration_ByDefault()
	{
		// Arrange & Act
		var message = new ProcessedMessage();

		// Assert
		message.Duration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveFalseSuccess_ByDefault()
	{
		// Arrange & Act
		var message = new ProcessedMessage();

		// Assert
		message.Success.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullErrorMessage_ByDefault()
	{
		// Arrange & Act
		var message = new ProcessedMessage();

		// Assert
		message.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessageId()
	{
		// Arrange
		var message = new ProcessedMessage();

		// Act
		message.MessageId = "msg-123";

		// Assert
		message.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void AllowSettingProcessedAt()
	{
		// Arrange
		var message = new ProcessedMessage();
		var processedAt = DateTime.UtcNow.AddMinutes(-5);

		// Act
		message.ProcessedAt = processedAt;

		// Assert
		message.ProcessedAt.ShouldBe(processedAt);
	}

	[Fact]
	public void AllowSettingDuration()
	{
		// Arrange
		var message = new ProcessedMessage();

		// Act
		message.Duration = TimeSpan.FromMilliseconds(150);

		// Assert
		message.Duration.ShouldBe(TimeSpan.FromMilliseconds(150));
	}

	[Fact]
	public void AllowSettingSuccessToTrue()
	{
		// Arrange
		var message = new ProcessedMessage();

		// Act
		message.Success = true;

		// Assert
		message.Success.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingErrorMessage()
	{
		// Arrange
		var message = new ProcessedMessage();

		// Act
		message.ErrorMessage = "Processing failed due to timeout";

		// Assert
		message.ErrorMessage.ShouldBe("Processing failed due to timeout");
	}

	[Fact]
	public void AllowCreatingSuccessfullyProcessedMessage()
	{
		// Arrange
		var processedAt = DateTime.UtcNow;

		// Act
		var message = new ProcessedMessage
		{
			MessageId = "msg-456",
			ProcessedAt = processedAt,
			Duration = TimeSpan.FromMilliseconds(50),
			Success = true,
			ErrorMessage = null,
		};

		// Assert
		message.MessageId.ShouldBe("msg-456");
		message.ProcessedAt.ShouldBe(processedAt);
		message.Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
		message.Success.ShouldBeTrue();
		message.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void AllowCreatingFailedProcessedMessage()
	{
		// Arrange
		var processedAt = DateTime.UtcNow;

		// Act
		var message = new ProcessedMessage
		{
			MessageId = "msg-789",
			ProcessedAt = processedAt,
			Duration = TimeSpan.FromSeconds(30),
			Success = false,
			ErrorMessage = "Operation timed out",
		};

		// Assert
		message.MessageId.ShouldBe("msg-789");
		message.ProcessedAt.ShouldBe(processedAt);
		message.Duration.ShouldBe(TimeSpan.FromSeconds(30));
		message.Success.ShouldBeFalse();
		message.ErrorMessage.ShouldBe("Operation timed out");
	}
}
