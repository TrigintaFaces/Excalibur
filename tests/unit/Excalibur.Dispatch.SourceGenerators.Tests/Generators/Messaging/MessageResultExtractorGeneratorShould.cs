// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MessageResultExtractorGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultExtractorGeneratorShould
{
	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(MessageResultExtractorGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(MessageResultExtractorGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new MessageResultExtractorGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(MessageResultExtractorGenerator).IsSealed.ShouldBeTrue();
	}

	#region Consumer result-type discovery

	/// <summary>
	/// Dispatch fails fast under Native AOT when a result type has no registered factory, so this
	/// covers the property that makes that safe: a consumer's own result types are discovered from
	/// their own project, with no annotation and no reference to this repository's types.
	/// </summary>
	[Fact]
	public void RegisterAResultTypeDeclaredByTheConsumer_FromIDispatchActionAlone()
	{
		const string source = """
			using Excalibur.Dispatch;

			namespace ConsumerApp
			{
			    public readonly struct OrderId { public int Value { get; init; } }

			    public sealed class PlaceOrderCommand : IDispatchAction<OrderId> { }
			}
			""";

		var registry = RunGeneratorAndGetRegistry(source);

		// A value type that was never statically instantiated is precisely the case that used to
		// fail at runtime under AOT rather than at the registry.
		registry.ShouldContain("RegisterFactory<global::ConsumerApp.OrderId>()");
	}

	[Fact]
	public void RegisterAReferenceResultTypeDeclaredByTheConsumer()
	{
		const string source = """
			using Excalibur.Dispatch;

			namespace ConsumerApp
			{
			    public sealed class OrderReceipt { public string? Reference { get; init; } }

			    public sealed class PlaceOrderCommand : IDispatchAction<OrderReceipt> { }
			}
			""";

		var registry = RunGeneratorAndGetRegistry(source);

		registry.ShouldContain("RegisterFactory<global::ConsumerApp.OrderReceipt>()");
	}

	[Fact]
	public void EmitTheRegistry_EvenWhenTheConsumerDeclaresNoResultTypes()
	{
		const string source = """
			namespace ConsumerApp
			{
			    public sealed class NothingToSeeHere { }
			}
			""";

		var registry = RunGeneratorAndGetRegistry(source);

		registry.ShouldContain("GeneratedResultFactoryRegistrations");

		// Control for the two tests above: same harness, same namespace, but nothing implements
		// IDispatchAction<T>, so no consumer type may be registered. Without this the assertions
		// above could pass on a generator that registered everything it saw.
		registry.ShouldNotContain("RegisterFactory<global::ConsumerApp");
	}

	#endregion

	#region Helpers

	private static string RunGeneratorAndGetRegistry(string sourceCode)
	{
		var compilation = CreateConsumerCompilation(sourceCode);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new MessageResultExtractorGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

		var result = driver.GetRunResult();
		result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

		var registry = result.GeneratedTrees
			.SingleOrDefault(static t => Path.GetFileName(t.FilePath) == "ResultFactoryRegistry.g.cs");
		registry.ShouldNotBeNull("the generator must always emit ResultFactoryRegistry.g.cs");

		return registry.ToString();
	}

	/// <summary>
	/// Builds a compilation shaped like a consumer's project: a separate assembly that references only
	/// the public abstractions package, with no reference to this repository's own result types.
	/// </summary>
	private static CSharpCompilation CreateConsumerCompilation(string sourceCode)
	{
		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Excalibur.Dispatch.IDispatchMessage).Assembly.Location),
		};

		var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
		foreach (var asm in new[] { "System.Runtime.dll", "System.Collections.dll", "netstandard.dll" })
		{
			var path = Path.Combine(runtimeDir, asm);
			if (File.Exists(path))
			{
				references.Add(MetadataReference.CreateFromFile(path));
			}
		}

		return CSharpCompilation.Create(
			"ConsumerAssembly",
			[CSharpSyntaxTree.ParseText(sourceCode)],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	#endregion
}
