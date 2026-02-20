// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ErrorStatisticsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var stats = new ErrorStatistics();

		// Assert
		stats.MessageId.ShouldBe(string.Empty);
		stats.ErrorCount.ShouldBe(0);
		stats.FirstError.ShouldBe(default);
		stats.LastError.ShouldBe(default);
		stats.ErrorTypes.ShouldNotBeNull();
		stats.ErrorTypes.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new ErrorStatistics
		{
			MessageId = "msg-42",
			ErrorCount = 5,
			FirstError = now.AddHours(-3),
			LastError = now,
		};
		stats.ErrorTypes.Add("TimeoutException");
		stats.ErrorTypes.Add("DeserializationException");

		// Assert
		stats.MessageId.ShouldBe("msg-42");
		stats.ErrorCount.ShouldBe(5);
		stats.FirstError.ShouldBe(now.AddHours(-3));
		stats.LastError.ShouldBe(now);
		stats.ErrorTypes.Count.ShouldBe(2);
		stats.ErrorTypes.ShouldContain("TimeoutException");
		stats.ErrorTypes.ShouldContain("DeserializationException");
	}
}
