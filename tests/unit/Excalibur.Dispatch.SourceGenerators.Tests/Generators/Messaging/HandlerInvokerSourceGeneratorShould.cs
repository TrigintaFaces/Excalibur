// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="HandlerInvokerSourceGenerator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class HandlerInvokerSourceGeneratorShould
{
	[Theory]
	[InlineData("")]
	[InlineData("namespace Foreign { public interface IActionHandler<T> { } public sealed class Other : IActionHandler<string>; }")]
	[InlineData("namespace Consumer { public static class Container { private sealed class Hidden : Excalibur.Dispatch.Delivery.IActionHandler<First> { public System.Threading.Tasks.Task HandleAsync(First message, System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask; } } }")]
	[InlineData("namespace Consumer { file sealed class Hidden : Excalibur.Dispatch.Delivery.IActionHandler<First> { public System.Threading.Tasks.Task HandleAsync(First message, System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask; } }")]
	public void CompileExplicitInterfaceHandlers(string additionalSource)
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			using Excalibur.Dispatch.Delivery;
			namespace Consumer;
			public sealed class First : IDispatchAction;
			public sealed class Second : IDispatchAction<string>;
			public sealed class Handler : IActionHandler<First>, IActionHandler<Second, string>
			{
			    Task IActionHandler<First>.HandleAsync(First message, CancellationToken token) => Task.CompletedTask;
			    Task<string> IActionHandler<Second, string>.HandleAsync(Second message, CancellationToken token) => Task.FromResult("second");
			}
			""";
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Append(typeof(IDispatchMessage).Assembly.Location)
			.Append(typeof(Excalibur.Dispatch.Delivery.Handlers.HandlerInvokerAot).Assembly.Location)
			.Distinct(StringComparer.Ordinal)
			.Select(static path => MetadataReference.CreateFromFile(path));
		var compilation = CSharpCompilation.Create("ExplicitConsumer", [CSharpSyntaxTree.ParseText(source, path: "/consumer/main.cs"), CSharpSyntaxTree.ParseText(additionalSource, path: "/consumer/additional.cs")], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new HandlerInvokerSourceGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
		diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		driver.GetRunResult().GeneratedTrees.ShouldNotBeEmpty();
		output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		using var assembly = new MemoryStream();
		output.Emit(assembly).Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var generated = driver.GetRunResult().GeneratedTrees.Single().ToString();
		generated.ShouldContain("global::Consumer.Handler");
		generated.ShouldNotContain("Foreign.Other");
		generated.ShouldNotContain("Hidden");
	}

	[Fact]
	public void ImplementIIncrementalGenerator()
	{
		typeof(HandlerInvokerSourceGenerator).GetInterfaces()
			.ShouldContain(typeof(IIncrementalGenerator));
	}

	[Fact]
	public void HaveGeneratorAttribute()
	{
		var attributes = typeof(HandlerInvokerSourceGenerator)
			.GetCustomAttributes(typeof(GeneratorAttribute), false);
		attributes.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeInstantiable()
	{
		var generator = new HandlerInvokerSourceGenerator();
		generator.ShouldNotBeNull();
	}

	[Fact]
	public void BeSealed()
	{
		typeof(HandlerInvokerSourceGenerator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveStaticHandlerInterfacesField()
	{
		// HandlerInvokerSourceGenerator defines a static HandlerInterfaces HashSet
		var field = typeof(HandlerInvokerSourceGenerator)
			.GetField("HandlerInterfaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		field.ShouldNotBeNull();
	}
}
