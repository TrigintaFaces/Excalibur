// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security.Aws;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Aws;

/// <summary>
/// Unit tests for AWS security DI extension methods.
/// Verifies builder-based entry point: AddDispatchSecurityAws(Action&lt;ISecurityAwsBuilder&gt;).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class DispatchSecurityAwsServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDispatchSecurityAws_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAws(_ => { }));
		exception.ParamName.ShouldBe("services");
	}

	[Fact]
	public void AddDispatchSecurityAws_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		var exception = Should.Throw<ArgumentNullException>(
			() => services.AddDispatchSecurityAws((Action<ISecurityAwsBuilder>)null!));
		exception.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void AddDispatchSecurityAws_DoesNotRegisterServices_WhenRegionNotConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act -- builder with no Region call
		_ = services.AddDispatchSecurityAws(_ => { });

		// Assert - No services should be registered when Region is not set
		services.ShouldNotContain(s => s.ServiceType == typeof(ICredentialStore));
		services.ShouldNotContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAws_RegistersCredentialStore_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAws(aws =>
		{
			aws.Region("us-east-1");
		});

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ICredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAws_RegistersWritableCredentialStore_WhenRegionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddDispatchSecurityAws(aws =>
		{
			aws.Region("us-east-1");
		});

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(IWritableCredentialStore));
	}

	[Fact]
	public void AddDispatchSecurityAws_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDispatchSecurityAws(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}
}
