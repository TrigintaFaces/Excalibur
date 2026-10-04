// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.SourceGenerators.Pipeline;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
[Trait("Pattern", "Regression")]
public sealed class StaticPipelineBehaviorShould
{
	[Fact]
	public void PreserveSelectedUntypedOverloadForTypedMessage()
	{
		var output = Generate("""
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			namespace Consumer;
			public sealed class Command : IDispatchAction<string>;
			public static class Calls
			{
			    public static Task<IMessageResult> Run(IDispatcher dispatcher, Command message, IMessageContext context, CancellationToken token)
			        => dispatcher.DispatchAsync<Command>(message, context, token);
			}
			""");
		output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void CompileIdenticalCallCoordinatesInDifferentFiles()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			namespace Consumer;
			public static class CLASS
			{
			    public static Task<IMessageResult> Run(IDispatcher dispatcher, Command message, IMessageContext context, CancellationToken token)
			        => dispatcher.DispatchAsync<Command>(message, context, token);
			}
			""";
		var output = Generate(source.Replace("CLASS", "First", StringComparison.Ordinal),
			source.Replace("CLASS", "Other", StringComparison.Ordinal),
			"namespace Consumer; public sealed class Command : Excalibur.Dispatch.IDispatchAction;");
		output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void ForwardWithoutExceptionConversionOrAsyncWrapper()
	{
		var output = Generate("""
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			namespace Consumer;
			public sealed class Command : IDispatchAction;
			public static class Calls
			{
			    public static Task<IMessageResult> Run(IDispatcher dispatcher, Command message, IMessageContext context, CancellationToken token)
			        => dispatcher.DispatchAsync<Command>(message, context, token);
			}
			""");
		var generated = output.SyntaxTrees.Last().ToString();
		generated.ShouldNotContain("catch (Exception");
		generated.ShouldNotContain("async Task");
	}

	[Theory]
	[InlineData("public sealed class Envelope<T> : IDispatchAction; public static class Calls { public static Task<IMessageResult> Run<T>(IDispatcher d, IMessageContext c) => d.DispatchAsync(new Envelope<T>(), c, default); }")]
	[InlineData("public static class Outer<T> { public sealed class Nested : IDispatchAction; public static Task<IMessageResult> Run(IDispatcher d, IMessageContext c) => d.DispatchAsync(new Nested(), c, default); }")]
	[InlineData("public static class Calls { private sealed class Hidden : IDispatchAction; public static Task<IMessageResult> Run(IDispatcher d, IMessageContext c) => d.DispatchAsync(new Hidden(), c, default); }")]
	[InlineData("file sealed class Hidden : IDispatchAction; public static class Calls { public static Task<IMessageResult> Run(IDispatcher d, IMessageContext c) => d.DispatchAsync(new Hidden(), c, default); }")]
	[InlineData("public static class Calls { private sealed class Response; private sealed class Hidden : IDispatchAction<Response>; private static Task<IMessageResult<Response>> Run(IDispatcher d, IMessageContext c) => d.DispatchAsync<Hidden, Response>(new Hidden(), c, default); }")]
	public void LeaveUnnameableCallSitesUntouched(string declarations)
	{
		var output = Generate("using System.Threading.Tasks; using Excalibur.Dispatch; namespace Consumer; " + declarations,
			"using System.Threading.Tasks; using Excalibur.Dispatch; namespace Control; internal sealed class Command : IDispatchAction; public static class Calls { public static Task<IMessageResult> Run(IDispatcher d, IMessageContext c) => d.DispatchAsync(new Command(), c, default); }");
		var generated = output.SyntaxTrees.Last().ToString();
		generated.ShouldContain("InterceptionCount => 1");
		generated.ShouldContain("global::Control.Command");
	}

	[Theory]
	[InlineData("pending")]
	[InlineData("success")]
	[InlineData("fault")]
	[InlineData("cancel")]
	[InlineData("throw")]
	public void PreserveTaskIdentityAndSynchronousThrowsAtInterceptedCallSite(string outcome)
	{
		var output = Generate("""
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch;
			namespace Consumer;
			public sealed class Command : IDispatchAction;
			public static class Calls
			{
			    public static Task<IMessageResult> Run(IDispatcher dispatcher, IMessageContext context, CancellationToken token)
			        => dispatcher.DispatchAsync<Command>(new Command(), context, token);
			}
			""");
		var tree = output.SyntaxTrees.First();
		var invocation = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().Single();
#pragma warning disable RSEXPERIMENTAL002
		output.GetSemanticModel(tree).GetInterceptorMethod(invocation).ShouldNotBeNull();
#pragma warning restore RSEXPERIMENTAL002
		using var bytes = new MemoryStream();
		output.Emit(bytes).Success.ShouldBeTrue();
		var assembly = System.Reflection.Assembly.Load(bytes.ToArray());
		var run = assembly.GetType("Consumer.Calls")!.GetMethod("Run")!
			.CreateDelegate<Func<IDispatcher, IMessageContext, CancellationToken, Task<IMessageResult>>>();
		var dispatcher = A.Fake<IDispatcher>();
		var context = A.Fake<IMessageContext>();
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var error = new InvalidOperationException("terminal failure");
		var expected = outcome switch
		{
			"fault" => Task.FromException<IMessageResult>(error),
			"cancel" => Task.FromCanceled<IMessageResult>(cancellation.Token),
			"success" => Task.FromResult(A.Fake<IMessageResult>()),
			_ => new TaskCompletionSource<IMessageResult>().Task,
		};
		var call = A.CallTo(dispatcher).Where(call => call.Method.Name == "DispatchAsync")
			.WithReturnType<Task<IMessageResult>>();
		if (outcome == "throw")
		{
			call.Throws(error);
			Should.Throw<InvalidOperationException>(() => run(dispatcher, context, cancellation.Token)).ShouldBeSameAs(error);
		}
		else
		{
			call.Returns(expected);
			run(dispatcher, context, cancellation.Token).ShouldBeSameAs(expected);
		}
		call.MustHaveHappenedOnceExactly();
		var actual = Fake.GetCalls(dispatcher).Single();
		actual.Arguments[1].ShouldBeSameAs(context);
		actual.Arguments[2].ShouldBe(cancellation.Token);
	}

	private static Compilation Generate(params string[] sources)
	{
		var options = new CSharpParseOptions(LanguageVersion.Preview).WithFeatures(
			[new KeyValuePair<string, string>("InterceptorsNamespaces", "Excalibur.Dispatch.Generated")]);
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Append(typeof(IDispatchMessage).Assembly.Location)
			.Append(typeof(Excalibur.Dispatch.Delivery.Handlers.HandlerInvokerAot).Assembly.Location)
			.Distinct(StringComparer.Ordinal).Select(static path => MetadataReference.CreateFromFile(path));
		var compilation = CSharpCompilation.Create("StaticConsumer",
			sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, options, $"/consumer/{index}.cs")), references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		GeneratorDriver driver = CSharpGeneratorDriver.Create([new StaticPipelineGenerator().AsSourceGenerator()], parseOptions: options);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
		diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		driver.GetRunResult().GeneratedTrees.ShouldNotBeEmpty();
		using var assembly = new MemoryStream();
		var emitted = output.Emit(assembly);
		emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		return output;
	}
}
