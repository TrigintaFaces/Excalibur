// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- IMeterFactory is DI-managed

using System.Diagnostics.Metrics;

using Excalibur.Domain.Metrics;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Domain.Metrics;

/// <summary>
/// Unit tests for <see cref="OpenTelemetryMetrics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OpenTelemetryMetricsShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IMeterFactory _meterFactory;
	private readonly OpenTelemetryMetrics _metrics;

	public OpenTelemetryMetricsShould()
	{
		var services = new ServiceCollection();
		services.AddMetrics();
		_serviceProvider = services.BuildServiceProvider();
		_meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
		_metrics = new OpenTelemetryMetrics(_meterFactory);
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenMeterFactoryIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new OpenTelemetryMetrics(null!));
	}

	[Fact]
	public void Constructor_CreatesInstance_WithValidMeterFactory()
	{
		// Assert
		_metrics.ShouldNotBeNull();
	}

	#endregion

	#region RecordCounter Tests

	[Fact]
	public void RecordCounter_ThrowsArgumentNullException_WhenNameIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _metrics.RecordCounter(null!, 1));
	}

	[Fact]
	public void RecordCounter_DoesNotThrow_WithValidParameters()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => _metrics.RecordCounter("test_counter", 42));
	}

	[Fact]
	public void RecordCounter_DoesNotThrow_WithTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordCounter(
			"test_counter_tags",
			100,
			new KeyValuePair<string, object>("env", "test"),
			new KeyValuePair<string, object>("service", "api")));
	}

	[Fact]
	public void RecordCounter_DoesNotThrow_WithEmptyTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordCounter(
			"test_counter_empty",
			50,
			Array.Empty<KeyValuePair<string, object>>()));
	}

	[Fact]
	public void RecordCounter_ReusesSameInstrument_ForSameName()
	{
		// Act & Assert - both calls should succeed without throwing
		Should.NotThrow(() =>
		{
			_metrics.RecordCounter("reuse_counter", 10);
			_metrics.RecordCounter("reuse_counter", 20);
		});
	}

	#endregion

	#region RecordGauge Tests

	[Fact]
	public void RecordGauge_ThrowsArgumentNullException_WhenNameIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _metrics.RecordGauge(null!, 1.0));
	}

	[Fact]
	public void RecordGauge_DoesNotThrow_WithValidParameters()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordGauge("test_gauge", 3.14));
	}

	[Fact]
	public void RecordGauge_DoesNotThrow_WithTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordGauge(
			"test_gauge_tags",
			2.718,
			new KeyValuePair<string, object>("region", "us-east")));
	}

	[Fact]
	public void RecordGauge_DoesNotThrow_WithEmptyTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordGauge(
			"test_gauge_empty",
			1.5,
			Array.Empty<KeyValuePair<string, object>>()));
	}

	[Fact]
	public void RecordGauge_ReusesSameInstrument_ForSameName()
	{
		// Act & Assert - both calls should succeed without throwing
		Should.NotThrow(() =>
		{
			_metrics.RecordGauge("reuse_gauge", 10.0);
			_metrics.RecordGauge("reuse_gauge", 20.0);
		});
	}

	#endregion

	#region RecordHistogram Tests

	[Fact]
	public void RecordHistogram_ThrowsArgumentNullException_WhenNameIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _metrics.RecordHistogram(null!, 1.0));
	}

	[Fact]
	public void RecordHistogram_DoesNotThrow_WithValidParameters()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordHistogram("test_histogram", 150.5));
	}

	[Fact]
	public void RecordHistogram_DoesNotThrow_WithTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordHistogram(
			"test_histogram_tags",
			99.9,
			new KeyValuePair<string, object>("operation", "read")));
	}

	[Fact]
	public void RecordHistogram_DoesNotThrow_WithEmptyTags()
	{
		// Act & Assert
		Should.NotThrow(() => _metrics.RecordHistogram(
			"test_histogram_empty",
			25.5,
			Array.Empty<KeyValuePair<string, object>>()));
	}

	[Fact]
	public void RecordHistogram_ReusesSameInstrument_ForSameName()
	{
		// Act & Assert - both calls should succeed without throwing
		Should.NotThrow(() =>
		{
			_metrics.RecordHistogram("reuse_histogram", 50.0);
			_metrics.RecordHistogram("reuse_histogram", 75.0);
		});
	}

	#endregion
}
