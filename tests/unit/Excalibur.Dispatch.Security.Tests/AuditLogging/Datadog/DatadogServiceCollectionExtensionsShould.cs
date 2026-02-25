// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Datadog;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Datadog;

/// <summary>
/// Unit tests for <see cref="DatadogServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DatadogServiceCollectionExtensionsShould
{
	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_RegistersRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "test-api-key";
			options.Site = "datadoghq.com";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var exporter = provider.GetService<IAuditLogExporter>();
		_ = exporter.ShouldNotBeNull();
		_ = exporter.ShouldBeOfType<DatadogAuditExporter>();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "my-api-key";
			options.Site = "us3.datadoghq.com";
			options.Service = "my-service";
			options.Source = "my-source";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<DatadogExporterOptions>>().Value;
		options.ApiKey.ShouldBe("my-api-key");
		options.Site.ShouldBe("us3.datadoghq.com");
		options.Service.ShouldBe("my-service");
		options.Source.ShouldBe("my-source");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			DatadogServiceCollectionExtensions.AddDatadogAuditExporter(null!, _ => { }));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddDatadogAuditExporter(null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "test-key";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddDatadogAuditExporter_ConfiguresHttpClientTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedTimeout = TimeSpan.FromSeconds(42);

		// Act
		_ = services.AddDatadogAuditExporter(options =>
		{
			options.ApiKey = "test-api-key";
			options.Site = "datadoghq.com";
			options.Timeout = expectedTimeout;
		});
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient through the factory to trigger the config lambda
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(DatadogAuditExporter));
		client.Timeout.ShouldBe(expectedTimeout);
	}
}
