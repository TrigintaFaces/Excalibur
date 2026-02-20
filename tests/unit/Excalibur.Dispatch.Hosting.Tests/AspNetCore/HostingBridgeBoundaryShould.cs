// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.AspNetCore.Builder;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore;

/// <summary>
/// Guard rail tests for Excalibur.Dispatch.Hosting.AspNetCore boundary enforcement.
/// These tests ensure the hosting bridge stays minimal and doesn't drift into a full hosting stack.
/// Per AD-326-2 (Sprint 326) and ADR-078 (Dispatch-Excalibur Boundary).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class HostingBridgeBoundaryShould : UnitTestBase
{
	private static readonly Assembly HostingAssembly = typeof(WebApplicationBuilderExtensions).Assembly;

	private static readonly HashSet<string> AllowedTypeNames = new(StringComparer.Ordinal)
	{
		"WebApplicationBuilderExtensions",
		"EndpointRouteBuilderExtensions",
		"RouteMessageHandlerFactory",
		"HttpContextExtensions",
		"MessageResultExtensions",
		"ControllerBaseExtensions",
		"DispatcherWebExtensions",
		"DispatchAspNetCoreAuthorizationExtensions",
		"AspNetCoreAuthorizationMiddleware",
		"AspNetCoreAuthorizationOptions"
	};

	private static readonly string[] ForbiddenAssemblyPatterns =
	[
		"Swashbuckle",
		"NSwag",
		"Microsoft.OpenApi",
		"Asp.Versioning",
		"Microsoft.AspNetCore.Mvc.Versioning",
		"OpenTelemetry",
		"Excalibur.Domain",
		"Excalibur.EventSourcing",
		"Excalibur.Data",
		"Excalibur.Saga",
		"Excalibur.Application",
		"Azure.Security.KeyVault"
	];

	private static readonly string[] ForbiddenNamespacePatterns =
	[
		"OpenApi",
		"Swagger",
		"Versioning",
		"HealthChecks",
		"OpenTelemetry",
		"Excalibur.Domain",
		"Excalibur.EventSourcing",
		"Excalibur.Data",
		"Excalibur.Saga",
		"KeyVault",
		"Compliance"
	];

	[Fact]
	public void NotReference_ForbiddenAssemblies()
	{
		// Arrange
		var referencedAssemblies = HostingAssembly.GetReferencedAssemblies();

		// Act
		var violations = referencedAssemblies
			.Where(a => ForbiddenAssemblyPatterns.Any(pattern =>
				a.Name?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true))
			.Select(a => a.Name)
			.ToList();

		// Assert
		violations.ShouldBeEmpty(
			$"Excalibur.Dispatch.Hosting.AspNetCore should only contain dispatcher wiring, " +
			$"not advanced hosting features. Found forbidden assemblies: {string.Join(", ", violations)}");
	}

	[Fact]
	public void OnlyExpose_AllowedPublicTypes()
	{
		// Arrange
		var publicTypes = HostingAssembly.GetExportedTypes();

		// Act
		var unexpectedTypes = publicTypes
			.Where(t => !AllowedTypeNames.Contains(t.Name))
			.Select(t => t.FullName)
			.ToList();

		// Assert
		unexpectedTypes.ShouldBeEmpty(
			$"Found unexpected public types in Excalibur.Dispatch.Hosting.AspNetCore. " +
			$"Only allowed: [{string.Join(", ", AllowedTypeNames)}]. " +
			$"Found: {string.Join(", ", unexpectedTypes)}");
	}

	[Fact]
	public void NotContain_ForbiddenNamespaces()
	{
		// Arrange
		var allTypes = HostingAssembly.GetTypes();

		// Act
		var violations = allTypes
			.Where(t => ForbiddenNamespacePatterns.Any(pattern =>
				t.Namespace?.Contains(pattern, StringComparison.OrdinalIgnoreCase) == true))
			.Select(t => t.FullName)
			.ToList();

		// Assert
		violations.ShouldBeEmpty(
			$"Excalibur.Dispatch.Hosting.AspNetCore should not contain types in forbidden namespaces. " +
			$"Found: {string.Join(", ", violations)}");
	}

	[Fact]
	public void HaveCorrectAssemblyName()
	{
		// Assert
		HostingAssembly.GetName().Name.ShouldBe("Excalibur.Dispatch.Hosting.AspNetCore",
			"Assembly name must be Excalibur.Dispatch.Hosting.AspNetCore (not Excalibur.*)");
	}

	[Fact]
	public void OnlyDependOn_AllowedFrameworkAssemblies()
	{
		// Arrange
		var referencedAssemblies = HostingAssembly.GetReferencedAssemblies();

		var allowedPrefixes = new[]
		{
			"Excalibur.Dispatch",
			"Microsoft.AspNetCore",
			"Microsoft.Extensions",
			"Microsoft.Net.Http",
			"System",
			"netstandard",
			"mscorlib",
			"Microsoft.CSharp"
		};

		// Act
		var unexpectedDependencies = referencedAssemblies
			.Where(a => !allowedPrefixes.Any(prefix =>
				a.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true))
			.Select(a => a.Name)
			.ToList();

		// Assert
		unexpectedDependencies.ShouldBeEmpty(
			$"Excalibur.Dispatch.Hosting.AspNetCore should only depend on Dispatch and ASP.NET Core framework assemblies. " +
			$"Found unexpected dependencies: {string.Join(", ", unexpectedDependencies)}");
	}

	[Theory]
	[InlineData("WebApplicationBuilderExtensions")]
	[InlineData("EndpointRouteBuilderExtensions")]
	[InlineData("RouteMessageHandlerFactory")]
	[InlineData("HttpContextExtensions")]
	[InlineData("MessageResultExtensions")]
	[InlineData("ControllerBaseExtensions")]
	[InlineData("DispatcherWebExtensions")]
	[InlineData("DispatchAspNetCoreAuthorizationExtensions")]
	[InlineData("AspNetCoreAuthorizationMiddleware")]
	[InlineData("AspNetCoreAuthorizationOptions")]
	public void ContainAllowed_Type(string typeName)
	{
		// Arrange
		var publicTypes = HostingAssembly.GetExportedTypes();

		// Act
		var typeExists = publicTypes.Any(t => t.Name == typeName);

		// Assert
		typeExists.ShouldBeTrue(
			$"Expected type '{typeName}' should exist in Excalibur.Dispatch.Hosting.AspNetCore as per AD-326-2");
	}

	[Fact]
	public void Have_TenPublicTypes()
	{
		// Arrange
		var expectedCount = 10;

		// Act
		var actualCount = HostingAssembly.GetExportedTypes().Length;

		// Assert
		actualCount.ShouldBe(expectedCount,
			$"Excalibur.Dispatch.Hosting.AspNetCore should have exactly {expectedCount} public types per AD-326-2. " +
			$"Adding new public types requires architectural review.");
	}
}
