// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Unit tests for <see cref="ContextEnrichingExporter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Exporter")]
public sealed class ContextEnrichingExporterShould
{
	[Fact]
	public void ImplementBaseExporter()
	{
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(serviceProvider);
		exporter.ShouldBeAssignableTo<BaseExporter<Activity>>();
	}

	[Fact]
	public void ConstructWithServiceProvider()
	{
		// Arrange
		var serviceProvider = new ServiceCollection().BuildServiceProvider();

		// Act — should not throw
		using var exporter = new ContextEnrichingExporter(serviceProvider);

		// Assert
		exporter.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructWithEnricherRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IContextTraceEnricher>());
		var serviceProvider = services.BuildServiceProvider();

		// Act — should not throw
		using var exporter = new ContextEnrichingExporter(serviceProvider);

		// Assert
		exporter.ShouldNotBeNull();
	}

	[Fact]
	public void ExportEmptyBatchSuccessfully()
	{
		// Arrange
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(serviceProvider);

		// Act — export with an empty/default batch
		var result = exporter.Export(default);

		// Assert
		result.ShouldBe(ExportResult.Success);
	}

	// --- Fail-open guard around a throwing enricher (6ducnt) ---

	[Fact]
	public void ExportSucceedsWhenEnricherThrows()
	{
		// SAFETY: a consumer-supplied enricher that throws must never break the trace export.
		// Arrange
		var enricher = A.Fake<IContextTraceEnricher>();
		A.CallTo(() => enricher.EnrichActivity(A<Activity>._, A<IMessageContext>._))
			.Throws(new InvalidOperationException("enricher boom"));

		var accessor = A.Fake<IMessageContextAccessor>();
		A.CallTo(() => accessor.MessageContext).Returns(A.Fake<IMessageContext>());

		var services = new ServiceCollection();
		services.AddSingleton(enricher);
		services.AddSingleton(accessor);
		var serviceProvider = services.BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(serviceProvider);

		using var activity = new Activity("test").Start();
		var batch = new Batch<Activity>(new[] { activity }, 1);

		// Act — must not throw; export continues unaffected (fail-open).
		var result = exporter.Export(batch);

		// Assert
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void ExportEnrichesActivityWhenEnricherSucceeds()
	{
		// LIVENESS: the guard must not silently swallow all enrichment — a working
		// enricher is still invoked for the activity. (Guards against an inert fail-open.)
		// Arrange
		var enricher = A.Fake<IContextTraceEnricher>();
		var context = A.Fake<IMessageContext>();

		var accessor = A.Fake<IMessageContextAccessor>();
		A.CallTo(() => accessor.MessageContext).Returns(context);

		var services = new ServiceCollection();
		services.AddSingleton(enricher);
		services.AddSingleton(accessor);
		var serviceProvider = services.BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(serviceProvider);

		using var activity = new Activity("test").Start();
		var batch = new Batch<Activity>(new[] { activity }, 1);

		// Act
		var result = exporter.Export(batch);

		// Assert — enrichment actually happened.
		result.ShouldBe(ExportResult.Success);
		A.CallTo(() => enricher.EnrichActivity(activity, context)).MustHaveHappenedOnceExactly();
	}
}
