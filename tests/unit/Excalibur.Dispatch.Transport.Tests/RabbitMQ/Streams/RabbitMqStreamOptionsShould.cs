// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Streams;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqStreamOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new RabbitMqStreamOptions();

		// Assert
		options.StreamName.ShouldBe(string.Empty);
		options.MaxAge.ShouldBeNull();
		options.MaxLength.ShouldBeNull();
		options.SegmentSize.ShouldBe(500_000_000);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new RabbitMqStreamOptions
		{
			StreamName = "my-stream",
			MaxAge = TimeSpan.FromDays(7),
			MaxLength = 1_000_000_000,
			SegmentSize = 250_000_000,
		};

		// Assert
		options.StreamName.ShouldBe("my-stream");
		options.MaxAge.ShouldBe(TimeSpan.FromDays(7));
		options.MaxLength.ShouldBe(1_000_000_000);
		options.SegmentSize.ShouldBe(250_000_000);
	}
}
