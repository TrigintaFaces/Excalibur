// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class HandlerRegistryContextAwarenessShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task InvokeTheRegisteredInterfaceInstanceAndSetItsContextExactlyOnce(bool registerDifferentConcreteInstance)
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			using Excalibur.Dispatch.Delivery;
			using Excalibur.Dispatch.Messaging;
			using Microsoft.Extensions.DependencyInjection;
			namespace Consumer;
			public sealed class Message : IDispatchAction;
			public sealed class Handler : IActionHandler<Message>, IMessageContextAware
			{
			    public int ContextCalls { get; private set; }
			    public int HandleCalls { get; private set; }
			    public IMessageContext? Received { get; private set; }
			    void IMessageContextAware.SetContext(IMessageContext context) { ContextCalls++; Received = context; }
			    Task IActionHandler<Message>.HandleAsync(Message action, CancellationToken token) { HandleCalls++; return Task.CompletedTask; }
			}
			public static class Harness
			{
			    public static async Task VerifyAsync(bool registerDifferentConcreteInstance)
			    {
			        var selected = new Handler();
			        var different = new Handler();
			        var services = new ServiceCollection();
			        services.AddSingleton<IActionHandler<Message>>(selected);
			        if (registerDifferentConcreteInstance) { services.AddSingleton(different); }
			        using var provider = services.BuildServiceProvider();
			        var action = new Message();
			        var context = new MessageContext(action, provider);
			        await Excalibur.Dispatch.Generated.PrecompiledDirectActionDispatch.InvokeAsync(action, provider, context, CancellationToken.None);
			        if (selected.HandleCalls != 1 || selected.ContextCalls != 1 || !ReferenceEquals(selected.Received, context)
			            || different.HandleCalls != 0 || different.ContextCalls != 0)
			        {
			            throw new InvalidOperationException("The generated invocation replaced the registered interface instance or applied context incorrectly.");
			        }
			    }
			}
			""";
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Append(typeof(IDispatchMessage).Assembly.Location)
			.Append(typeof(Excalibur.Dispatch.Delivery.Handlers.HandlerActivator).Assembly.Location)
			.Distinct(StringComparer.Ordinal)
			.Select(static path => MetadataReference.CreateFromFile(path));
		var compilation = CSharpCompilation.Create("ContextInstanceConsumer" + Guid.NewGuid().ToString("N"),
			[CSharpSyntaxTree.ParseText(source)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new HandlerRegistrySourceGenerator());
		driver = driver.RunGenerators(compilation);
		var direct = driver.GetRunResult().Results.Single().GeneratedSources
			.Single(static generated => generated.HintName == "PrecompiledDirectActionDispatch.g.cs").SyntaxTree;
		// Exercise the actual generated invocation body without mutating process-global registries.
		// Module registration is independently covered by the native public-composition acceptance sample.
		var root = direct.GetRoot();
		var initializerAttribute = root.DescendantNodes().OfType<AttributeListSyntax>()
			.Single(static list => list.Attributes.Any(static attribute => attribute.Name.ToString() == "ModuleInitializer"));
		root = root.RemoveNode(initializerAttribute, SyntaxRemoveOptions.KeepNoTrivia)!;
		var executable = compilation.AddSyntaxTrees(CSharpSyntaxTree.Create((CSharpSyntaxNode)root));
		using var bytes = new MemoryStream();
		executable.Emit(bytes).Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var assembly = System.Reflection.Assembly.Load(bytes.ToArray());
		var method = assembly.GetType("Consumer.Harness", throwOnError: true)!.GetMethod("VerifyAsync")!;
		var invocation = method.Invoke(null, [registerDifferentConcreteInstance]).ShouldBeAssignableTo<Task>();
		await invocation.WaitAsync(TimeSpan.FromSeconds(10));
	}

	[Theory]
	[InlineData("explicit")]
	[InlineData("inherited")]
	[InlineData("throwing-property")]
	[InlineData("property-only")]
	[InlineData("foreign-interface")]
	public void CompileConsistentContextMetadataAndSetters(string kind)
	{
		var declaration = kind switch
		{
			"inherited" => "public abstract class Base : IMessageContextAware { void IMessageContextAware.SetContext(IMessageContext context) { } } public sealed class Handler : Base, IActionHandler<Message>",
			"foreign-interface" => "public sealed class Handler : Foreign.IMessageContextAware, IActionHandler<Message>",
			"property-only" => "public sealed class Handler : IActionHandler<Message>",
			_ => "public sealed class Handler : IMessageContextAware, IActionHandler<Message>"
		};
		var contextMember = kind switch
		{
			"property-only" => "public IMessageContext? Context { get; set; }",
			"throwing-property" => "void IMessageContextAware.SetContext(IMessageContext context) { } public IMessageContext? Context { get => null; set => throw new System.InvalidOperationException(); }",
			"explicit" => "void IMessageContextAware.SetContext(IMessageContext context) { }",
			_ => string.Empty
		};
		var source = $$"""
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			using Excalibur.Dispatch.Delivery;
			namespace Foreign { public interface IMessageContextAware { } }
			namespace Consumer
			{
			    public sealed class Message : IDispatchAction;
			    {{declaration}}
			    {
			        {{contextMember}}
			        public Task HandleAsync(Message message, CancellationToken token) => Task.CompletedTask;
			    }
			}
			""";
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Append(typeof(IDispatchMessage).Assembly.Location)
			.Append(typeof(Excalibur.Dispatch.Delivery.Handlers.HandlerActivator).Assembly.Location)
			.Distinct(StringComparer.Ordinal)
			.Select(static path => MetadataReference.CreateFromFile(path));
		var compilation = CSharpCompilation.Create("ContextAwareConsumer", [CSharpSyntaxTree.ParseText(source)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new HandlerRegistrySourceGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
		diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		using var assembly = new MemoryStream();
		output.Emit(assembly).Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var generated = driver.GetRunResult().Results.Single().GeneratedSources;
		var setters = generated.Single(static source => source.HintName == "GeneratedHandlerActivatorRegistrations.g.cs").SourceText.ToString();
		var direct = generated.Single(static source => source.HintName == "PrecompiledDirectActionDispatch.g.cs").SourceText.ToString();
		if (kind == "foreign-interface")
		{
			setters.ShouldNotContain("RegisterContextSetter<");
			direct.ShouldNotContain("requiresContext = true;");
		}
		else
		{
			direct.ShouldContain("requiresContext = true;");
			setters.Split("RegisterContextSetter<", StringSplitOptions.None).Length.ShouldBe(2);
			if (kind == "property-only")
			{
				setters.ShouldContain("handler.Context = context");
				direct.ShouldContain("handler.Context = context");
			}
			else
			{
				setters.Split(".SetContext(context)", StringSplitOptions.None).Length.ShouldBe(2);
				direct.Split(".SetContext(context)", StringSplitOptions.None).Length.ShouldBe(2);
				setters.ShouldNotContain("handler.Context = context");
				direct.ShouldNotContain("handler.Context = context");
			}
		}
	}
}
