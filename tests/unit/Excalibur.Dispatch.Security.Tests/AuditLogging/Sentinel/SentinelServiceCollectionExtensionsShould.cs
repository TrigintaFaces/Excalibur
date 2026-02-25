// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Sentinel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Sentinel;

/// <summary>
/// Unit tests for <see cref="SentinelServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SentinelServiceCollectionExtensionsShould
{
	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_RegistersRequiredServices()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "test-workspace-id";
			options.SharedKey = Convert.ToBase64String(new byte[32]);
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var exporter = provider.GetService<IAuditLogExporter>();
		_ = exporter.ShouldNotBeNull();
		_ = exporter.ShouldBeOfType<SentinelAuditExporter>();
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "my-workspace";
			options.SharedKey = Convert.ToBase64String(new byte[32]);
			options.LogType = "CustomAudit";
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SentinelExporterOptions>>().Value;
		options.WorkspaceId.ShouldBe("my-workspace");
		options.LogType.ShouldBe("CustomAudit");
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_ThrowsOnNullServices()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			SentinelServiceCollectionExtensions.AddSentinelAuditExporter(null!, _ => { }));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddSentinelAuditExporter(null!));
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		var result = services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "test-workspace";
			options.SharedKey = Convert.ToBase64String(new byte[32]);
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public void AddSentinelAuditExporter_ConfiguresHttpClientTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedTimeout = TimeSpan.FromSeconds(42);

		// Act
		_ = services.AddSentinelAuditExporter(options =>
		{
			options.WorkspaceId = "test-workspace";
			options.SharedKey = Convert.ToBase64String(new byte[32]);
			options.Timeout = expectedTimeout;
		});
		using var provider = services.BuildServiceProvider();

		// Assert - resolve HttpClient through the factory to trigger the config lambda
		var factory = provider.GetRequiredService<IHttpClientFactory>();
		using var client = factory.CreateClient(nameof(SentinelAuditExporter));
		client.Timeout.ShouldBe(expectedTimeout);
	}
}
