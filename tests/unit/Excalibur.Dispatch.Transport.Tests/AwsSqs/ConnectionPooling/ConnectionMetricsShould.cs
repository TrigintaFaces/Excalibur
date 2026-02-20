// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.ConnectionPooling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ConnectionMetricsShould : IDisposable
{
	private readonly ConnectionMetrics _metrics = new("test");

	[Fact]
	public void StartWithZeroValues()
	{
		// Assert
		_metrics.GetHitRate().ShouldBe(0.0);
		_metrics.GetAverageAcquisitionTime().ShouldBe(TimeSpan.Zero);
		_metrics.GetErrorCount().ShouldBe(0);
	}

	[Fact]
	public void RecordAcquisitionAndTrackHitRate()
	{
		// Act
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(10), hit: true);
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(20), hit: false);

		// Assert
		_metrics.GetHitRate().ShouldBe(0.5);
	}

	[Fact]
	public void CalculateAverageAcquisitionTime()
	{
		// Arrange & Act
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(10), hit: true);
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(30), hit: true);

		// Assert
		_metrics.GetAverageAcquisitionTime().ShouldBe(TimeSpan.FromMilliseconds(20));
	}

	[Fact]
	public void TrackErrors()
	{
		// Act
		_metrics.RecordError();
		_metrics.RecordError();
		_metrics.RecordError();

		// Assert
		_metrics.GetErrorCount().ShouldBe(3);
	}

	[Fact]
	public void HandleAllHitsCorrectly()
	{
		// Act
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(5), hit: true);
		_metrics.RecordAcquisition(TimeSpan.FromMilliseconds(5), hit: true);

		// Assert
		_metrics.GetHitRate().ShouldBe(1.0);
	}

	[Fact]
	public void DisposeWithoutError()
	{
		// Act & Assert — should not throw
		_metrics.Dispose();
	}

	public void Dispose()
	{
		_metrics.Dispose();
	}
}
