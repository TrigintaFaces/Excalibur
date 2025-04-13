using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Meter;
using App.Metrics.Timer;

using FakeItEasy;

namespace Excalibur.Tests.Fakes;

public static class MetricsFakes
{
	public static IMetrics Metrics => MetricsFake.Value;

	private static Lazy<IMetrics> MetricsFake { get; } = new(() =>
	{
		var fakeMeasure = A.Fake<IMeasureMetrics>();

		var fakeTimer = A.Fake<IMeasureTimerMetrics>();
		_ = A.CallTo(() => fakeMeasure.Timer).Returns(fakeTimer);

		var fakeCounter = A.Fake<IMeasureCounterMetrics>();
		_ = A.CallTo(() => fakeMeasure.Counter).Returns(fakeCounter);

		var fakeMeter = A.Fake<IMeasureMeterMetrics>();
		_ = A.CallTo(() => fakeMeasure.Meter).Returns(fakeMeter);

		var fakeMetrics = A.Fake<IMetrics>();
		_ = A.CallTo(() => fakeMetrics.Measure).Returns(fakeMeasure);

		return fakeMetrics;
	});
}
