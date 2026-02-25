// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Generators;

/// <summary>
/// Sprint 522 cleanup verification tests:
/// - S522.4: JsonSerializationSG duplicate pattern cleanup
/// - S522.5: AOT sample net10.0 update
/// - S522.8: CSharpGeneratorDriver integration test infrastructure validation
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
public sealed class Sprint522CleanupVerificationShould
{
	#region S522.4: JsonSerializationSG Duplicate Pattern Cleanup

	[Fact]
	public void JsonSerializationSG_InterfaceCheck_HaveExactlyTwoUniquePatterns()
	{
		// Verify lines 80-82 in JsonSerializationSourceGenerator have exactly 2 unique patterns:
		// "global::Excalibur.Dispatch.Abstractions.IDispatchMessage" and
		// "Excalibur.Dispatch.Abstractions.IDispatchMessage"
		var sgSourcePath = FindSourceFile("JsonSerializationSourceGenerator.cs");
		sgSourcePath.ShouldNotBeNull("JsonSerializationSourceGenerator.cs should exist");

		var content = File.ReadAllText(sgSourcePath);

		// Count occurrences of the pattern strings (both with and without global:: prefix)
		var globalPattern = "global::Excalibur.Dispatch.Abstractions.IDispatchMessage";
		var simplePattern = "\"Excalibur.Dispatch.Abstractions.IDispatchMessage\"";

		var globalCount = CountOccurrences(content, globalPattern);
		var simpleCount = CountOccurrences(content, simplePattern);

		// After S522.4 cleanup, each pattern should appear exactly once in the is/or expression
		globalCount.ShouldBe(1, $"global:: pattern should appear exactly once, found {globalCount}");
		simpleCount.ShouldBe(1, $"non-global pattern should appear exactly once, found {simpleCount}");
	}

	[Fact]
	public void JsonSerializationSG_InterfaceCheck_NotContainDuplicateOrPatterns()
	{
		var sgSourcePath = FindSourceFile("JsonSerializationSourceGenerator.cs");
		sgSourcePath.ShouldNotBeNull();

		var content = File.ReadAllText(sgSourcePath);

		// The old code had patterns like:
		// ... is "X" or "Y" or "X" or "Y"
		// After cleanup, there should be no adjacent duplicate patterns
		var lines = content.Split('\n');
		foreach (var line in lines)
		{
			if (!line.Contains(" or ")) continue;
			var parts = line.Split(" or ", StringSplitOptions.TrimEntries);
			var uniqueParts = parts.Distinct().ToList();
			uniqueParts.Count.ShouldBe(parts.Length, $"Line contains duplicate or-patterns: {line.Trim()}");
		}
	}

	[Fact]
	public void JsonSerializationSG_StillFiltersAbstractTypes()
	{
		// Verify the generator still correctly filters abstract types after cleanup
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public abstract class BaseMessage : IDispatchMessage { }
			    public class ConcreteMessage : BaseMessage { }
			}
			""";

		var result = RunJsonSerializationGenerator(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("ConcreteMessage");
		text.ShouldNotContain("\"global::TestApp.BaseMessage\"");
	}

	[Fact]
	public void JsonSerializationSG_StillFiltersGenericTypes()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class GenericMessage<T> : IDispatchMessage { }
			    public class ConcreteMessage : IDispatchMessage { }
			}
			""";

		var result = RunJsonSerializationGenerator(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("ConcreteMessage");
		text.ShouldNotContain("GenericMessage");
	}

