using System.Diagnostics.Metrics;

using Excalibur.Domain.Metrics;

namespace Excalibur.Tests.Domain.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class OpenTelemetryMetricsExtendedShould : IDisposable
{
	private readonly TestMeterFactory _meterFactory = new();

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new OpenTelemetryMetrics(null!));
	}

	[Fact]
	public void RecordCounter_WithoutTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert — should not throw
		metrics.RecordCounter("test.counter", 5);
	}

	[Fact]
	public void RecordCounter_WithTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);
		var tags = new[]
		{
			new KeyValuePair<string, object>("environment", "test"),
			new KeyValuePair<string, object>("service", "api"),
		};

		// Act & Assert — should not throw
		metrics.RecordCounter("test.counter.tagged", 1, tags);
	}

	[Fact]
	public void RecordCounter_ThrowsOnNullName()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => metrics.RecordCounter(null!, 1));
	}

	[Fact]
	public void RecordGauge_WithoutTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert — should not throw
		metrics.RecordGauge("test.gauge", 42.5);
	}

	[Fact]
	public void RecordGauge_WithTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);
		var tags = new[] { new KeyValuePair<string, object>("region", "us-east-1") };

		// Act & Assert — should not throw
		metrics.RecordGauge("test.gauge.tagged", 99.9, tags);
	}

	[Fact]
	public void RecordGauge_ThrowsOnNullName()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => metrics.RecordGauge(null!, 1.0));
	}

	[Fact]
	public void RecordHistogram_WithoutTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert — should not throw
		metrics.RecordHistogram("test.histogram", 123.45);
	}

	[Fact]
	public void RecordHistogram_WithTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);
		var tags = new[] { new KeyValuePair<string, object>("method", "GET") };

		// Act & Assert — should not throw
		metrics.RecordHistogram("test.histogram.tagged", 200.0, tags);
	}

	[Fact]
	public void RecordHistogram_ThrowsOnNullName()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => metrics.RecordHistogram(null!, 1.0));
	}

	[Fact]
	public void ReuseInstruments_ForSameName()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act — record multiple times with same name, should reuse instrument
		metrics.RecordCounter("reused.counter", 1);
		metrics.RecordCounter("reused.counter", 2);
		metrics.RecordCounter("reused.counter", 3);

		// Assert — no exception means reuse worked
	}

	[Fact]
	public void RecordCounter_WithEmptyTags()
	{
		// Arrange
		var metrics = new OpenTelemetryMetrics(_meterFactory);

		// Act & Assert — empty array should take no-tags path
		metrics.RecordCounter("test.empty.tags", 1, []);
	}

	public void Dispose() => _meterFactory.Dispose();

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options.Name ?? "test", options.Version);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters)
			{
				meter.Dispose();
			}

			_meters.Clear();
		}
	}
}
