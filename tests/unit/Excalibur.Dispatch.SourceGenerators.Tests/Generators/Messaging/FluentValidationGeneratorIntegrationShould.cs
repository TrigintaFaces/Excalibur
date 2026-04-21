// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

/// <summary>
/// Integration tests for <see cref="FluentValidationGenerator"/> using CSharpGeneratorDriver
/// to verify actual source generation output.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
public sealed class FluentValidationGeneratorIntegrationShould
{
	/// <summary>
	/// Creates a compilation with FluentValidation and Dispatch references so the generator
	/// can discover AbstractValidator&lt;T&gt; implementations.
	/// </summary>
	private static CSharpCompilation CreateCompilationWithFluentValidation(string sourceCode)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(IServiceProvider).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Abstractions.IDispatchMessage).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(FluentValidation.AbstractValidator<>).Assembly.Location),
		};

		var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
		string[] runtimeAssemblies =
		[
			"System.Runtime.dll",
			"System.Collections.dll",
			"System.Threading.Tasks.dll",
			"System.ComponentModel.dll",
			"System.ComponentModel.DataAnnotations.dll",
			"System.Linq.dll",
			"System.Linq.Expressions.dll",
			"netstandard.dll",
		];

		foreach (var asm in runtimeAssemblies)
		{
			var path = Path.Combine(runtimeDir, asm);
			if (File.Exists(path))
			{
				references.Add(MetadataReference.CreateFromFile(path));
			}
		}

		return CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static GeneratorDriverRunResult RunFluentValidationGenerator(string sourceCode)
	{
		var compilation = CreateCompilationWithFluentValidation(sourceCode);
		var generator = new FluentValidationGenerator();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
		return driver.GetRunResult();
	}

	[Fact]
	public void GenerateDispatcherForSingleValidator()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record CreateOrderCommand(string Name) : IDispatchMessage;

			public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
			{
			    public CreateOrderCommandValidator()
			    {
			        RuleFor(x => x.Name).NotEmpty();
			    }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("GeneratedFluentValidationDispatcher");
		generatedCode.ShouldContain("IAotValidationDispatcher");
		generatedCode.ShouldContain("global::TestApp.CreateOrderCommand m => ValidateTyped");
	}

	[Fact]
	public void GenerateDispatcherForMultipleValidators()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record CommandA(string Name) : IDispatchMessage;
			public sealed record CommandB(int Value) : IDispatchMessage;
			public sealed record CommandC(string Email) : IDispatchMessage;

			public sealed class CommandAValidator : AbstractValidator<CommandA>
			{
			    public CommandAValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}

			public sealed class CommandBValidator : AbstractValidator<CommandB>
			{
			    public CommandBValidator() { RuleFor(x => x.Value).GreaterThan(0); }
			}

			public sealed class CommandCValidator : AbstractValidator<CommandC>
			{
			    public CommandCValidator() { RuleFor(x => x.Email).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("global::TestApp.CommandA m => ValidateTyped");
		generatedCode.ShouldContain("global::TestApp.CommandB m => ValidateTyped");
		generatedCode.ShouldContain("global::TestApp.CommandC m => ValidateTyped");
	}

	[Fact]
	public void DeduplicateMessageTypesWithMultipleValidatorsPerType()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name, int Age) : IDispatchMessage;

			public sealed class NameValidator : AbstractValidator<MyCommand>
			{
			    public NameValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}

			public sealed class AgeValidator : AbstractValidator<MyCommand>
			{
			    public AgeValidator() { RuleFor(x => x.Age).GreaterThan(0); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		var generatedCode = result.GeneratedTrees[0].GetText().ToString();

		// Should have exactly one switch arm for MyCommand, not two
		var count = CountOccurrences(generatedCode, "global::TestApp.MyCommand m => ValidateTyped");
		count.ShouldBe(1);
	}

	[Fact]
	public void GenerateNothingWhenNoValidatorsExist()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(0);
	}

	[Fact]
	public void IgnoreValidatorsForNonDispatchMessageTypes()
	{
		const string source = """
			using FluentValidation;

			namespace TestApp;

			public sealed record PlainDto(string Name);

			public sealed class PlainDtoValidator : AbstractValidator<PlainDto>
			{
			    public PlainDtoValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(0);
	}

	[Fact]
	public void IgnoreAbstractValidatorSubclasses()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public abstract class BaseCommandValidator<T> : AbstractValidator<T> where T : IDispatchMessage { }
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(0);
	}

	[Fact]
	public void GenerateDiExtensionMethod()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public sealed class MyCommandValidator : AbstractValidator<MyCommand>
			{
			    public MyCommandValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("GeneratedFluentValidationExtensions");
		generatedCode.ShouldContain("AddGeneratedFluentValidationDispatcher");
		generatedCode.ShouldContain("TryAddSingleton");
	}

	[Fact]
	public void GenerateValidateTypedHelperMethod()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public sealed class MyCommandValidator : AbstractValidator<MyCommand>
			{
			    public MyCommandValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("private static");
		generatedCode.ShouldContain("ValidateTyped<TMessage>");
		generatedCode.ShouldContain("GetServices<global::FluentValidation.IValidator<TMessage>>");
		generatedCode.ShouldContain("SerializableValidationResult.Success()");
		generatedCode.ShouldContain("SerializableValidationResult.Failed(errors)");
	}

	[Fact]
	public void HandleValidatorsInDeeperInheritanceChain()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public abstract class BaseMyValidator : AbstractValidator<MyCommand> { }

			public sealed class ConcreteMyValidator : BaseMyValidator
			{
			    public ConcreteMyValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		result.Diagnostics.ShouldBeEmpty();
		result.GeneratedTrees.Length.ShouldBe(1);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("global::TestApp.MyCommand m => ValidateTyped");
	}

	[Fact]
	public void IncludeGeneratedCodeAttribute()
	{
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public sealed class MyCommandValidator : AbstractValidator<MyCommand>
			{
			    public MyCommandValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("[global::System.CodeDom.Compiler.GeneratedCode(\"FluentValidationGenerator\", \"1.0\")]");
	}

	[Fact]
	public void GenerateValidatorPresenceCacheField()
	{
		// Arrange - Sprint 742: FluentValidation generator caches validator presence (bool)
		// in a ConcurrentDictionary to avoid repeated DI probing per message type.
		// Validator instances are resolved fresh each call to respect DI scoping.
		const string source = """
			using Excalibur.Dispatch.Abstractions;
			using FluentValidation;

			namespace TestApp;

			public sealed record MyCommand(string Name) : IDispatchMessage;

			public sealed class MyCommandValidator : AbstractValidator<MyCommand>
			{
			    public MyCommandValidator() { RuleFor(x => x.Name).NotEmpty(); }
			}
			""";

		var result = RunFluentValidationGenerator(source);

		var generatedCode = result.GeneratedTrees[0].GetText().ToString();
		generatedCode.ShouldContain("ConcurrentDictionary");
		generatedCode.ShouldContain("_hasValidatorsCache");
	}

	private static int CountOccurrences(string text, string pattern)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
		{
			count++;
			index += pattern.Length;
		}

		return count;
	}
}
