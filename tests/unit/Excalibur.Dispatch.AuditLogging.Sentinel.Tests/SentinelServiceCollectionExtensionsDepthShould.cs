// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Sentinel;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Sentinel.Tests;

/// <summary>
/// Depth coverage tests for <see cref="SentinelServiceCollectionExtensions"/>
/// covering fluent chaining, ValidateOnStart, singleton registration, and HttpClient.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SentinelServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		var result = services.AddSentinelAuditExporter(o =>
		{
			o.WorkspaceId = "test-ws";
			o.SharedKey = "dGVzdC1rZXk=";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterValidateOnStartOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		services.AddSentinelAuditExporter(o =>
		{
			o.WorkspaceId = "test-ws";
			o.SharedKey = "dGVzdC1rZXk=";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IValidateOptions<SentinelExporterOptions>));
	}

	[Fact]
	public void RegisterExporterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
#pragma warning disable IL2026, IL3050
		services.AddSentinelAuditExporter(o =>
		{
			o.WorkspaceId = "test-ws";
			o.SharedKey = "dGVzdC1rZXk=";
		});
#pragma warning restore IL2026, IL3050

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
#pragma warning disable IL2026, IL3050
		services.AddSentinelAuditExporter(o =>
		{
			o.WorkspaceId = "test-ws";
			o.SharedKey = "dGVzdC1rZXk=";
		});
#pragma warning restore IL2026, IL3050

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHttpClientFactory));
	}
}
