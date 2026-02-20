// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.AwsSqs;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MetricDatumShould
{
	[Fact]
	public void StoreAllConstructorParameters()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var dims = new Dictionary<string, string> { ["queue"] = "test-queue" };

		// Act
		var datum = new MetricDatum("MessagesSent", 42.0, "Count", now, dims);

		// Assert
		datum.MetricName.ShouldBe("MessagesSent");
		datum.Value.ShouldBe(42.0);
		datum.Unit.ShouldBe("Count");
		datum.Timestamp.ShouldBe(now);
		datum.Dimensions.ShouldNotBeNull();
		datum.Dimensions!.Count.ShouldBe(1);
	}

	[Fact]
	public void AllowNullDimensions()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var datum = new MetricDatum("Latency", 15.5, "Milliseconds", now);

		// Assert
		datum.MetricName.ShouldBe("Latency");
		datum.Value.ShouldBe(15.5);
		datum.Unit.ShouldBe("Milliseconds");
		datum.Dimensions.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var datum1 = new MetricDatum("Count", 1.0, "Count", now);
		var datum2 = new MetricDatum("Count", 1.0, "Count", now);

		// Assert
		datum1.ShouldBe(datum2);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var datum1 = new MetricDatum("Count", 1.0, "Count", now);
		var datum2 = new MetricDatum("Count", 2.0, "Count", now);

		// Assert
		datum1.ShouldNotBe(datum2);
	}
}
