// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using Excalibur.Dispatch.Validation.FluentValidation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
[Trait("Pattern", "Regression")]
public sealed class FluentValidationDispatcherExecutionShould
{
    [Fact]
    public async Task AwaitRulesAndResolveValidatorsOncePerProvider()
    {
        const string source = """
            using Excalibur.Dispatch;
            using FluentValidation;
            using System.Threading.Tasks;
            namespace CompiledValidation;
            public sealed record Message : IDispatchMessage;
            public sealed class Validator : AbstractValidator<Message>
            {
                public static int Instances;
                public Validator()
                {
                    Instances++;
                    RuleFor(x => x).MustAsync(async (_, token) =>
                    {
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();
                        return false;
                    });
                }
            }
            """;
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[]
            {
                typeof(IDispatchMessage).Assembly.Location,
                typeof(IAotValidationDispatcher).Assembly.Location,
                typeof(FluentValidation.AbstractValidator<>).Assembly.Location,
                typeof(IServiceCollection).Assembly.Location,
            }).Distinct(StringComparer.OrdinalIgnoreCase);
        var compilation = CSharpCompilation.Create("CompiledValidation_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FluentValidationGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        diagnostics.ShouldBeEmpty();
        using var stream = new MemoryStream();
        var emitted = generated.Emit(stream);
        emitted.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var dispatcher = (IAotValidationDispatcher)Activator.CreateInstance(assembly.GetType(
            "Excalibur.Dispatch.Validation.FluentValidation.Generated.GeneratedFluentValidationDispatcher", true)!)!;
        var messageType = assembly.GetType("CompiledValidation.Message", true)!;
        var validatorType = assembly.GetType("CompiledValidation.Validator", true)!;
        var message = (IDispatchMessage)Activator.CreateInstance(messageType)!;
        await using var empty = new ServiceCollection().BuildServiceProvider();
        (await dispatcher.TryValidateAsync(message, empty)).ShouldBeNull();
        var services = new ServiceCollection();
        services.AddTransient(typeof(FluentValidation.IValidator<>).MakeGenericType(messageType), validatorType);
        await using var populated = services.BuildServiceProvider();
        (await dispatcher.TryValidateAsync(message, populated))!.IsValid.ShouldBeFalse();
        validatorType.GetField("Instances")!.GetValue(null).ShouldBe(1);
        (await dispatcher.TryValidateAsync(message, empty)).ShouldBeNull();
        (await dispatcher.TryValidateAsync(message, populated))!.IsValid.ShouldBeFalse();
        validatorType.GetField("Instances")!.GetValue(null).ShouldBe(2);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await dispatcher.TryValidateAsync(message, populated, cancellation.Token));
        validatorType.GetField("Instances")!.GetValue(null).ShouldBe(2);
    }
}
