// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Reflection;
using Excalibur.Dispatch.Benchmarks.Patterns;
using Tests.Shared.Fixtures;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Collection("BenchmarkValidationLifecycle")]
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class SqlBenchmarkOwnershipShould
{
    [Theory]
    [InlineData("")]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    public async Task RejectInvalidOwnershipBeforeCreatingAnyContainer(string token)
    {
        var previous = Environment.GetEnvironmentVariable("DISPATCH_BENCHMARK_OWNER_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("DISPATCH_BENCHMARK_OWNER_TOKEN", token);
            var aggregate = new AggregateLoadBenchmarks();
            var append = new EventAppendBenchmarks();
            var fixture = new SqlServerContainerFixture();
            (await Should.ThrowAsync<InvalidOperationException>(aggregate.GlobalSetup)).Message.ShouldContain("32 lowercase hexadecimal");
            (await Should.ThrowAsync<InvalidOperationException>(append.GlobalSetup)).Message.ShouldContain("32 lowercase hexadecimal");
            var initialize = typeof(SqlServerContainerFixture).GetMethod("InitializeContainerAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var initialization = (Task)initialize.Invoke(fixture, [CancellationToken.None])!;
            (await Should.ThrowAsync<InvalidOperationException>(() => initialization)).Message.ShouldContain("32 lowercase hexadecimal");
            typeof(AggregateLoadBenchmarks).GetField("_sqlContainer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(aggregate).ShouldBeNull();
            typeof(EventAppendBenchmarks).GetField("_sqlContainer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(append).ShouldBeNull();
            typeof(SqlServerContainerFixture).GetField("_container", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture).ShouldBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPATCH_BENCHMARK_OWNER_TOKEN", previous);
        }
    }
}
