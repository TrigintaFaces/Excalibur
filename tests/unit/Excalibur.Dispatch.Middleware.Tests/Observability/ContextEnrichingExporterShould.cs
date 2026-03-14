// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability;
using Excalibur.Dispatch.Observability.Context;

using OpenTelemetry;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ContextEnrichingExporterShould : IDisposable
{
	private readonly IServiceProvider _fakeServiceProvider;
	private readonly ContextEnrichingExporter _sut;

	public ContextEnrichingExporterShould()
	{
		_fakeServiceProvider = A.Fake<IServiceProvider>();
		_sut = new ContextEnrichingExporter(_fakeServiceProvider);
	}

	[Fact]
	public void ReturnSuccessWhenNoEnricherRegistered()
	{
		// Arrange
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IContextTraceEnricher)))
			.Returns(null);

		using var activitySource = new ActivitySource("Test.Exporter");
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name == "Test.Exporter",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = activitySource.StartActivity("test");
		activity.ShouldNotBeNull();
		activity.Stop();

		// Act - create batch with single activity
		var result = _sut.Export(new Batch<Activity>([activity], 1));

		// Assert
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void ReturnSuccessWhenNoMessageContextAccessor()
	{
		// Arrange
		var enricher = A.Fake<IContextTraceEnricher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IContextTraceEnricher)))
			.Returns(enricher);
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IMessageContextAccessor)))
			.Returns(null);

		using var activitySource = new ActivitySource("Test.Exporter2");
		using var listener = new ActivityListener
		{
			ShouldListenTo = s => s.Name == "Test.Exporter2",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		using var activity = activitySource.StartActivity("test");
		activity.ShouldNotBeNull();
		activity.Stop();

		// Act
		var result = _sut.Export(new Batch<Activity>([activity], 1));

		// Assert
		result.ShouldBe(ExportResult.Success);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}
