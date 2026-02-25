// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MetricDataShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var data = new MetricData();

		// Assert
		data.Name.ShouldBe(string.Empty);
		data.Value.ShouldBe(0.0);
		data.Unit.ShouldBe(MetricUnit.None);
		data.Timestamp.ShouldNotBe(default);
		data.Dimensions.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var data = new MetricData
		{
			Name = "MessagesSent",
			Value = 42.0,
			Unit = MetricUnit.Count,
			Timestamp = now,
		};
		data.Dimensions["env"] = "prod";
		data.Dimensions["region"] = "us-east-1";

		// Assert
		data.Name.ShouldBe("MessagesSent");
		data.Value.ShouldBe(42.0);
		data.Unit.ShouldBe(MetricUnit.Count);
		data.Timestamp.ShouldBe(now);
		data.Dimensions.Count.ShouldBe(2);
		data.Dimensions["env"].ShouldBe("prod");
	}

	[Fact]
	public void SupportAllMetricUnits()
	{
		// Assert — verify all enum values exist and cast correctly
		((int)MetricUnit.None).ShouldBe(0);
		((int)MetricUnit.Count).ShouldBe(1);
		((int)MetricUnit.Bytes).ShouldBe(2);
		((int)MetricUnit.Milliseconds).ShouldBe(15);
		((int)MetricUnit.Percent).ShouldBe(12);
		((int)MetricUnit.Seconds).ShouldBe(13);
		((int)MetricUnit.CountPerSecond).ShouldBe(26);
	}
}
