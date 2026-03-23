// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Contracts;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed partial class PublicApiContractShould
{
	/// <summary>
	/// Regex to extract type names from PublicAPI.Shipped.txt entries.
	/// Matches patterns like "Excalibur.Dispatch.Abstractions.IDispatcher" from lines like:
	/// "Excalibur.Dispatch.Abstractions.IDispatcher.DispatchAsync(...)"
	/// </summary>
	[GeneratedRegex(@"^(?:abstract |const |override |readonly |sealed |static |virtual )?(?:Excalibur\.[A-Za-z.]+)(?=\.\w+(?:\(|\.get|\.set| ->| \{))", RegexOptions.Compiled)]
	private static partial Regex TypeNameExtractor();

	private static string GetPublicApiFilePath(string packageName)
	{
		// Navigate from test output to source tree
		var solutionDir = FindSolutionDirectory();
		return Path.Combine(solutionDir, "src", "Dispatch", packageName, "PublicAPI.Shipped.txt");
	}

	private static string FindSolutionDirectory()
	{
		var dir = AppContext.BaseDirectory;
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
			{
				return dir;
			}

			dir = Path.GetDirectoryName(dir);
		}

		throw new InvalidOperationException("Could not find solution directory from " + AppContext.BaseDirectory);
	}

	private static HashSet<string> ExtractTypeNamesFromPublicApi(string filePath)
	{
		var types = new HashSet<string>(StringComparer.Ordinal);

		if (!File.Exists(filePath))
		{
			return types;
		}

		foreach (var line in File.ReadLines(filePath))
		{
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
			{
				continue;
			}

			var match = TypeNameExtractor().Match(line);
			if (match.Success)
			{
				types.Add(match.Value);
			}
		}

		return types;
	}

	[Fact]
	public void HaveAbstractionsPublicApiFilePresent()
	{
		// Arrange
		var filePath = GetPublicApiFilePath("Excalibur.Dispatch.Abstractions");

		// Assert
		File.Exists(filePath).ShouldBeTrue($"PublicAPI.Shipped.txt not found at {filePath}");
	}

	[Fact]
	public void HaveCorePublicApiFilePresent()
	{
		// Arrange
		var filePath = GetPublicApiFilePath("Excalibur.Dispatch");

		// Assert
		File.Exists(filePath).ShouldBeTrue($"PublicAPI.Shipped.txt not found at {filePath}");
	}

	[Fact]
	public void HaveNonEmptyAbstractionsPublicApiFile()
	{
		// Arrange
		var filePath = GetPublicApiFilePath("Excalibur.Dispatch.Abstractions");
		var types = ExtractTypeNamesFromPublicApi(filePath);

		// Assert -- the Abstractions package should have many public types
		types.Count.ShouldBeGreaterThan(10,
			"PublicAPI.Shipped.txt should contain many type declarations");
	}

	[Fact]
	public void ContainKeyAbstractionsTypes()
	{
		// Arrange -- force assembly load via concrete type references
		_ = typeof(IDispatcher);
		_ = typeof(IDispatchMessage);
		_ = typeof(IMessageContext);
		_ = typeof(MessageResult);

		var abstractionsAssembly = typeof(IDispatcher).Assembly;
		var exportedTypes = abstractionsAssembly.GetExportedTypes()
			.Select(t => t.FullName!)
			.ToHashSet(StringComparer.Ordinal);

		// Assert -- key types MUST exist in the assembly
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.IDispatcher");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.IDispatchMessage");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.IMessageContext");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.MessageResult");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.IMessageResult");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.MessageKinds");
		exportedTypes.ShouldContain("Excalibur.Dispatch.Abstractions.DispatchMiddlewareStage");
	}

	[Fact]
	public void VerifyPublicApiTypesExistInAbstractionsAssembly()
	{
		// Arrange
		_ = typeof(IDispatcher); // Force assembly load

		var filePath = GetPublicApiFilePath("Excalibur.Dispatch.Abstractions");
		var abstractionsAssembly = typeof(IDispatcher).Assembly;
		var existingTypes = abstractionsAssembly.GetTypes()
			.Where(t => t.IsPublic || t.IsNestedPublic)
			.Select(t => t.FullName!)
			.ToHashSet(StringComparer.Ordinal);

		// Extract declared type names (lines that look like type declarations, not members)
		var declaredTypes = new HashSet<string>(StringComparer.Ordinal);
		foreach (var line in File.ReadLines(filePath))
		{
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
			{
				continue;
			}

			// Match type declarations: lines ending with type signatures
			// e.g., "Excalibur.Dispatch.Abstractions.IDispatcher" (no parentheses, no ".get", no ".set")
			var typeMatch = TypeDeclarationExtractor().Match(line);
			if (typeMatch.Success)
			{
				declaredTypes.Add(typeMatch.Groups[1].Value);
			}
		}

		// Act -- check for declared types that no longer exist in the assembly
		// Generic types in PublicAPI use <T> notation, .NET reflection uses `1 notation
		// Strip generic parameters for comparison
		var missingTypes = declaredTypes
			.Where(t =>
			{
				// Try exact match first
				if (existingTypes.Contains(t))
				{
					return false;
				}

				// For generic types like "Foo<T>", convert to "Foo`1" format
				var genericMatch = GenericTypeNormalizer().Match(t);
				if (genericMatch.Success)
				{
					var baseName = genericMatch.Groups[1].Value;
					var typeParamCount = genericMatch.Groups[2].Value.Split(',').Length;
					var reflectionName = $"{baseName}`{typeParamCount}";
					return !existingTypes.Contains(reflectionName);
				}

				return true;
			})
			.ToList();

		// Assert
		missingTypes.ShouldBeEmpty(
			$"PublicAPI declares types missing from assembly: {string.Join(", ", missingTypes.Take(10))}");
	}

	/// <summary>
	/// Matches full type declaration lines from PublicAPI.Shipped.txt.
	/// These are lines like "Excalibur.Dispatch.Abstractions.IDispatcher" without member access patterns.
	/// </summary>
	[GeneratedRegex(@"^(?:abstract |sealed |static )?(Excalibur\.[A-Za-z.]+\.[A-Z][A-Za-z0-9]*(?:<[^>]+>)?)\s*$", RegexOptions.Compiled)]
	private static partial Regex TypeDeclarationExtractor();

	/// <summary>
	/// Extracts the base type name and generic parameters from a type like "Foo.Bar&lt;T, U&gt;".
	/// </summary>
	[GeneratedRegex(@"^(.+)<([^>]+)>$", RegexOptions.Compiled)]
	private static partial Regex GenericTypeNormalizer();

	[Fact]
	public void ContainKeyCoreDispatchTypes()
	{
		// Arrange -- force assembly load
		_ = typeof(DispatchPipeline);

		var coreAssembly = typeof(DispatchPipeline).Assembly;
		var allTypes = coreAssembly.GetTypes()
			.Select(t => t.FullName!)
			.ToHashSet(StringComparer.Ordinal);

		// Assert -- key internal and public types should exist
		allTypes.ShouldContain("Excalibur.Dispatch.Messaging.MessageContext");
		allTypes.ShouldContain("Excalibur.Dispatch.Delivery.Pipeline.DispatchPipeline");
	}

	[Fact]
	public void HavePublicApiFilesForAllShippingPackages()
	{
		// Arrange
		var solutionDir = FindSolutionDirectory();
		var dispatchSrcDir = Path.Combine(solutionDir, "src", "Dispatch");

		var packageDirs = Directory.GetDirectories(dispatchSrcDir)
			.Where(d => Directory.GetFiles(d, "*.csproj").Length > 0)
			.ToList();

		// Act -- each package with a csproj should have a PublicAPI.Shipped.txt
		var missing = new List<string>();
		foreach (var dir in packageDirs)
		{
			var shippedFile = Path.Combine(dir, "PublicAPI.Shipped.txt");
			if (!File.Exists(shippedFile))
			{
				missing.Add(Path.GetFileName(dir));
			}
		}

		// Assert
		missing.ShouldBeEmpty(
			$"Packages missing PublicAPI.Shipped.txt: {string.Join(", ", missing)}");
	}

	[Fact]
	public void HaveConsistentNullableDirectiveInPublicApiFiles()
	{
		// Arrange
		var solutionDir = FindSolutionDirectory();
		var dispatchSrcDir = Path.Combine(solutionDir, "src", "Dispatch");

		var shippedFiles = Directory.GetFiles(dispatchSrcDir, "PublicAPI.Shipped.txt", SearchOption.AllDirectories);

		var missingNullable = new List<string>();
		foreach (var file in shippedFiles)
		{
			var firstLine = File.ReadLines(file).FirstOrDefault();
			if (firstLine != "#nullable enable")
			{
				missingNullable.Add(Path.GetFileName(Path.GetDirectoryName(file)!));
			}
		}

		// Assert -- all PublicAPI files should start with #nullable enable
		missingNullable.ShouldBeEmpty(
			$"PublicAPI files missing #nullable enable: {string.Join(", ", missingNullable)}");
	}
}
