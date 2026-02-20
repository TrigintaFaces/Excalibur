// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Analyzers;
using Excalibur.Dispatch.SourceGenerators.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Analyzers;

/// <summary>
/// Unit tests for <see cref="OptimizationHintAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OptimizationHintAnalyzerTests
{
	// Interface stubs file (separate from test code to avoid using statement issues)
	private const string InterfaceStubs = """
		namespace Excalibur.Dispatch.Abstractions
		{
		    public interface IDispatchMessage { }
		    public interface IDispatchHandler<in TMessage> where TMessage : IDispatchMessage
		    {
		        System.Threading.Tasks.Task<object> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		}
		""";

	/// <summary>
	/// Interface stubs including IActionHandler for testing the IActionHandler branch.
	/// </summary>
	private const string ActionHandlerInterfaceStubs = """
		namespace Excalibur.Dispatch.Abstractions
		{
		    public interface IDispatchMessage { }
		    public interface IDispatchHandler<in TMessage> where TMessage : IDispatchMessage
		    {
		        System.Threading.Tasks.Task<object> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		    public interface IActionHandler<in TMessage> where TMessage : IDispatchMessage
		    {
		        System.Threading.Tasks.Task HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		}
		""";

	/// <summary>
	/// Verifies DISP004 is reported when handler class is not sealed.
	/// </summary>
	[Fact]
	public async Task UnsealedHandler_ReportsDISP004()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class {|#0:TestHandler|} : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> {|#1:HandleAsync|}(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// Expect two DISP004 diagnostics: one for sealing, one for ValueTask
		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Handler 'TestHandler' could be sealed for better performance (enables devirtualization)"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(1)
				.WithArguments("Method 'HandleAsync' returns Task<T>. Consider ValueTask<T> if synchronous completion is common for zero-allocation fast path"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies sealed handlers do not trigger the sealing hint.
	/// </summary>
	[Fact]
	public async Task SealedHandler_NoSealingHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> {|#0:HandleAsync|}(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// Only expect ValueTask hint, not sealing hint
		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Method 'HandleAsync' returns Task<T>. Consider ValueTask<T> if synchronous completion is common for zero-allocation fast path"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies methods returning ValueTask do not trigger ValueTask hint.
	/// Uses explicit interface to allow ValueTask return type.
	/// </summary>
	[Fact]
	public async Task ValueTaskHandler_NoValueTaskHint()
	{
		// Custom interface stub that uses ValueTask
		const string valueTaskInterfaceStub = """
			namespace Excalibur.Dispatch.Abstractions
			{
			    public interface IDispatchMessage { }
			    public interface IDispatchHandler<in TMessage> where TMessage : IDispatchMessage
			    {
			        System.Threading.Tasks.ValueTask<object> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
			    }
			}
			""";

		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.ValueTask<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.ValueTask.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(valueTaskInterfaceStub);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies abstract handlers do not trigger optimization hints.
	/// </summary>
	[Fact]
	public async Task AbstractHandler_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public abstract class AbstractHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public abstract System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct);
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for abstract classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies non-handler classes do not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task NonHandlerClass_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class NonHandler
			    {
			        public System.Threading.Tasks.Task<string> HandleAsync(string message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for non-handler classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP004 is reported for IActionHandler implementations that are not sealed.
	/// Covers the IActionHandler branch in IsHandlerType.
	/// </summary>
	[Fact]
	public async Task UnsealedActionHandler_ReportsSealingHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class {|#0:TestActionHandler|} : Excalibur.Dispatch.Abstractions.IActionHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.CompletedTask;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// Expect sealing hint (IActionHandler handler that's not sealed)
		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Handler 'TestActionHandler' could be sealed for better performance (enables devirtualization)"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies sealed IActionHandler does not trigger sealing hint.
	/// </summary>
	[Fact]
	public async Task SealedActionHandler_NoSealingHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestActionHandler : Excalibur.Dispatch.Abstractions.IActionHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.CompletedTask;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected - sealed and Task (not Task<T>)
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies methods not named HandleAsync in handler types do not trigger ValueTask hint.
	/// </summary>
	[Fact]
	public async Task NonHandleAsyncMethod_InHandler_OnlyHandleAsyncTriggersHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> {|#0:HandleAsync|}(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>(ProcessAsync().Result);
			        }

			        public System.Threading.Tasks.Task<string> ProcessAsync()
			        {
			            return System.Threading.Tasks.Task.FromResult("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// Only expect ValueTask hint for HandleAsync, NOT for ProcessAsync
		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Method 'HandleAsync' returns Task<T>. Consider ValueTask<T> if synchronous completion is common for zero-allocation fast path"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies HandleAsync returning plain Task (not Task of T) does not trigger ValueTask hint.
	/// </summary>
	[Fact]
	public async Task HandleAsyncReturningPlainTask_NoValueTaskHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestActionHandler : Excalibur.Dispatch.Abstractions.IActionHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.CompletedTask;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics - sealed and returns plain Task (not Task<T>)
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies HandleAsync returning void does not trigger ValueTask hint.
	/// This covers the branch where return type is not INamedTypeSymbol.
	/// </summary>
	[Fact]
	public async Task HandleAsyncReturningVoid_InHandler_OnlyTaskTHandleAsyncTriggersHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public sealed class TestHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> {|#0:HandleAsync|}(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }

			        public void HandleAsync()
			        {
			            // This overload returns void - should not trigger ValueTask hint
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// Only the Task<object> HandleAsync triggers ValueTask hint
		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Method 'HandleAsync' returns Task<T>. Consider ValueTask<T> if synchronous completion is common for zero-allocation fast path"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies struct types do not trigger any optimization hints.
	/// </summary>
	[Fact]
	public async Task StructType_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public struct MyStruct
			    {
			        public int Value;
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for structs
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies interface types do not trigger optimization hints.
	/// </summary>
	[Fact]
	public async Task InterfaceType_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public interface IMyInterface
			    {
			        System.Threading.Tasks.Task<object> HandleAsync(string message, System.Threading.CancellationToken ct);
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for interfaces
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies that when IDispatchHandler is not available (not defined), no diagnostics are reported.
	/// This covers the null branch when GetTypeByMetadataName returns null.
	/// </summary>
	[Fact]
	public async Task HandlerWithoutDispatchAbstractions_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public System.Threading.Tasks.Task<object> HandleAsync(string message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No interface stubs added - both GetTypeByMetadataName calls return null
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected when handler interfaces are not available
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies that IActionHandler is checked when IDispatchHandler is not implemented.
	/// Covers the fallthrough from IDispatchHandler check to IActionHandler check.
	/// </summary>
	[Fact]
	public async Task ActionHandlerOnly_WithDispatchHandlerDefined_ReportsSealingHint()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class {|#0:TestActionHandler|} : Excalibur.Dispatch.Abstractions.IActionHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.CompletedTask;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<OptimizationHintAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// Both IDispatchHandler and IActionHandler are defined, but class only implements IActionHandler
		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.OptimizationHint)
				.WithLocation(0)
				.WithArguments("Handler 'TestActionHandler' could be sealed for better performance (enables devirtualization)"));

		await test.RunAsync();
	}
}
