// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

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
}
