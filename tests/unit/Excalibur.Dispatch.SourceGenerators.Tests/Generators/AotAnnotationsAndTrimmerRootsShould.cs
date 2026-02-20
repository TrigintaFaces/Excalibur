// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Xml.Linq;

using Excalibur.Dispatch.TypeResolution;

namespace Excalibur.Dispatch.SourceGenerators.Tests;

/// <summary>
/// Tests for Sprint 521 DynamicallyAccessedMembers annotations and TrimmerRoots.xml fixes.
/// Validates TypeResolver.ResolveType guards, TrimmerRoots assembly names, type fullnames.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AotAnnotationsAndTrimmerRootsShould
{
	#region TrimmerRoots.xml Assembly Name Tests (S521.6)

	[Fact]
	public void TrimmerRoots_ShouldUseCorrectAbstractionsAssemblyName()
	{
		// TrimmerRoots.xml was fixed from "Dispatch.Abstractions" to "Excalibur.Dispatch.Abstractions"
		var doc = LoadTrimmerRoots();
		var assemblies = doc.Descendants("assembly")
			.Select(a => a.Attribute("fullname")?.Value)
			.ToList();

		assemblies.ShouldContain("Excalibur.Dispatch.Abstractions");
		// Should not have bare "Dispatch.Abstractions" (which would be wrong assembly name)
		assemblies.ShouldNotContain("Dispatch.Abstractions");
	}

	[Fact]
	public void TrimmerRoots_ShouldUseCorrectDispatchAssemblyName()
	{
		// TrimmerRoots.xml was fixed from "Dispatch" to "Excalibur.Dispatch"
		var doc = LoadTrimmerRoots();
		var assemblies = doc.Descendants("assembly")
			.Select(a => a.Attribute("fullname")?.Value)
			.ToList();

		assemblies.ShouldContain("Excalibur.Dispatch");
		// Should not have bare "Dispatch" (which would be wrong assembly name)
		assemblies.ShouldNotContain("Dispatch");
	}

	[Fact]
	public void TrimmerRoots_TypeFullnames_ShouldUseExcaliburPrefix()
	{
		// All type fullnames should use Excalibur.Dispatch.* not Excalibur.Dispatch.*
		var doc = LoadTrimmerRoots();
		var typeNames = doc.Descendants("type")
			.Select(t => t.Attribute("fullname")?.Value)
			.Where(n => n != null && !n.StartsWith("System", StringComparison.Ordinal))
			.ToList();

		foreach (var typeName in typeNames)
		{
			typeName.ShouldStartWith("Excalibur.Dispatch");
		}
	}

	[Fact]
	public void TrimmerRoots_ShouldPreserveHandlerInterfaces()
	{
		// Handler interfaces should be preserved: IActionHandler, IEventHandler, IDocumentHandler
		var doc = LoadTrimmerRoots();
		var typeNames = doc.Descendants("type")
			.Select(t => t.Attribute("fullname")?.Value)
			.ToList();

		typeNames.ShouldContain(n => n != null && n.Contains("IActionHandler"));
		typeNames.ShouldContain(n => n != null && n.Contains("IEventHandler"));
		typeNames.ShouldContain(n => n != null && n.Contains("IDocumentHandler"));
	}

	[Fact]
	public void TrimmerRoots_ShouldPreserveHandlerInterfacesInDeliveryNamespace()
	{
		// Handler interfaces are in Excalibur.Dispatch.Abstractions.Delivery (not .Delivery.Handlers)
		var doc = LoadTrimmerRoots();
		var handlerTypes = doc.Descendants("type")
			.Select(t => t.Attribute("fullname")?.Value)
			.Where(n => n != null && (n.Contains("IActionHandler") || n.Contains("IEventHandler") || n.Contains("IDocumentHandler")))
			.ToList();

		foreach (var typeName in handlerTypes)
		{
			typeName.ShouldContain("Excalibur.Dispatch.Abstractions.Delivery.I");
		}
	}

	[Fact]
	public void TrimmerRoots_ShouldPreservePrecompiledHandlerRegistry()
	{
		// PrecompiledHandlerRegistry should be preserved for AOT
		var doc = LoadTrimmerRoots();
		var typeNames = doc.Descendants("type")
			.Select(t => t.Attribute("fullname")?.Value)
			.ToList();

		typeNames.ShouldContain("Excalibur.Dispatch.Delivery.Handlers.PrecompiledHandlerRegistry");
	}

	[Fact]
	public void TrimmerRoots_ShouldPreserveResultFactoryRegistry()
	{
		// ResultFactoryRegistry should be preserved (was phantom "MessageFactoryRegistry" before fix)
		var doc = LoadTrimmerRoots();
		var typeNames = doc.Descendants("type")
			.Select(t => t.Attribute("fullname")?.Value)
			.ToList();

		typeNames.ShouldContain("Excalibur.Dispatch.Delivery.Handlers.ResultFactoryRegistry");
		typeNames.ShouldNotContain("Excalibur.Dispatch.Delivery.Handlers.MessageFactoryRegistry");
	}

	[Fact]
	public void TrimmerRoots_ShouldPreserveMiddlewareNamespace()
	{
		// Middleware namespace should be preserved with correct Excalibur prefix
		var doc = LoadTrimmerRoots();
		var namespaces = doc.Descendants("namespace")
			.Select(n => n.Attribute("fullname")?.Value)
			.ToList();

		namespaces.ShouldContain("Excalibur.Dispatch.Middleware");
	}

	#endregion

	#region TypeResolver Tests (S521.5)

	[Fact]
	public void TypeResolver_ResolveType_ShouldReturnNullForNullInput()
	{
		// TypeResolver.ResolveType should handle null gracefully
		var result = TypeResolver.ResolveType(null!);
		result.ShouldBeNull();
	}

	[Fact]
	public void TypeResolver_ResolveType_ShouldReturnNullForEmptyString()
	{
		// TypeResolver.ResolveType should handle empty string gracefully
		var result = TypeResolver.ResolveType("");
		result.ShouldBeNull();
	}

	[Fact]
	public void TypeResolver_ResolveType_ShouldResolveKnownSystemTypes()
	{
		// In JIT mode, Type.GetType fallback should resolve well-known types
		var result = TypeResolver.ResolveType("System.String");
		_ = result.ShouldNotBeNull();
		result.ShouldBe(typeof(string));
	}

	[Fact]
	public void TypeResolver_ResolveTypeRequired_ShouldThrowForUnknownType()
	{
		// ResolveTypeRequired should throw TypeLoadException for unknown types
		Should.Throw<TypeLoadException>(() => TypeResolver.ResolveTypeRequired("NonExistent.Type.That.DoesNotExist"));
	}

	[Fact]
	public void TypeResolver_ResolveTypeRuntime_ShouldReturnNullForNullInput()
	{
		var result = TypeResolver.ResolveTypeRuntime(null!);
		result.ShouldBeNull();
	}

	[Fact]
	public void TypeResolver_ResolveTypeRuntime_ShouldReturnNullForEmptyString()
	{
		var result = TypeResolver.ResolveTypeRuntime("");
		result.ShouldBeNull();
	}

	#endregion

	#region Helper Methods

	private static XDocument LoadTrimmerRoots()
	{
		// Find the TrimmerRoots.xml file relative to test execution
		var basePath = FindProjectRoot();
		var trimmerRootsPath = Path.Combine(basePath, "src", "Dispatch", "Excalibur.Dispatch", "TrimmerRoots.xml");

		if (!File.Exists(trimmerRootsPath))
		{
			throw new FileNotFoundException($"TrimmerRoots.xml not found at: {trimmerRootsPath}");
		}

		return XDocument.Load(trimmerRootsPath);
	}

	private static string FindProjectRoot()
	{
		// Walk up from the test output directory to find the repo root
		var dir = AppDomain.CurrentDomain.BaseDirectory;
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
			{
				return dir;
			}

			dir = Directory.GetParent(dir)?.FullName;
		}

		// Fallback: try user profile path (cross-platform safe)
		var userProfileCandidate = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Excalibur.Dispatch");

		if (Directory.Exists(userProfileCandidate))
		{
			return userProfileCandidate;
		}

		throw new DirectoryNotFoundException("Could not find project root directory. " +
			"Ensure the test runs from within the repository or set the working directory to the repo root.");
	}

	#endregion
}
