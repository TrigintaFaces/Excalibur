// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using RepositoryAnalyzers;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class MessageIdentityPolicyShould
{
    private static readonly string[] Configurations = ["Debug", "Release"];
    private static readonly string[] AnalyzerProperties = ["RunAnalyzers", "RunAnalyzersDuringBuild"];
    [Theory]
    [InlineData("MessageType = item.GetType().FullName;", 1)]
    [InlineData("EventType = item.GetType().Name;", 1)]
    [InlineData("MessageType = item.GetType().AssemblyQualifiedName;", 1)]
    [InlineData("MessageType = typeof(string).FullName;", 1)]
    [InlineData("var MessageType = item.GetType().FullName;", 1)]
    [InlineData("MessageType = item\n.GetType()\n.FullName;", 1)]
    [InlineData("MessageType = \"item.GetType().FullName\";", 0)]
    [InlineData("// MessageType = item.GetType().FullName;", 0)]
    [InlineData("var label = item.GetType().FullName;", 0)]
    [InlineData("MessageType = \"Declared.Name\";", 0)]
    public Task CheckActualIdentityAssignments(string body, int expected) => Verify(
        "class C { string MessageType; string EventType; void M(object item) { " + body + "\n } }", expected);

    [Theory]
    [InlineData("type.FullName", 1)]
    [InlineData("type.AssemblyQualifiedName", 1)]
    [InlineData("\"Declared.Name\"", 0)]
    public Task CheckResolvedNamingReturns(string expression, int expected) => Verify(
        "class C { string GetTypeName(System.Type type) => " + expression + "; }", expected);

    [Theory]
    [InlineData("System.Func<string> name = () => type.FullName; return name();")]
    [InlineData("string Name() => type.FullName; return Name();")]
    public Task InspectNestedFunctionsInsideTheNamingContract(string body) => Verify(
        "class C { string GetTypeName(System.Type type) { " + body + " } }", 1);

    [Fact]
    public Task IgnoreUnrelatedPropertiesWithTheSameSpelling() => Verify(
        "class C { string MessageType; void M(Fake item) { MessageType = item.FullName; } } class Fake { public string FullName => \"name\"; }", 0);

    [Fact]
    public Task RejectClrNamesPassedToTheContextIdentitySetter() => Verify(
        "namespace Excalibur.Dispatch { static class MessageContextExtensions { public static void SetMessageType(string name) {} } } " +
        "class C { void M(object item) => Excalibur.Dispatch.MessageContextExtensions.SetMessageType(item.GetType().FullName); }", 1);

    [Fact]
    public Task IgnoreAnUnrelatedIdentitySetter() => Verify(
        "class C { void SetMessageType(string text) {} void M(object item) => SetMessageType(item.GetType().FullName); }", 0);

    [Theory]
    [InlineData("MessageType = item.GetType().FullName;", 1)]
    [InlineData("MessageType = item.GetType().Name;", 1)]
    [InlineData("var label = item.GetType().FullName;", 0)]
    public Task CheckDynamicallyBoundIdentityDerivation(string body, int expected) => Verify(
        "class C { string MessageType; void M(dynamic item) { " + body + " } }", expected);

    [Theory]
    [InlineData("Excalibur.EventSourcing.SqlServer", 1)]
    [InlineData("Excalibur.Dispatch", 0)]
    public Task PreventStoresFromRenamingAlreadyNamedEvents(string assembly, int expected) => Verify(
        "namespace Excalibur.Dispatch { static class MessageNameHelper { public static string GetName(System.Type type) => \"declared\"; } } " +
        "class C { string M() => Excalibur.Dispatch.MessageNameHelper.GetName(typeof(string)); }", expected, assembly);

    [Fact]
    public async Task ApplyTheCompilerPolicyToEveryShippingSourceProject()
    {
        var root = TestHelpers.GetRepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories);
        projects.Length.ShouldBeGreaterThan(100);
        await Parallel.ForEachAsync(projects, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (project, token) =>
        {
            foreach (var configuration in Configurations)
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var argument in new[] { "msbuild", project, "-nologo", "-p:Configuration=" + configuration,
                    "-getProperty:RunAnalyzers,RunAnalyzersDuringBuild,RepositoryMessageIdentityPolicy", "-getItem:ProjectReference,CompilerVisibleProperty" })
                {
                    process.StartInfo.ArgumentList.Add(argument);
                }

                process.Start().ShouldBeTrue();
                var stdout = process.StandardOutput.ReadToEndAsync(token);
                var stderr = process.StandardError.ReadToEndAsync(token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { process.Kill(entireProcessTree: true); throw; }
                process.ExitCode.ShouldBe(0, await stderr);
                using var evaluated = JsonDocument.Parse(await stdout);
                var properties = evaluated.RootElement.GetProperty("Properties");
                properties.GetProperty("RepositoryMessageIdentityPolicy").GetString().ShouldBe("true", project);
                foreach (var property in AnalyzerProperties)
                {
                    string.Equals(properties.GetProperty(property).GetString(), "false", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(project);
                }

                var items = evaluated.RootElement.GetProperty("Items");
                items.GetProperty("CompilerVisibleProperty").EnumerateArray().Any(item =>
                    item.GetProperty("Identity").GetString() == "RepositoryMessageIdentityPolicy").ShouldBeTrue(project);
                var expected = Path.GetFullPath(Path.Combine(root, "eng/tools/RepositoryAnalyzers/RepositoryAnalyzers.csproj"));
                items.GetProperty("ProjectReference").EnumerateArray().Count(reference =>
                    string.Equals(Path.GetFullPath(reference.GetProperty("FullPath").GetString()!), expected,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    reference.GetProperty("OutputItemType").GetString() == "Analyzer" &&
                    reference.GetProperty("ReferenceOutputAssembly").GetString() == "false" &&
                    reference.GetProperty("PrivateAssets").GetString() == "all").ShouldBe(1, project);
            }
        });
    }

    private static async Task Verify(string source, int expected, string assembly = "PolicyProbe")
    {
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
             MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var options = new AnalyzerOptions([], new RepositoryPolicyOptions("RepositoryMessageIdentityPolicy"));
        var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MessageIdentityAnalyzer()), options)
            .GetAnalyzerDiagnosticsAsync();
        diagnostics.Length.ShouldBe(expected);
        foreach (var diagnostic in diagnostics)
        {
            diagnostic.Id.ShouldBe("REPO002");
            diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
            diagnostic.Location.IsInSource.ShouldBeTrue();
        }
    }
}
