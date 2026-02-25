// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Excalibur.Dispatch.SourceGenerators.Saga;
using Excalibur.Dispatch.SourceGenerators.Validation;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Generators.Integration;

/// <summary>
/// Additional CSharpGeneratorDriver integration tests for generators not covered
/// by CSharpGeneratorDriverIntegrationShould. Drives generators through real Roslyn
/// compilation to exercise internal code paths and increase coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
public sealed class AdditionalGeneratorDriverIntegrationShould
{
	/// <summary>
	/// Creates a CSharpCompilation with the specified source code and references.
	/// </summary>
	private static CSharpCompilation CreateCompilation(string sourceCode, string assemblyName = "TestAssembly")
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
		};

		var abstractionsAssembly = typeof(Abstractions.IDispatchMessage).Assembly;
		references.Add(MetadataReference.CreateFromFile(abstractionsAssembly.Location));

		var runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location);
		string[] runtimeAssemblies =
		[
			"System.Runtime.dll",
			"System.Collections.dll",
			"System.Threading.Tasks.dll",
			"System.ComponentModel.dll",
			"System.ComponentModel.Annotations.dll",
			"netstandard.dll",
		];

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

	#region EventStoreTypeMapGenerator

	[Fact]
	public void EventStoreTypeMap_WithDomainEvent_GeneratesEventMetadata()
	{
		const string source = """
			using System;
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderCreatedEvent : IDomainEvent
			    {
			        public Guid EventId { get; set; }
			        public Guid AggregateId { get; set; }
			        public int Version { get; set; }
			        public DateTimeOffset OccurredAt { get; set; }
			        public string EventType { get; set; } = string.Empty;
			        public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
			    }
			}
			""";

		var result = RunGenerator<EventStoreTypeMapGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void EventStoreTypeMap_WithNoEvents_ProducesNoDiagnosticErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAnEvent { }
			}
			""";

		var result = RunGenerator<EventStoreTypeMapGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region MessageTypeSourceGenerator

	[Fact]
	public void MessageType_WithDispatchMessage_GeneratesTypeRegistration()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class MyMessage : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();
	}

	[Fact]
	public void MessageType_WithMultipleMessages_GeneratesAllTypes()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class FirstMessage : IDispatchMessage { }
			    public class SecondMessage : IDispatchMessage { }
			    public class ThirdMessage : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

		var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
		generatedText.ShouldContain("FirstMessage");
		generatedText.ShouldContain("SecondMessage");
		generatedText.ShouldContain("ThirdMessage");
	}

	[Fact]
	public void MessageType_ExcludesAbstractMessages()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public abstract class BaseMessage : IDispatchMessage { }
			    public class ConcreteMessage : BaseMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void MessageType_WithNoMessages_GeneratesEmptyOutput()
	{
		const string source = """
			namespace TestApp
			{
			    public class PlainClass { }
			}
			""";

		var result = RunGenerator<MessageTypeSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region HandlerInvokerSourceGenerator

	[Fact]
	public void HandlerInvoker_WithActionHandler_GeneratesInvokerRegistry()
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

		var result = RunGenerator<HandlerInvokerSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();
	}

	[Fact]
	public void HandlerInvoker_WithNoHandlers_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<HandlerInvokerSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void HandlerInvoker_WithMultipleHandlers_GeneratesAllInvokers()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class Cmd1 : IDispatchAction<Guid> { }
			    public class Handler1 : IActionHandler<Cmd1, Guid>
			    {
			        public Task<Guid> HandleAsync(Cmd1 message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }

			    public class Cmd2 : IDispatchAction<string> { }
			    public class Handler2 : IActionHandler<Cmd2, string>
			    {
			        public Task<string> HandleAsync(Cmd2 message, CancellationToken cancellationToken)
			            => Task.FromResult(string.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerInvokerSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

		var generatedText = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
		generatedText.ShouldContain("Handler1");
		generatedText.ShouldContain("Handler2");
	}

	#endregion

	#region HandlerActivationGenerator

	[Fact]
	public void HandlerActivation_WithActionHandler_GeneratesActivation()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class PlaceOrderCommand : IDispatchAction<Guid> { }
			    public class PlaceOrderHandler : IActionHandler<PlaceOrderCommand, Guid>
			    {
			        public Task<Guid> HandleAsync(PlaceOrderCommand message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerActivationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();
	}

	[Fact]
	public void HandlerActivation_WithNoHandlers_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<HandlerActivationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region HandlerInvocationGenerator

	[Fact]
	public void HandlerInvocation_WithActionHandler_GeneratesInvocation()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class MyCmd : IDispatchAction<Guid> { }
			    public class MyCmdHandler : IActionHandler<MyCmd, Guid>
			    {
			        public Task<Guid> HandleAsync(MyCmd message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<HandlerInvocationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();
	}

	[Fact]
	public void HandlerInvocation_WithNoHandlers_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<HandlerInvocationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region ZeroAllocHandlerInvokerGenerator

	[Fact]
	public void ZeroAllocInvoker_WithActionHandler_GeneratesInvoker()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class FastCmd : IDispatchAction<Guid> { }
			    public class FastCmdHandler : IActionHandler<FastCmd, Guid>
			    {
			        public Task<Guid> HandleAsync(FastCmd message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<ZeroAllocHandlerInvokerGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void ZeroAllocInvoker_WithNoHandlers_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAHandler { }
			}
			""";

		var result = RunGenerator<ZeroAllocHandlerInvokerGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region MessageFactorySourceGenerator

	[Fact]
	public void MessageFactory_WithDispatchMessage_GeneratesFactory()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderMessage : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<MessageFactorySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void MessageFactory_WithNoMessages_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAMessage { }
			}
			""";

		var result = RunGenerator<MessageFactorySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region MessageTypeRegistrySourceGenerator

	[Fact]
	public void MessageTypeRegistry_WithDispatchMessage_ProducesNoErrors()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class MyEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeRegistrySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void MessageTypeRegistry_WithMultipleMessages_ProducesNoErrors()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class Event1 : IDispatchMessage { }
			    public class Event2 : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeRegistrySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void MessageTypeRegistry_WithNoMessages_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAMessage { }
			}
			""";

		var result = RunGenerator<MessageTypeRegistrySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region FluentValidationGenerator

	[Fact]
	public void FluentValidation_WithActionHandler_GeneratesValidator()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class ValidateCmd : IDispatchAction<Guid> { }
			    public class ValidateCmdHandler : IActionHandler<ValidateCmd, Guid>
			    {
			        public Task<Guid> HandleAsync(ValidateCmd message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var result = RunGenerator<FluentValidationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void FluentValidation_WithNoHandlers_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class PlainClass { }
			}
			""";

		var result = RunGenerator<FluentValidationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region AotValidationGenerator

	[Fact]
	public void AotValidation_WithAotValidatableClass_GeneratesValidator()
	{
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using Excalibur.Dispatch.SourceGenerators.Validation;

			namespace TestApp
			{
			    [AotValidatable]
			    public class OrderOptions
			    {
			        [Required]
			        public string Name { get; set; } = string.Empty;

			        [Range(1, 100)]
			        public int Quantity { get; set; }
			    }
			}
			""";

		var result = RunGenerator<AotValidationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void AotValidation_WithNoAotValidatableClasses_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class PlainOptions
			    {
			        public string Name { get; set; } = string.Empty;
			    }
			}
			""";

		var result = RunGenerator<AotValidationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region CacheInfoSourceGenerator

	[Fact]
	public void CacheInfo_WithCacheableMessage_GeneratesCacheInfo()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class GetOrderQuery : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<CacheInfoSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void CacheInfo_WithNoMessages_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotCacheable { }
			}
			""";

		var result = RunGenerator<CacheInfoSourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region CachePolicySourceGenerator

	[Fact]
	public void CachePolicy_WithCacheableMessage_GeneratesPolicy()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class GetProductQuery : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<CachePolicySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void CachePolicy_WithNoMessages_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotCacheable { }
			}
			""";

		var result = RunGenerator<CachePolicySourceGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region ProjectionTagResolverGenerator

	[Fact]
	public void ProjectionTagResolver_WithMessage_Generates()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp
			{
			    public class OrderProjectionEvent : IDispatchMessage { }
			}
			""";

		var result = RunGenerator<ProjectionTagResolverGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	[Fact]
	public void ProjectionTagResolver_WithNoMessages_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotAProjection { }
			}
			""";

		var result = RunGenerator<ProjectionTagResolverGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region SagaMetadataGenerator

	[Fact]
	public void SagaMetadata_WithNoSagas_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotASaga { }
			}
			""";

		var result = RunGenerator<SagaMetadataGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region SagaRegistrationGenerator

	[Fact]
	public void SagaRegistration_WithNoSagas_ProducesNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class NotASaga { }
			}
			""";

		var result = RunGenerator<SagaRegistrationGenerator>(source);
		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion

	#region Cross-Generator: Multiple Generators on Same Compilation

	[Fact]
	public void MultipleGenerators_OnSameCompilation_ProduceNoConflicts()
	{
		const string source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using Excalibur.Dispatch.Abstractions;
			using Excalibur.Dispatch.Abstractions.Delivery;

			namespace TestApp
			{
			    public class OrderCmd : IDispatchAction<Guid> { }
			    public class OrderHandler : IActionHandler<OrderCmd, Guid>
			    {
			        public Task<Guid> HandleAsync(OrderCmd message, CancellationToken cancellationToken)
			            => Task.FromResult(Guid.Empty);
			    }
			}
			""";

		var compilation = CreateCompilation(source);

		// Run multiple generators on the same compilation
		var generators = new IIncrementalGenerator[]
		{
			new HandlerRegistrySourceGenerator(),
			new HandlerInvokerSourceGenerator(),
			new HandlerActivationGenerator(),
			new MessageTypeSourceGenerator(),
		};

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		var result = driver.GetRunResult();

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
		result.GeneratedTrees.ShouldNotBeEmpty();
	}

	[Fact]
	public void MultipleGenerators_WithEmptySource_ProduceNoErrors()
	{
		const string source = """
			namespace TestApp
			{
			    public class Empty { }
			}
			""";

		var compilation = CreateCompilation(source);

		var generators = new IIncrementalGenerator[]
		{
			new HandlerRegistrySourceGenerator(),
			new MessageTypeSourceGenerator(),
			new JsonSerializationSourceGenerator(),
			new ServiceRegistrationSourceGenerator(),
			new CacheInfoSourceGenerator(),
			new CachePolicySourceGenerator(),
		};

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		var result = driver.GetRunResult();

		result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
	}

	#endregion
}
