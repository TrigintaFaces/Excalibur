// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ErrorDetailShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var detail = new ErrorDetail { Message = "Something went wrong" };

		// Assert
		detail.Message.ShouldBe("Something went wrong");
		detail.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		detail.ErrorType.ShouldBeNull();
		detail.StackTrace.ShouldBeNull();
		detail.Context.ShouldNotBeNull();
		detail.Context.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var detail = new ErrorDetail
		{
			Message = "Timeout connecting to SQS",
			Timestamp = timestamp,
			ErrorType = "System.TimeoutException",
			StackTrace = "at Method() in File.cs:line 42",
		};
		detail.Context["queue"] = "my-queue";
		detail.Context["attempt"] = 3;

		// Assert
		detail.Message.ShouldBe("Timeout connecting to SQS");
		detail.Timestamp.ShouldBe(timestamp);
		detail.ErrorType.ShouldBe("System.TimeoutException");
		detail.StackTrace.ShouldBe("at Method() in File.cs:line 42");
		detail.Context["queue"].ShouldBe("my-queue");
		detail.Context["attempt"].ShouldBe(3);
	}
}
