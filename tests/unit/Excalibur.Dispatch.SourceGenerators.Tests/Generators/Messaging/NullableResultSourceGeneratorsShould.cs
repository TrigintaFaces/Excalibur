// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "SourceGenerators")]
[Trait("Pattern", "Regression")]
public sealed class NullableResultSourceGeneratorsShould
{
    [Theory]
    [InlineData("Payload?", false)]
    [InlineData("string?", false)]
    [InlineData("System.Collections.Generic.List<Payload?>?", false)]
    [InlineData("int?", false)]
    [InlineData("Payload", false)]
    [InlineData("Payload?", true)]
    public void CompileNullableResultsWithoutWarnings(string resultType, bool constructedMessage)
    {
        var declaration = constructedMessage ? "Find<T>" : "Find";
        var message = constructedMessage ? "Find<string?>" : "Find";
        var source = $$"""
            #nullable enable
            using System.Threading;
            using System.Threading.Tasks;
            using Excalibur.Dispatch;
            using Excalibur.Dispatch.Delivery;
            namespace Consumer;
            public sealed class Payload;
            public sealed class {{declaration}} : IDispatchAction<{{resultType}}>;
            public sealed class Handler : IActionHandler<{{message}}, {{resultType}}>
            {
                Task<{{resultType}}> IActionHandler<{{message}}, {{resultType}}>.HandleAsync({{message}} message, CancellationToken token)
                    => Task.FromResult<{{resultType}}>(default!);
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(IDispatchMessage).Assembly.Location)
            .Append(typeof(Excalibur.Dispatch.Delivery.Handlers.HandlerInvokerAot).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollection).Assembly.Location)
            .Append(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("NullableConsumer", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                generalDiagnosticOption: ReportDiagnostic.Error));
        compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new HandlerInvokerSourceGenerator(), new HandlerRegistrySourceGenerator(), new DispatchActionExtensionGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        var results = driver.GetRunResult().Results;
        results.Length.ShouldBe(3);
        results[0].GeneratedSources.ShouldNotBeEmpty();
        results[1].GeneratedSources.ShouldNotBeEmpty();
        if (!constructedMessage)
        {
            results[2].GeneratedSources.ShouldNotBeEmpty();
        }
        output.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        using var assembly = new MemoryStream();
        output.Emit(assembly).Success.ShouldBeTrue();
    }
}
