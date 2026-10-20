// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Splunk;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Splunk;

/// <summary>
/// Unit tests for <see cref="SplunkServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class SplunkServiceCollectionExtensionsShould
{
	[Fact]
	public void AddSplunkAuditExporter_WithBuilder_RegistersRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(splunk =>
		{
			splunk.HecEndpoint(new Uri("https://splunk.example.com:8088/services/collector"))
			      .HecToken("test-token");
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var exporter = provider.GetService<IAuditLogExporter>();
		_ = exporter.ShouldNotBeNull();
		_ = exporter.ShouldBeOfType<SplunkAuditExporter>();
	}

	[Fact]
	public void AddSplunkAuditExporter_WithBuilder_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSplunkAuditExporter(splunk =>
		{
			splunk.HecEndpoint(new Uri("https://splunk.example.com:8088/services/collector"))
			      .HecToken("my-token")
			      .Index("my-index")
			      .SourceType("my-sourcetype");
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;
		options.Connection.HecToken.ShouldBe("my-token");
		options.Index.ShouldBe("my-index");
		options.SourceType.ShouldBe("my-sourcetype");
	}

	[Fact]
	public void AddSplunkAuditExporter_WithBuilder_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SplunkServiceCollectionExtensions.AddSplunkAuditExporter(null!, _ => { }));
	}

	[Fact]
	public void AddSplunkAuditExporter_WithBuilder_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSplunkAuditExporter((Action<IAuditLoggingSplunkBuilder>)null!));
	}

	[Fact]
	public void AddSplunkAuditExporter_WithBuilder_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddSplunkAuditExporter(splunk =>
		{
			splunk.HecEndpoint(new Uri("https://splunk.example.com:8088/services/collector"))
			      .HecToken("test-token");
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithBindConfiguration_RegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - the overload registers the services even without configuration values
		_ = services.AddSplunkAuditExporter(splunk =>
			splunk.BindConfiguration("Splunk"));

		// Assert - verify descriptors are registered (don't resolve, since required options are missing)
		services.ShouldContain(sd => sd.ServiceType == typeof(IAuditLogExporter));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithBindConfiguration_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddSplunkAuditExporter(splunk =>
			splunk.BindConfiguration("CustomSection"));

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithBindConfiguration_ConfiguresHttpClient()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["Splunk:Connection:HecEndpoint"] = "https://splunk.example.com:8088/services/collector",
			["Splunk:Connection:HecToken"] = "test-token",
			["Splunk:Batch:RequestTimeout"] = "00:00:42",
			["Splunk:Batch:MaxRetryAttempts"] = "2",
			["Splunk:Batch:RetryBaseDelay"] = "00:00:01",
			["Splunk:Connection:ValidateCertificate"] = "true"
		};
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// Act
		_ = services.AddSplunkAuditExporter(splunk =>
			splunk.BindConfiguration("Splunk"));
		using var provider = services.BuildServiceProvider();

		// Microsoft names the standard pipeline after the client and the standard handler identifier.
		// Looking up the client name alone creates unrelated default options, hiding the configured pipeline.
		var resilience = provider
			.GetRequiredService<IOptionsMonitor<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>>()
			.Get(nameof(SplunkAuditExporter) + "-standard");
		resilience.AttemptTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(42));
		resilience.Retry.MaxRetryAttempts.ShouldBe(2);
		resilience.Retry.Delay.ShouldBe(TimeSpan.FromSeconds(1));
		resilience.TotalRequestTimeout.Timeout.ShouldBe(TimeSpan.FromSeconds(126));
		resilience.CircuitBreaker.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(84));

		// The client retains a total deadline so buffering the response body is bounded too.
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		client.Timeout.ShouldBe(TimeSpan.FromSeconds(126));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSplunkAuditExporter_WithBindConfiguration_DisablesCertValidation()
	{
		// Arrange
		var configData = new Dictionary<string, string?>
		{
			["Splunk:Connection:HecEndpoint"] = "https://splunk.example.com:8088/services/collector",
			["Splunk:Connection:HecToken"] = "test-token",
			["Splunk:Connection:ValidateCertificate"] = "false"
		};
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(configData)
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// Act
		_ = services.AddSplunkAuditExporter(splunk =>
			splunk.BindConfiguration("Splunk"));
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient to trigger ConfigurePrimaryHttpMessageHandler with cert validation disabled
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SplunkAuditExporter));
		_ = client.ShouldNotBeNull();

		// Verify options reflect disabled validation
		var options = provider.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;
		options.Connection.ValidateCertificate.ShouldBeFalse();
	}
}
