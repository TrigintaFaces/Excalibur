// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

/// <summary>
/// Depth coverage tests for <see cref="AwsAuditServiceCollectionExtensions"/>
/// covering fluent chaining, ValidateOnStart registration, and options resolution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AwsAuditServiceCollectionExtensionsDepthShould
{
	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
		});

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterValidateOnStartOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
		});

		// Assert — options resolve with configured values (ValidateDataAnnotations removed in Sprint 750 AOT migration)
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<AwsAuditOptions>>().Value;
		options.LogGroupName.ShouldBe("test-group");
		options.Region.ShouldBe("us-east-1");
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterExporterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
		});

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(IAuditLogExporter));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	[RequiresDynamicCode("Test")]
	[RequiresUnreferencedCode("Test")]
	public void RegisterHttpClient()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAwsAuditExporter(o =>
		{
			o.LogGroupName = "test-group";
			o.Region = "us-east-1";
		});

		// Assert — AddHttpClient registers IHttpClientFactory
		services.ShouldContain(sd => sd.ServiceType == typeof(IHttpClientFactory));
	}
}
