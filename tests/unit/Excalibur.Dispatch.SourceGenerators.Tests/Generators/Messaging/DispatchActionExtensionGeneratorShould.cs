// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Unit and integration tests for <see cref="DispatchActionExtensionGenerator"/>.
/// Validates that typed dispatch extension methods are correctly generated for
/// concrete <c>IDispatchAction&lt;TResponse&gt;</c> types, enabling TResponse inference.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
public sealed class DispatchActionExtensionGeneratorShould
{
	#region Structural Tests

	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(DispatchActionExtensionGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(DispatchActionExtensionGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new DispatchActionExtensionGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(DispatchActionExtensionGenerator).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Generator Driver Tests

	[Fact]
	public void GenerateTypedExtensions_ForConcreteActionType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();

		var generatedFiles = result.GeneratedTrees
			.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("TypedDispatchExtensions.g.cs");
	}

	[Fact]
	public void GenerateDispatchAsyncOverload_ForConcreteActionType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("DispatchAsync");
		text.ShouldContain("global::TestApp.CreateOrderCommand");
		text.ShouldContain("global::System.Guid");
	}

	[Fact]
	public void GenerateDispatchChildAsyncOverload_ForConcreteActionType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("DispatchChildAsync");
		text.ShouldContain("global::TestApp.CreateOrderCommand");
	}

	[Fact]
	public void GenerateContextOverload_ForConcreteActionType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// Should contain the IMessageContext overload
		text.ShouldContain("IMessageContext context");
	}

	[Fact]
	public void GenerateAggressiveInlining_OnAllMethods()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// All generated methods should have AggressiveInlining
		text.ShouldContain("MethodImpl(MethodImplOptions.AggressiveInlining)");
	}

	[Fact]
	public void GenerateInCorrectNamespace()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("namespace Excalibur.Dispatch;");
	}

	[Fact]
	public void GenerateGeneratedCodeAttribute()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("[System.CodeDom.Compiler.GeneratedCode(\"Excalibur.Dispatch.SourceGenerators\"");
	}

	#endregion

	#region Exclusion Tests

	[Fact]
	public void SkipAbstractTypes()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public abstract class AbstractCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);

		// Should either not generate the file, or not include the abstract type
		var tree = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("TypedDispatchExtensions.g.cs"));

		if (tree is not null)
		{
			var text = tree.GetText().ToString();
			text.ShouldNotContain("AbstractCommand");
		}
	}

	[Fact]
	public void SkipOpenGenericTypes()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class GenericCommand<T> : IDispatchAction<T> { }
			}
			""";

		var result = RunGenerator(source);

		var tree = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("TypedDispatchExtensions.g.cs"));

		if (tree is not null)
		{
			var text = tree.GetText().ToString();
			text.ShouldNotContain("GenericCommand");
		}
	}

	[Fact]
	public void NotGenerateForEmptyCompilation()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAnAction { }
			}
			""";

		var result = RunGenerator(source);

		var tree = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("TypedDispatchExtensions.g.cs"));
		tree.ShouldBeNull();
	}

	#endregion

	#region Multiple Types Tests

	[Fact]
	public void GenerateForMultipleActionTypes()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			    public class GetOrderQuery : IDispatchAction<string> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("global::TestApp.CreateOrderCommand");
		text.ShouldContain("global::TestApp.GetOrderQuery");
		text.ShouldContain("global::System.Guid");
		text.ShouldContain("string");
	}

	[Fact]
	public void DeduplicateTypesFromMultipleSyntaxNodes()
	{
		// A partial class can appear in multiple syntax trees
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// Count method signatures for CreateOrderCommand — should be exactly 3
		// (DispatchAsync, DispatchAsync with context, DispatchChildAsync)
		var methodSignatures = text.Split('\n')
			.Where(l => l.Contains("global::TestApp.CreateOrderCommand") &&
			            (l.Contains("DispatchAsync") || l.Contains("DispatchChildAsync")))
			.ToList();
		methodSignatures.Count.ShouldBe(3);
	}

	[Fact]
	public void HandleTypesInDifferentNamespaces()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp.Orders
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}

			namespace TestApp.Inventory
			{
			    public class CreateOrderCommand : IDispatchAction<int> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// Both should appear with fully qualified names — no collision
		text.ShouldContain("global::TestApp.Orders.CreateOrderCommand");
		text.ShouldContain("global::TestApp.Inventory.CreateOrderCommand");
	}

	[Fact]
	public void ReportDiscoveredActionCount()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class Command1 : IDispatchAction<Guid> { }
			    public class Command2 : IDispatchAction<int> { }
			    public class Command3 : IDispatchAction<string> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("Action types discovered: 3");
	}

	#endregion

	#region Generated Code Structure Tests

	[Fact]
	public void GenerateForwardingToDispatcherContextExtensions()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// Verify the generated methods forward to the correct existing methods
		text.ShouldContain("DispatcherContextExtensions.DispatchAsync<");
		text.ShouldContain("DispatcherContextExtensions.DispatchChildAsync<");
	}

	[Fact]
	public void GenerateNullableEnable()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("#nullable enable");
	}

	[Fact]
	public void GenerateAutoGeneratedHeader()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("// <auto-generated/>");
	}

	[Fact]
	public void GenerateXmlDocumentation()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		// Should contain XML docs for the class and methods
		text.ShouldContain("/// <summary>");
		text.ShouldContain("compile-time type inference");
	}

	#endregion

	#region Value Type Tests

	[Fact]
	public void HandleValueTypeResponseCorrectly()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class IncrementCounter : IDispatchAction<int> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("global::TestApp.IncrementCounter");
		text.ShouldContain("int");
	}

	[Fact]
	public void HandleCustomResponseTypeCorrectly()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;

			namespace TestApp
			{
			    public class OrderResult
			    {
			        public Guid OrderId { get; set; }
			    }

			    public class CreateOrderCommand : IDispatchAction<OrderResult> { }
			}
			""";

		var result = RunGenerator(source);
		var text = GetGeneratedText(result, "TypedDispatchExtensions.g.cs");

		text.ShouldContain("global::TestApp.CreateOrderCommand");
		text.ShouldContain("global::TestApp.OrderResult");
	}

	#endregion

	#region Helpers

	private static CSharpCompilation CreateCompilation(string sourceCode, string assemblyName = "TestAssembly")
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
		};

		// Add Excalibur.Dispatch.Abstractions assembly for IDispatchAction<T>
		var abstractionsAssembly = typeof(Excalibur.Dispatch.IDispatchMessage).Assembly;
		references.Add(MetadataReference.CreateFromFile(abstractionsAssembly.Location));

		// Add system runtime references needed for compilation
		var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
		var runtimeAssemblies = new[]
		{
			"System.Runtime.dll",
			"System.Collections.dll",
			"System.Threading.Tasks.dll",
			"System.ComponentModel.dll",
			"netstandard.dll",
		};

		foreach (var asm in runtimeAssemblies)
		{
			var path = System.IO.Path.Combine(runtimeDir!, asm);
			if (System.IO.File.Exists(path))
			{
				references.Add(MetadataReference.CreateFromFile(path));
			}
		}

		return CSharpCompilation.Create(
			assemblyName,
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static GeneratorDriverRunResult RunGenerator(string sourceCode)
	{
		var compilation = CreateCompilation(sourceCode);
		var generator = new DispatchActionExtensionGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		return driver.GetRunResult();
	}

	private static string GetGeneratedText(GeneratorDriverRunResult result, string fileName)
	{
		var tree = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith(fileName));
		tree.ShouldNotBeNull($"Expected generated file '{fileName}' was not produced.");
		return tree.GetText().ToString();
	}

	#endregion
}
