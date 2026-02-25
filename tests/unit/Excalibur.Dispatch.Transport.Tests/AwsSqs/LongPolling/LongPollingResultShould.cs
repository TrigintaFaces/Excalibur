// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class LongPollingResultShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var result = new LongPollingResult();

		// Assert
		result.MessageCount.ShouldBe(0);
		result.ElapsedTime.ShouldBe(TimeSpan.Zero);
		result.IsEmpty.ShouldBeTrue();
		result.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void ReportIsEmptyWhenMessageCountIsZero()
	{
		// Arrange & Act
		var result = new LongPollingResult { MessageCount = 0 };

		// Assert
		result.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void ReportNotEmptyWhenMessageCountIsPositive()
	{
		// Arrange & Act
		var result = new LongPollingResult { MessageCount = 5 };

		// Assert
		result.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var result = new LongPollingResult
		{
			MessageCount = 10,
			ElapsedTime = TimeSpan.FromSeconds(5),
			Timestamp = now,
		};

		// Assert
		result.MessageCount.ShouldBe(10);
		result.ElapsedTime.ShouldBe(TimeSpan.FromSeconds(5));
		result.Timestamp.ShouldBe(now);
	}
}
