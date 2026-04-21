// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.GoogleCloud.Tests;

/// <summary>
/// Depth coverage tests for <see cref="GoogleCloudServiceCollectionExtensions"/>
/// covering fluent chaining, ValidateOnStart, singleton registration, and HttpClient.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class GoogleCloudServiceCollectionExtensionsDepthShould
{
	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddGoogleCloudAuditExporter(gcp =>
			gcp.ProjectId("test-project"));

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterOptionsWithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudAuditExporter(gcp =>
			gcp.ProjectId("test-project"));

		// Assert -- Sprint 750: explicit GoogleCloudAuditOptionsValidator registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IValidateOptions<GoogleCloudAuditOptions>));
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterExporterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudAuditExporter(gcp =>
			gcp.ProjectId("test-project"));

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(IAuditLogExporter));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterHttpClientFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGoogleCloudAuditExporter(gcp =>
			gcp.ProjectId("test-project"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHttpClientFactory));
	}
}
