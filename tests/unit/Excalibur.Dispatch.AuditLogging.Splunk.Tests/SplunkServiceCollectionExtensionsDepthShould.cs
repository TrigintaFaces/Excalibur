// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.AuditLogging.Splunk.Tests;

/// <summary>
/// Depth coverage tests for <see cref="SplunkServiceCollectionExtensions"/>
/// covering fluent chaining, singleton registration, HttpClient, and config section overload.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SplunkServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSplunkAuditExporter(o =>
		{
			o.HecEndpoint = new Uri("https://splunk.local:8088/services/collector");
			o.HecToken = "test-token";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterExporterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSplunkAuditExporter(o =>
		{
			o.HecEndpoint = new Uri("https://splunk.local:8088/services/collector");
			o.HecToken = "test-token";
		});

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(IAuditLogExporter));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterHttpClientFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSplunkAuditExporter(o =>
		{
			o.HecEndpoint = new Uri("https://splunk.local:8088/services/collector");
			o.HecToken = "test-token";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHttpClientFactory));
	}

	[Fact]
	public void RegisterExporter_ViaConfigSectionOverload()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		var result = services.AddSplunkAuditExporter("SplunkHec");
#pragma warning restore IL2026, IL3050

		// Assert
		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}
}
