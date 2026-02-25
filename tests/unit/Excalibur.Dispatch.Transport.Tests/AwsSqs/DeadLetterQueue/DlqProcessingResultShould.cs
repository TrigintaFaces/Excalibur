// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqProcessingResultShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var result = new DlqProcessingResult { MessageId = "msg-1" };

		// Assert
		result.Success.ShouldBeFalse();
		result.MessageId.ShouldBe("msg-1");
		result.Action.ShouldBe(DlqAction.None);
		result.ErrorMessage.ShouldBeNull();
		result.ProcessedAt.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		result.RetryAttempts.ShouldBe(0);
		result.Metadata.ShouldNotBeNull();
		result.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var processedAt = DateTimeOffset.UtcNow;

		// Act
		var result = new DlqProcessingResult
		{
			Success = true,
			MessageId = "msg-42",
			Action = DlqAction.Redriven,
			ErrorMessage = null,
			ProcessedAt = processedAt,
			RetryAttempts = 3,
		};
		result.Metadata["queue"] = "source-queue";

		// Assert
		result.Success.ShouldBeTrue();
		result.MessageId.ShouldBe("msg-42");
		result.Action.ShouldBe(DlqAction.Redriven);
		result.ErrorMessage.ShouldBeNull();
		result.ProcessedAt.ShouldBe(processedAt);
		result.RetryAttempts.ShouldBe(3);
		result.Metadata["queue"].ShouldBe("source-queue");
	}

	[Fact]
	public void SupportFailedProcessingResult()
	{
		// Arrange & Act
		var result = new DlqProcessingResult
		{
			Success = false,
			MessageId = "msg-99",
			Action = DlqAction.RetryFailed,
			ErrorMessage = "Connection timeout",
			RetryAttempts = 5,
		};

		// Assert
		result.Success.ShouldBeFalse();
		result.Action.ShouldBe(DlqAction.RetryFailed);
		result.ErrorMessage.ShouldBe("Connection timeout");
	}
}