	[Fact]
	public void JsonSerializationSG_EmitsJSON001Diagnostic()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class MyMessage : IDispatchMessage { }
			}
			""";

		var result = RunJsonSerializationGenerator(source);

		var json001 = result.Diagnostics.FirstOrDefault(d => d.Id == "JSON001");
		json001.ShouldNotBeNull("JSON001 diagnostic should be emitted");
		json001.Severity.ShouldBe(DiagnosticSeverity.Info);
	}

	#endregion

	#region S522.5: AOT Sample net10.0 Update

	[Fact]
	public void AotSample_Csproj_TargetNet10()
	{
		var csprojPath = FindProjectFile("Excalibur.Dispatch.Aot.Sample.csproj");
		csprojPath.ShouldNotBeNull("AOT sample csproj should exist");

		var content = File.ReadAllText(csprojPath);
		content.ShouldContain("<TargetFramework>net10.0</TargetFramework>");
		content.ShouldNotContain("net9.0");
	}

	[Fact]
	public void AotSample_ProgramCs_NotReferenceNet9()
	{
		var programPath = FindProgramFile();
		programPath.ShouldNotBeNull("AOT sample Program.cs should exist");

		var content = File.ReadAllText(programPath);
		content.ShouldNotContain("net9.0");
	}

	[Fact]
	public void AotSample_Readme_NotReferenceNet9()
	{
		var readmePath = FindReadmeFile();
		readmePath.ShouldNotBeNull("AOT sample README.md should exist");

		var content = File.ReadAllText(readmePath);
		content.ShouldNotContain("net9.0");
	}

	[Fact]
	public void AotSample_Csproj_HavePublishAotEnabled()
	{
		var csprojPath = FindProjectFile("Excalibur.Dispatch.Aot.Sample.csproj");
		csprojPath.ShouldNotBeNull();

		var content = File.ReadAllText(csprojPath);
		content.ShouldContain("<PublishAot>true</PublishAot>");
	}

	#endregion

	#region S522.8: CSharpGeneratorDriver Integration Infrastructure

	[Fact]
	public void GeneratorDriverInfrastructure_CanCreateCompilationWithAbstractionsReference()
	{
		// Verify that the integration test infrastructure correctly references Abstractions
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class TestMessage : IDispatchMessage { }
			}
			""";

		var compilation = CreateCompilation(source);
		compilation.ShouldNotBeNull();

		// Should compile without reference-related errors
		var errors = compilation.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error && d.Id.StartsWith("CS0246"))
			.ToList();

		errors.ShouldBeEmpty("Compilation should resolve IDispatchMessage without CS0246 errors");
	}

	[Fact]
	public void GeneratorDriverInfrastructure_CanRunMultipleGenerators()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class TestCommand : IDispatchAction<Guid> { }
			    public class TestHandler : IActionHandler<TestCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(TestCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var compilation = CreateCompilation(source);

		// Run all 4 generators at once
		var generators = new IIncrementalGenerator[]
		{
			new HandlerRegistrySourceGenerator(),
			new JsonSerializationSourceGenerator(),
			new MessageResultExtractorGenerator(),
			new ServiceRegistrationSourceGenerator(),
		};

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var result = driver.GetRunResult();
		result.GeneratedTrees.ShouldNotBeEmpty("Running 4 generators should produce generated trees");

		// No generator errors
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void GeneratorDriverInfrastructure_GeneratorResults_AreIsolatedPerGenerator()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class TestCommand : IDispatchAction<Guid> { }
			    public class TestHandler : IActionHandler<TestCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(TestCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var compilation = CreateCompilation(source);

		var generators = new IIncrementalGenerator[]
		{
			new HandlerRegistrySourceGenerator(),
			new JsonSerializationSourceGenerator(),
		};

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var result = driver.GetRunResult();

		// Each generator result should have its own set of generated sources
		result.Results.Length.ShouldBe(2, "Should have results for 2 generators");

		var handlerResults = result.Results[0];
		var jsonResults = result.Results[1];

		handlerResults.GeneratedSources.ShouldNotBeEmpty("HandlerRegistrySG should generate sources");
		jsonResults.GeneratedSources.ShouldNotBeEmpty("JsonSerializationSG should generate sources");
	}

	[Fact]
	public void GeneratorDriverInfrastructure_EmptyInput_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class EmptyClass { }
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new HandlerRegistrySourceGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var result = driver.GetRunResult();
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void GeneratorDriverInfrastructure_IntegrationTestFileExists()
	{
		// Verify the CSharpGeneratorDriverIntegrationShould.cs file exists in the expected location
		var basePath = FindProjectRoot();
		var integrationPath = Path.Combine(basePath, "tests", "unit",
			"Excalibur.Dispatch.SourceGenerators.Tests", "Generators", "Integration",
			"CSharpGeneratorDriverIntegrationShould.cs");

		File.Exists(integrationPath).ShouldBeTrue(
			$"CSharpGeneratorDriverIntegrationShould.cs should exist at expected path: {integrationPath}");
	}

	[Fact]
	public void GeneratorDriverInfrastructure_TestCsprojReferencesTestingPackage()
	{
		// Verify the test csproj has the SourceGenerators.Testing.XUnit package reference
		var basePath = FindProjectRoot();
		var csprojPath = Path.Combine(basePath, "tests", "unit",
			"Excalibur.Dispatch.SourceGenerators.Tests",
			"Excalibur.Dispatch.SourceGenerators.Tests.csproj");

		File.Exists(csprojPath).ShouldBeTrue();
		var content = File.ReadAllText(csprojPath);
		content.ShouldContain("Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit");
	}

	#endregion

	#region Helpers

	private static CSharpCompilation CreateCompilation(string sourceCode)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Abstractions.IDispatchMessage).Assembly.Location),
		};

		var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
		foreach (var asm in new[] { "System.Runtime.dll", "System.Collections.dll", "System.Threading.Tasks.dll", "netstandard.dll" })
		{
			var path = Path.Combine(runtimeDir, asm);
			if (File.Exists(path))
			{
				references.Add(MetadataReference.CreateFromFile(path));
			}
		}

		return CSharpCompilation.Create("TestAssembly", [syntaxTree], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static GeneratorDriverRunResult RunJsonSerializationGenerator(string sourceCode)
	{
		var compilation = CreateCompilation(sourceCode);
		var generator = new JsonSerializationSourceGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		return driver.GetRunResult();
	}

	private static int CountOccurrences(string text, string pattern)
	{
		int count = 0;
		int index = 0;
		while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
		{
			count++;
			index += pattern.Length;
		}
		return count;
	}

	private static string FindProjectRoot()
	{
		var dir = AppDomain.CurrentDomain.BaseDirectory;
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir, "Excalibur.sln")))
			{
				return dir;
			}
			dir = Directory.GetParent(dir)?.FullName;
		}

		throw new DirectoryNotFoundException("Could not find project root");
	}

	private static string? FindSourceFile(string fileName)
	{
		var basePath = FindProjectRoot();
		var candidates = new[]
		{
			Path.Combine(basePath, "src", "Dispatch", "Excalibur.Dispatch.SourceGenerators", "Messaging", fileName),
			Path.Combine(basePath, "src", "Dispatch", "Excalibur.Dispatch.SourceGenerators", fileName),
		};
		return candidates.FirstOrDefault(File.Exists);
	}

	private static string? FindProjectFile(string fileName)
	{
		var basePath = FindProjectRoot();
		var path = Path.Combine(basePath, "samples", "11-aot", "Excalibur.Dispatch.Aot.Sample", fileName);
		return File.Exists(path) ? path : null;
	}

	private static string? FindProgramFile()
	{
		var basePath = FindProjectRoot();
		var path = Path.Combine(basePath, "samples", "11-aot", "Excalibur.Dispatch.Aot.Sample", "Program.cs");
		return File.Exists(path) ? path : null;
	}

	private static string? FindReadmeFile()
	{
		var basePath = FindProjectRoot();
		var path = Path.Combine(basePath, "samples", "11-aot", "Excalibur.Dispatch.Aot.Sample", "README.md");
		return File.Exists(path) ? path : null;
	}

	#endregion
}
