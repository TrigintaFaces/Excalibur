// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ErrorModelsShould
{
	[Fact]
	public void ErrorDetailHaveCorrectDefaults()
	{
		// Arrange & Act
		var detail = new ErrorDetail { Message = "test error" };

		// Assert
		detail.Timestamp.ShouldNotBe(default);
		detail.Message.ShouldBe("test error");
		detail.ErrorType.ShouldBeNull();
		detail.StackTrace.ShouldBeNull();
		detail.Context.ShouldBeEmpty();
	}

	[Fact]
	public void ErrorDetailAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var detail = new ErrorDetail
		{
			Timestamp = now,
			Message = "Connection refused",
			ErrorType = "System.Net.Sockets.SocketException",
			StackTrace = "at MyService.ProcessAsync()",
		};
		detail.Context["endpoint"] = "sqs.us-east-1.amazonaws.com";

		// Assert
		detail.Timestamp.ShouldBe(now);
		detail.Message.ShouldBe("Connection refused");
		detail.ErrorType.ShouldBe("System.Net.Sockets.SocketException");
		detail.StackTrace.ShouldBe("at MyService.ProcessAsync()");
		detail.Context.Count.ShouldBe(1);
	}

	[Fact]
	public void ErrorMetadataHaveCorrectDefaults()
	{
		// Arrange & Act
		var metadata = new ErrorMetadata();

		// Assert
		metadata.Source.ShouldBeNull();
		metadata.Category.ShouldBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void ErrorMetadataAllowSettingAllProperties()
	{
		// Arrange & Act
		var metadata = new ErrorMetadata
		{
			Source = "SqsReceiver",
			Category = "Transport",
		};
		metadata.Properties["retryable"] = true;

		// Assert
		metadata.Source.ShouldBe("SqsReceiver");
		metadata.Category.ShouldBe("Transport");
		metadata.Properties.Count.ShouldBe(1);
	}

	[Fact]
	public void ErrorStatisticsHaveCorrectDefaults()
	{
		// Arrange & Act
		var stats = new ErrorStatistics();

		// Assert
		stats.MessageId.ShouldBe(string.Empty);
		stats.ErrorCount.ShouldBe(0);
		stats.FirstError.ShouldBe(default);
		stats.LastError.ShouldBe(default);
		stats.ErrorTypes.ShouldBeEmpty();
	}

	[Fact]
	public void ErrorStatisticsAllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new ErrorStatistics
		{
			MessageId = "msg-1",
			ErrorCount = 5,
			FirstError = now.AddHours(-2),
			LastError = now,
		};
		stats.ErrorTypes.Add("TimeoutException");
		stats.ErrorTypes.Add("OperationCanceledException");

		// Assert
		stats.MessageId.ShouldBe("msg-1");
		stats.ErrorCount.ShouldBe(5);
		stats.FirstError.ShouldBe(now.AddHours(-2));
		stats.LastError.ShouldBe(now);
		stats.ErrorTypes.Count.ShouldBe(2);
	}
}
