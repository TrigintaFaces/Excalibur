// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RepositoryChecks;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class TestTimingRuleShould
{
    [Theory]
    [InlineData("_ = Task.Delay(50);", 1)]
    [InlineData("_ = T.Delay(50);", 1)]
    [InlineData("_ = Task.Delay(\n 50\n);", 1)]
    [InlineData("// Task.Delay(50);", 0)]
    [InlineData("var text = \"Task.Delay(50)\";", 0)]
    [InlineData("_ = Task.Delay(-1);", 0)]
    [InlineData("_ = Task.Delay((int)-1.5);", 0)]
    [InlineData("_ = Task.Delay(Timeout.Infinite);", 0)]
    [InlineData("_ = Task.Delay(Timeout.InfiniteTimeSpan);", 0)]
    [InlineData("_ = Task.Delay(cancellationToken: default, millisecondsDelay: -1);", 0)]
    [InlineData("_ = Task.Delay(cancellationToken: default, millisecondsDelay: 50);", 1)]
    [InlineData("_ = Task.WhenAny(Task.CompletedTask, Task.Delay(50));", 0)]
    [InlineData("_ = Task.WhenAny(new[] { Task.CompletedTask, Task.Delay(50) });", 0)]
    [InlineData("_ = Task.WhenAny([Task.CompletedTask, Task.Delay(50)]);", 0)]
    [InlineData("_ = Task.WhenAny(Task.Run(() => Task.Delay(50)), Task.CompletedTask);", 1)]
    [InlineData("_ = Task.WhenAny(Task.CompletedTask); _ = Task.Delay(50);", 1)]
    [InlineData("_ = WhenAny(Task.Delay(50));", 1)]
    [InlineData("_ = Task.WhenAny(WhenAny(Task.Delay(50)));", 1)]
    [InlineData("_ = Task.Delay(50); // delay-ok: simulated work", 0)]
    [InlineData("_ = Task.Delay(50); // delay-ok:", 1)]
    [InlineData("_ = Task.Delay(\n 50 // delay-ok: simulated work\n);", 0)]
    [InlineData("_ = Task.Delay(50); _ = Task.CompletedTask; // delay-ok: unrelated statement", 1)]
    [InlineData("using var c = new CancellationTokenSource(50);", 1)]
    [InlineData("using CTS c = new(50);", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(9));", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(10));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(9, 999));", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(9, 1000));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromSeconds(0.5));", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromMilliseconds(9999));", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromMilliseconds(10000));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.Zero);", 1)]
    [InlineData("using var c = new CTS(new TimeSpan(500));", 1)]
    [InlineData("using var c = new CTS(new TimeSpan(0, 0, 9));", 1)]
    [InlineData("using var c = new CTS(new TimeSpan(0, 0, 10));", 0)]
    [InlineData("using var c = new CTS(TimeSpan.FromMinutes(1));", 0)]
    [InlineData("using var c = new CTS(-1);", 0)]
    [InlineData("using var c = new CTS();", 0)]
    [InlineData("using (var c = new CTS(50)) // deadline-ok: cancellation under test\n { }", 0)]
    [InlineData("using (var c = new CTS(50)) { _ = Task.CompletedTask; // deadline-ok: unrelated body statement\n }", 1)]
    [InlineData("using var c = new CTS(TimeSpan.FromMilliseconds(duration));", 1)]
    [InlineData("using var c = new CTS(50); // deadline-ok: cancellation behavior under test", 0)]
    public void EnforceResolvedCallsAndActualDurations(string body, int expected) => Inspect(body).Length.ShouldBe(expected);

    [Fact]
    public void IgnoreSameNamedApplicationMethods() =>
        Inspect("_ = Other.Task.Delay(50);", "namespace Other { public static class Task { public static int Delay(int value) => value; } }").ShouldBeEmpty();

    [Theory]
    [InlineData("tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs")]
    [InlineData("tests/performance/Pacing.cs")]
    public void KeepTimingInfrastructureExempt(string path) => Inspect("_ = Task.Delay(50);", path: path).ShouldBeEmpty();

    [Fact]
    public void RefuseNearMissInfrastructurePaths() =>
        Inspect("_ = Task.Delay(50);", path: "tests/unit/WaitHelpers.cs").ShouldHaveSingleItem();

    private static string[] Inspect(string body, string extra = "", string path = "tests/unit/TimingFixture.cs")
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using T = System.Threading.Tasks.Task;
            using CTS = System.Threading.CancellationTokenSource;
            public class Fixture {
                private static Task WhenAny(Task task) => task;
                public void Run(double duration) {
            """ + "\n" + body + "\n} }\n" + extra, path: path);
        var compilation = CSharpCompilation.Create("TimingFixture", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return TestTimingRule.Find(compilation, tree, path);
    }
}
