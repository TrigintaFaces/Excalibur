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
/// Unit tests for <see cref="HandlerDiscoverabilityAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HandlerDiscoverabilityAnalyzerTests
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
		    [System.AttributeUsage(System.AttributeTargets.Class)]
		    public class AutoRegisterAttribute : System.Attribute { }
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
		    [System.AttributeUsage(System.AttributeTargets.Class)]
		    public class AutoRegisterAttribute : System.Attribute { }
		}
		""";

	/// <summary>
	/// Interface stubs including IStreamingDocumentHandler for testing that branch.
	/// </summary>
	private const string StreamingHandlerInterfaceStubs = """
		namespace Excalibur.Dispatch.Abstractions
		{
		    public interface IDispatchMessage { }
		    public interface IDispatchHandler<in TMessage> where TMessage : IDispatchMessage
		    {
		        System.Threading.Tasks.Task<object> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		    public interface IStreamingDocumentHandler<in TMessage, TResult> where TMessage : IDispatchMessage
		    {
		        System.Collections.Generic.IAsyncEnumerable<TResult> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		    [System.AttributeUsage(System.AttributeTargets.Class)]
		    public class AutoRegisterAttribute : System.Attribute { }
		}
		""";

	/// <summary>
	/// Interface stubs for the alternate Delivery namespace.
	/// </summary>
	private const string DeliveryNamespaceInterfaceStubs = """
		namespace Excalibur.Dispatch.Abstractions.Delivery
		{
		    public interface IDispatchMessage { }
		    public interface IDispatchHandler<in TMessage> where TMessage : IDispatchMessage
		    {
		        System.Threading.Tasks.Task<object> HandleAsync(TMessage message, System.Threading.CancellationToken ct);
		    }
		}
		namespace Excalibur.Dispatch.Abstractions
		{
		    [System.AttributeUsage(System.AttributeTargets.Class)]
		    public class AutoRegisterAttribute : System.Attribute { }
		}
		""";

	/// <summary>
	/// Verifies DISP001 is reported when a handler lacks [AutoRegister] attribute.
	/// </summary>
	[Fact]
	public async Task Handler_WithoutAutoRegister_ReportsDISP001()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class {|#0:TestHandler|} : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.HandlerNotDiscoverable)
				.WithLocation(0)
				.WithArguments("TestHandler", "IDispatchHandler<TestMessage>"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.MissingAutoRegisterAttribute)
				.WithLocation(0)
				.WithArguments("TestHandler"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics are reported when handler has [AutoRegister].
	/// </summary>
	[Fact]
	public async Task Handler_WithAutoRegister_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    [Excalibur.Dispatch.Abstractions.AutoRegister]
			    public class TestHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies abstract handler classes do not trigger diagnostics.
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

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for abstract classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies private handlers do not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task PrivateHandler_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class OuterClass
			    {
			        private class PrivateHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			        {
			            public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			            {
			                return System.Threading.Tasks.Task.FromResult<object>("done");
			            }
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for private classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies protected handlers do not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task ProtectedHandler_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class OuterClass
			    {
			        protected class ProtectedHandler : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			        {
			            public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			            {
			                return System.Threading.Tasks.Task.FromResult<object>("done");
			            }
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for protected classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies internal handlers trigger diagnostics (they are discoverable).
	/// </summary>
	[Fact]
	public async Task InternalHandler_WithoutAutoRegister_ReportsDISP001()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    internal class {|#0:InternalHandler|} : Excalibur.Dispatch.Abstractions.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.HandlerNotDiscoverable)
				.WithLocation(0)
				.WithArguments("InternalHandler", "IDispatchHandler<TestMessage>"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.MissingAutoRegisterAttribute)
				.WithLocation(0)
				.WithArguments("InternalHandler"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies structs do not trigger diagnostics (TypeKind != Class).
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

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for structs
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies a class not implementing any handler interface does not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task NonHandlerClass_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class RegularClass
			    {
			        public void DoSomething() { }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected for non-handler classes
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP001 is reported for IActionHandler implementations without AutoRegister.
	/// </summary>
	[Fact]
	public async Task ActionHandler_WithoutAutoRegister_ReportsDISP001()
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

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.HandlerNotDiscoverable)
				.WithLocation(0)
				.WithArguments("TestActionHandler", "IActionHandler<TestMessage>"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.MissingAutoRegisterAttribute)
				.WithLocation(0)
				.WithArguments("TestActionHandler"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP001 is reported for IStreamingDocumentHandler implementations without AutoRegister.
	/// </summary>
	[Fact]
	public async Task StreamingHandler_WithoutAutoRegister_ReportsDISP001()
	{
		const string testCode = """
			using System.Collections.Generic;

			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    public class {|#0:TestStreamingHandler|} : Excalibur.Dispatch.Abstractions.IStreamingDocumentHandler<TestMessage, string>
			    {
			        public async IAsyncEnumerable<string> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            yield return "result";
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(StreamingHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.HandlerNotDiscoverable)
				.WithLocation(0)
				.WithArguments("TestStreamingHandler", "IStreamingDocumentHandler<TestMessage, string>"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.MissingAutoRegisterAttribute)
				.WithLocation(0)
				.WithArguments("TestStreamingHandler"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP001 is reported for handler implementing the alternate Delivery namespace interface.
	/// </summary>
	[Fact]
	public async Task DeliveryNamespaceHandler_WithoutAutoRegister_ReportsDISP001()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.Delivery.IDispatchMessage { }

			    public class {|#0:DeliveryHandler|} : Excalibur.Dispatch.Abstractions.Delivery.IDispatchHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task<object> HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.FromResult<object>("done");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(DeliveryNamespaceInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.HandlerNotDiscoverable)
				.WithLocation(0)
				.WithArguments("DeliveryHandler", "IDispatchHandler<TestMessage>"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.MissingAutoRegisterAttribute)
				.WithLocation(0)
				.WithArguments("DeliveryHandler"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics for IActionHandler with [AutoRegister] attribute.
	/// </summary>
	[Fact]
	public async Task ActionHandler_WithAutoRegister_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public class TestMessage : Excalibur.Dispatch.Abstractions.IDispatchMessage { }

			    [Excalibur.Dispatch.Abstractions.AutoRegister]
			    public class TestActionHandler : Excalibur.Dispatch.Abstractions.IActionHandler<TestMessage>
			    {
			        public System.Threading.Tasks.Task HandleAsync(TestMessage message, System.Threading.CancellationToken ct)
			        {
			            return System.Threading.Tasks.Task.CompletedTask;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(ActionHandlerInterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies class implementing non-generic interface (not a handler) does not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task ClassWithNonGenericInterface_NoDiagnostics()
	{
		const string testCode = """
			namespace TestNamespace
			{
			    public interface INotAHandler { }

			    public class TestClass : INotAHandler
			    {
			        public void DoSomething() { }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies class implementing unrelated generic interface does not trigger diagnostics.
	/// </summary>
	[Fact]
	public async Task ClassWithUnrelatedGenericInterface_NoDiagnostics()
	{
		const string testCode = """
			using System.Collections.Generic;

			namespace TestNamespace
			{
			    public class TestClass : IEnumerable<string>
			    {
			        public IEnumerator<string> GetEnumerator() => throw new System.NotImplementedException();
			        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<HandlerDiscoverabilityAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.TestState.Sources.Add(InterfaceStubs);
		test.TestState.Sources.Add(testCode);

		// No diagnostics expected
		await test.RunAsync();
	}
}
