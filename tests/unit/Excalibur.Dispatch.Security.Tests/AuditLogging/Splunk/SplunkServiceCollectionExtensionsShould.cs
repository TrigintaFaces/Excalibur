// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Splunk;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Splunk;

/// <summary>
/// Unit tests for <see cref="SplunkServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SplunkServiceCollectionExtensionsShould
{
	[Fact]
	public void AddSplunkAuditExporter_WithAction_RegistersRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "test-token";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var exporter = provider.GetService<IAuditLogExporter>();
		_ = exporter.ShouldNotBeNull();
		_ = exporter.ShouldBeOfType<SplunkAuditExporter>();
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "my-token";
			options.Index = "my-index";
			options.SourceType = "my-sourcetype";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;
		options.HecToken.ShouldBe("my-token");
		options.Index.ShouldBe("my-index");
		options.SourceType.ShouldBe("my-sourcetype");
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SplunkServiceCollectionExtensions.AddSplunkAuditExporter(null!, _ => { }));
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSplunkAuditExporter((Action<SplunkExporterOptions>)null!));
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "test-token";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ConfiguresDisabledCertificateValidation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "test-token";
			options.ValidateCertificate = false;
		});
		using var provider = services.BuildServiceProvider();

		// Assert - verify options are configured
		var options = provider.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;
		options.ValidateCertificate.ShouldBeFalse();

		// Trigger the HttpClient factory to exercise the ConfigurePrimaryHttpMessageHandler lambda
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		_ = client.ShouldNotBeNull();
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ConfiguresEnabledCertificateValidation()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "test-token";
			options.ValidateCertificate = true; // default, but explicit for coverage
		});
		using var provider = services.BuildServiceProvider();

		// Assert - trigger the HttpClient factory to exercise the handler lambda with cert validation enabled
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		_ = client.ShouldNotBeNull();
	}

	[Fact]
	public void AddSplunkAuditExporter_WithAction_ConfiguresHttpClientTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedTimeout = TimeSpan.FromSeconds(42);

		// Act
		_ = services.AddSplunkAuditExporter(options =>
		{
			options.HecEndpoint = new Uri("https://splunk.example.com:8088/services/collector");
			options.HecToken = "test-token";
			options.RequestTimeout = expectedTimeout;
		});
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient through the factory to trigger the config lambda
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		client.Timeout.ShouldBe(expectedTimeout);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithConfigSection_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - the overload registers the services even without configuration values
		_ = services.AddSplunkAuditExporter("Splunk");

		// Assert - verify descriptors are registered (don't resolve, since required options are missing)
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithConfigSection_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SplunkServiceCollectionExtensions.AddSplunkAuditExporter(null!, "Splunk"));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithConfigSection_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddSplunkAuditExporter("CustomSection");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithConfigSection_ConfiguresHttpClient()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["Splunk:HecEndpoint"] = "https://splunk.example.com:8088/services/collector",
			["Splunk:HecToken"] = "test-token",
			["Splunk:RequestTimeout"] = "00:00:42",
			["Splunk:ValidateCertificate"] = "true"
		};
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// Act
		_ = services.AddSplunkAuditExporter("Splunk");
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient through the factory to trigger the config lambda
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		client.Timeout.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithConfigSection_DisablesCertValidation()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["Splunk:HecEndpoint"] = "https://splunk.example.com:8088/services/collector",
			["Splunk:HecToken"] = "test-token",
			["Splunk:ValidateCertificate"] = "false"
		};
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// Act
		_ = services.AddSplunkAuditExporter("Splunk");
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient to trigger ConfigurePrimaryHttpMessageHandler with cert validation disabled
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		_ = client.ShouldNotBeNull();

		// Verify options reflect disabled validation
		var options = provider.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;
		options.ValidateCertificate.ShouldBeFalse();
	}
}
