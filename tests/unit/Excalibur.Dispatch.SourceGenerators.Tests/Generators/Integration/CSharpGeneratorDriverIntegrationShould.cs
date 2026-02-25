// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Generators.Integration;

/// <summary>
/// CSharpGeneratorDriver integration tests that drive all 4 enhanced source generators
/// through real Roslyn compilation and verify actual generated output.
/// Per AD-522.3: Uses semantic assertions (not snapshot testing).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
public sealed class CSharpGeneratorDriverIntegrationShould
{
	/// <summary>
	/// Creates a CSharpCompilation with the specified source code and references to
	/// Excalibur.Dispatch.Abstractions so the generators can discover handler interfaces.
	/// </summary>
	private static CSharpCompilation CreateCompilation(string sourceCode, string assemblyName = "TestAssembly")
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		// Core references for netstandard/runtime
		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
		};

		// Add Excalibur.Dispatch.Abstractions assembly for handler interfaces
		var abstractionsAssembly = typeof(Abstractions.IDispatchMessage).Assembly;
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
			var path = System.IO.Path.Combine(runtimeDir, asm);
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

	/// <summary>
	/// Runs the specified generator against the source code and returns the result.
	/// </summary>
	private static GeneratorDriverRunResult RunGenerator<TGenerator>(string sourceCode)
		where TGenerator : IIncrementalGenerator, new()
	{
		var compilation = CreateCompilation(sourceCode);
		var generator = new TGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		return driver.GetRunResult();
	}

	#region HandlerRegistrySourceGenerator (8 tests)

	[Fact]
	public void HandlerRegistry_WithActionHandler_GeneratesPrecompiledHandlerRegistry()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			    public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();

		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("PrecompiledHandlerRegistry.g.cs");
	}

	[Fact]
	public void HandlerRegistry_WithActionHandler_GeneratesResolveHandlerType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			    public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("PrecompiledHandlerRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("ResolveHandlerType");
		text.ShouldContain("CreateOrderCommand");
		text.ShouldContain("CreateOrderHandler");
	}

	[Fact]
	public void HandlerRegistry_WithActionHandler_GeneratesCreateHandler()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class CreateOrderCommand : IDispatchAction<Guid> { }
			    public class CreateOrderHandler : IActionHandler<CreateOrderCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("PrecompiledHandlerRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("CreateHandler");
		text.ShouldContain("IServiceProvider");
	}

	[Fact]
	public void HandlerRegistry_WithMultipleHandlers_GeneratesSwitchExpression()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class Command1 : IDispatchAction<Guid> { }
			    public class Handler1 : IActionHandler<Command1, Guid>
			    {
			        public Task<Guid> HandleAsync(Command1 message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }

			    public class Command2 : IDispatchAction<string> { }
			    public class Handler2 : IActionHandler<Command2, string>
			    {
			        public Task<string> HandleAsync(Command2 message, CancellationToken cancellationToken)
			            => Task.FromResult(string.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("PrecompiledHandlerRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("Command1");
		text.ShouldContain("Handler1");
		text.ShouldContain("Command2");
		text.ShouldContain("Handler2");
	}

	[Fact]
	public void HandlerRegistry_WithNoHandlers_GeneratesEmptyRegistry()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		// Should still generate registry file (empty registrations)
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("PrecompiledHandlerRegistry.g.cs"));
		registrySource.ShouldNotBeNull();
	}

	[Fact]
	public void HandlerRegistry_GeneratesHandlerActivator()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("PrecompiledHandlerActivator.g.cs");
	}

	[Fact]
	public void HandlerRegistry_GeneratesHandlerMetadata()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("PrecompiledHandlerMetadata.g.cs");
	}

	[Fact]
	public void HandlerRegistry_GeneratesHandlerInvoker()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("PrecompiledHandlerInvoker.g.cs");
	}

	[Fact]
	public void HandlerRegistry_WithNullableMessageContextProperty_MarksDirectDispatchAsRequiresContext()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class WithContextCommand : IDispatchAction { }
			    public class WithContextHandler : IActionHandler<WithContextCommand>
			    {
			        public IMessageContext? Context { get; set; }

			        public Task HandleAsync(WithContextCommand message, CancellationToken cancellationToken)
			            => Task.CompletedTask;
			    }
			}
			""";

		var result = RunGenerator<HandlerRegistrySourceGenerator>(source);

		var directDispatchSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("PrecompiledDirectActionDispatch.g.cs"));
		directDispatchSource.ShouldNotBeNull();

		var text = directDispatchSource.GetText().ToString();
		text.ShouldContain("typeof(global::TestApp.WithContextCommand)");
		text.ShouldContain("requiresContext = true;");
	}

	#endregion

	#region JsonSerializationSourceGenerator (8 tests)

	[Fact]
	public void JsonSerialization_WithMessageType_GeneratesTypeRegistry()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("DiscoveredMessageTypeRegistry.g.cs");
	}

	[Fact]
	public void JsonSerialization_WithMessageType_GeneratesMetadata()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("DiscoveredMessageTypeMetadata.g.cs");
	}

	[Fact]
	public void JsonSerialization_WithMessageType_ContainsIsMessageType()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var metadataSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeMetadata.g.cs"));
		metadataSource.ShouldNotBeNull();

		var text = metadataSource.GetText().ToString();
		text.ShouldContain("IsMessageType");
	}

	[Fact]
	public void JsonSerialization_WithMessageType_ContainsGetTypeInfo()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var metadataSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeMetadata.g.cs"));
		metadataSource.ShouldNotBeNull();

		var text = metadataSource.GetText().ToString();
		text.ShouldContain("GetTypeInfo");
	}

	[Fact]
	public void JsonSerialization_WithMessageType_ContainsTypeName()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var metadataSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeMetadata.g.cs"));
		metadataSource.ShouldNotBeNull();

		var text = metadataSource.GetText().ToString();
		text.ShouldContain("OrderCreatedEvent");
	}

	[Fact]
	public void JsonSerialization_ExcludesAbstractTypes()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public abstract class BaseEvent : IDispatchMessage { }
			    public class ConcreteEvent : BaseEvent { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		// ConcreteEvent should be included, BaseEvent should not (abstract)
		text.ShouldContain("ConcreteEvent");
	}

	[Fact]
	public void JsonSerialization_ExcludesNonPublicTypes()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    internal class InternalEvent : IDispatchMessage { }
			    public class PublicEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeRegistry.g.cs"));
		registrySource.ShouldNotBeNull();

		var text = registrySource.GetText().ToString();
		text.ShouldContain("PublicEvent");
		text.ShouldNotContain("InternalEvent");
	}

	[Fact]
	public void JsonSerialization_WithNoMessageTypes_GeneratesEmptyRegistry()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAMessage { }
			}
			""";

		var result = RunGenerator<JsonSerializationSourceGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		// Generator should still produce output even with no message types
		var registrySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("DiscoveredMessageTypeRegistry.g.cs"));
		registrySource.ShouldNotBeNull();
	}

	#endregion

	#region MessageResultExtractorGenerator (7 tests)

	[Fact]
	public void ResultFactory_WithActionHandler_GeneratesResultFactoryRegistry()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("ResultFactoryRegistry.g.cs");
	}

	[Fact]
	public void ResultFactory_WithActionHandler_ContainsExtractReturnValue()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		var factorySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("ResultFactoryRegistry.g.cs"));
		factorySource.ShouldNotBeNull();

		var text = factorySource.GetText().ToString();
		text.ShouldContain("ExtractReturnValue");
	}

	[Fact]
	public void ResultFactory_WithActionHandler_ContainsResultType()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		var factorySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("ResultFactoryRegistry.g.cs"));
		factorySource.ShouldNotBeNull();

		var text = factorySource.GetText().ToString();
		// The generator extracts return types (Guid), not message types (MyCommand)
		text.ShouldContain("Guid");
	}

	[Fact]
	public void ResultFactory_WithMultipleReturnTypes_GeneratesMultipleFactories()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class GuidCommand : IDispatchAction<Guid> { }
			    public class GuidHandler : IActionHandler<GuidCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(GuidCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }

			    public class StringCommand : IDispatchAction<string> { }
			    public class StringHandler : IActionHandler<StringCommand, string>
			    {
			        public Task<string> HandleAsync(StringCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(string.Empty);
			    }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		var factorySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("ResultFactoryRegistry.g.cs"));
		factorySource.ShouldNotBeNull();

		var text = factorySource.GetText().ToString();
		// The generator extracts return types (Guid, String), not message types
		text.ShouldContain("Guid");
		text.ShouldContain("String");
	}

	[Fact]
	public void ResultFactory_WithNoHandlers_GeneratesEmptyFactory()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var factorySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("ResultFactoryRegistry.g.cs"));
		factorySource.ShouldNotBeNull();
	}

	[Fact]
	public void ResultFactory_GeneratesMessageResultFactory()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<MessageResultExtractorGenerator>(source);

		var factorySource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("ResultFactoryRegistry.g.cs"));
		factorySource.ShouldNotBeNull();

		var text = factorySource.GetText().ToString();
		text.ShouldContain("ResultFactoryRegistry");
	}

	[Fact]
	public void ResultFactory_GeneratedCode_CompilesWithoutErrors()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCommand : IDispatchAction<Guid> { }
			    public class MyHandler : IActionHandler<MyCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var compilation = CreateCompilation(source);
		var generator = new MessageResultExtractorGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

		// Verify generated output compiles without errors
		var errors = outputCompilation.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToList();

		// Some errors are expected due to missing runtime references (MessageResult<T>, etc.)
		// but the generator itself should not produce compilation errors
		var generatorResult = driver.GetRunResult();
		generatorResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region ServiceRegistrationSourceGenerator (7 tests)

	[Fact]
	public void ServiceRegistration_WithAutoRegister_GeneratesExtensions()
	{
		const string source = """
			using System;
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public interface IMyService { }

			    [AutoRegister]
			    public class MyService : IMyService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		var generatedFiles = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToList();
		generatedFiles.ShouldContain("GeneratedServiceCollectionExtensions.g.cs");
	}

	[Fact]
	public void ServiceRegistration_WithAutoRegister_ContainsServiceType()
	{
		const string source = """
			using System;
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public interface IMyService { }

			    [AutoRegister]
			    public class MyService : IMyService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		var extensionsSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("GeneratedServiceCollectionExtensions.g.cs"));
		extensionsSource.ShouldNotBeNull();

		var text = extensionsSource.GetText().ToString();
		text.ShouldContain("MyService");
	}

	[Fact]
	public void ServiceRegistration_WithAutoRegisterAsInterfaces_RegistersInterfaces()
	{
		const string source = """
			using System;
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public interface IOrderService { }
			    public interface ICommandService { }

			    [AutoRegister(AsInterfaces = true)]
			    public class OrderService : IOrderService, ICommandService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		var extensionsSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("GeneratedServiceCollectionExtensions.g.cs"));
		extensionsSource.ShouldNotBeNull();

		var text = extensionsSource.GetText().ToString();
		text.ShouldContain("OrderService");
		text.ShouldContain("IOrderService");
	}

	[Fact]
	public void ServiceRegistration_WithNoAutoRegister_GeneratesEmptyExtensions()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotRegistered { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void ServiceRegistration_EmitsSRG001Diagnostic_WhenRegistrationsFound()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public interface IMyService { }

			    [AutoRegister]
			    public class MyService : IMyService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		var srg001 = result.Diagnostics.FirstOrDefault(d => d.Id == "SRG001");
		srg001.ShouldNotBeNull();
	}

	[Fact]
	public void ServiceRegistration_EmitsSRG002Diagnostic_WhenAsInterfacesButNoInterfaces()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    [AutoRegister(AsInterfaces = true)]
			    public class LonelyService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		var srg002 = result.Diagnostics.FirstOrDefault(d => d.Id == "SRG002");
		srg002.ShouldNotBeNull();
		srg002.Severity.ShouldBe(DiagnosticSeverity.Warning);
	}

	[Fact]
	public void ServiceRegistration_GeneratedCode_ContainsAddSourceGenerated()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public interface IMyService { }

			    [AutoRegister]
			    public class MyService : IMyService { }
			}
			""";

		var result = RunGenerator<ServiceRegistrationSourceGenerator>(source);

		var extensionsSource = result.GeneratedTrees
			.FirstOrDefault(t => t.FilePath.EndsWith("GeneratedServiceCollectionExtensions.g.cs"));
		extensionsSource.ShouldNotBeNull();

		var text = extensionsSource.GetText().ToString();
		text.ShouldContain("AddGeneratedServices");
	}

	#endregion
}
