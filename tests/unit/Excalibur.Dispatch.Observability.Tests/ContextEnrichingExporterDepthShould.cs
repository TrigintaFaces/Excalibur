// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Deep coverage tests for <see cref="ContextEnrichingExporter"/> covering
/// enricher resolution, context accessor integration, null enricher path,
/// and null context accessor scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextEnrichingExporterDepthShould
{
	[Fact]
	public void AlwaysReturnSuccess_EvenWithEmptyBatch()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(services);

		// Act
		var result = exporter.Export(default);

		// Assert
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void AlwaysReturnSuccess_WhenNoEnricherRegistered()
	{
		// Arrange — no IContextTraceEnricher in DI
		var services = new ServiceCollection().BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(services);

		// Act
		var result = exporter.Export(default);

		// Assert — skips enrichment but still returns success
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void AlwaysReturnSuccess_WhenEnricherRegisteredButNoContextAccessor()
	{
		// Arrange — enricher present but no IMessageContextAccessor
		var sc = new ServiceCollection();
		sc.AddSingleton(A.Fake<IContextTraceEnricher>());
		var sp = sc.BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(sp);

		// Act
		var result = exporter.Export(default);

		// Assert — no context accessor means no enrichment, but still success
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void AlwaysReturnSuccess_WhenContextAccessorHasNullContext()
	{
		// Arrange — context accessor registered but MessageContext is null
		var sc = new ServiceCollection();
		sc.AddSingleton(A.Fake<IContextTraceEnricher>());
		var accessor = A.Fake<IMessageContextAccessor>();
		A.CallTo(() => accessor.MessageContext).Returns(null);
		sc.AddSingleton(accessor);
		var sp = sc.BuildServiceProvider();
		using var exporter = new ContextEnrichingExporter(sp);

		// Act
		var result = exporter.Export(default);

		// Assert — null MessageContext skips enrichment
		result.ShouldBe(ExportResult.Success);
	}

	[Fact]
	public void ImplementBaseExporterOfActivity()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();

		// Act
		using var exporter = new ContextEnrichingExporter(sp);

		// Assert
		exporter.ShouldBeAssignableTo<BaseExporter<Activity>>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(ContextEnrichingExporter).IsNotPublic.ShouldBeTrue();
		typeof(ContextEnrichingExporter).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void DisposeCleanly()
	{
		// Arrange
		var sp = new ServiceCollection().BuildServiceProvider();
		var exporter = new ContextEnrichingExporter(sp);

		// Act & Assert — should not throw
		exporter.Dispose();
		exporter.Dispose(); // Idempotent
	}
}
