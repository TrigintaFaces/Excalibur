// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Reflection;

using Excalibur.Benchmarks.Saga;

namespace Excalibur.Saga.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerSagaBenchmarkFixtureShould
{
    [Fact]
    public void RejectRepeatedSetupBeforeReplacingOwnedIdentity()
    {
        var fixture = new SqlServerSagaStoreBenchmarks();
        var schema = $"saga_benchmark_{Guid.NewGuid():N}";
        var seed = Guid.NewGuid();
        Set(fixture, "_ownsSchema", true);
        Set(fixture, "_ownedSchema", schema);
        Set(fixture, "_ownedSchemaId", 719);
        Set(fixture, "_databaseId", 37);
        Set(fixture, "_expectedVersion", 9L);
        Set(fixture, "_preloadedSagaId", seed);

        // The already-owned guard must run even when the environment has no SQL configuration.
        var failure = Should.Throw<InvalidOperationException>(fixture.GlobalSetup);
        failure.Message.ShouldContain("already owns a schema");
        Read<bool>(fixture, "_ownsSchema").ShouldBeTrue();
        Read<string>(fixture, "_ownedSchema").ShouldBe(schema);
        Read<int>(fixture, "_ownedSchemaId").ShouldBe(719);
        Read<int>(fixture, "_databaseId").ShouldBe(37);
        Read<long>(fixture, "_expectedVersion").ShouldBe(9L);
        Read<Guid>(fixture, "_preloadedSagaId").ShouldBe(seed);
    }

    [Fact]
    public void LeaveNonownedStateUntouchedWithoutAttemptingSchemaCleanup()
    {
        var fixture = new SqlServerSagaStoreBenchmarks();
        // An unowned caller schema must never become a cleanup target.
        Set(fixture, "_ownedSchema", "dispatch");
        Set(fixture, "_ownedSchemaId", 719);
        Set(fixture, "_databaseId", 37);
        Set(fixture, "_ownsSchema", false);

        fixture.GlobalCleanup();
        fixture.GlobalCleanup();

        Read<bool>(fixture, "_ownsSchema").ShouldBeFalse();
        Read<string>(fixture, "_ownedSchema").ShouldBe("dispatch");
        Read<int>(fixture, "_ownedSchemaId").ShouldBe(719);
        Read<int>(fixture, "_databaseId").ShouldBe(37);
    }

    [Fact]
    public void EmbedTheShippedCanonicalSagaSchemaWithoutAHandwrittenCopy()
    {
        var assembly = typeof(SqlServerSagaStoreBenchmarks).Assembly;
        using var resource = assembly.GetManifestResourceStream("BenchmarkSchema.01-SagaSchema.sql")
            ?? throw new InvalidOperationException("The actual fixture resource is missing.");
        using var memory = new MemoryStream();
        resource.CopyTo(memory);
        var canonicalPath = Path.Combine(AppContext.BaseDirectory, "TestResources", "CanonicalSagaSchema.sql");
        var shippedBytes = File.ReadAllBytes(canonicalPath);
        shippedBytes.Length.ShouldBeGreaterThan(0);
        memory.ToArray().ShouldBe(shippedBytes);
    }

    // Missing/blank environment and real SQL behavior require isolated-process/database controls.
    // Do not mutate process-global environment or the fixture's static readonly connection value here.
    private static FieldInfo Field(string name) => typeof(SqlServerSagaStoreBenchmarks)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Actual fixture field {name} is missing.");

    private static void Set(SqlServerSagaStoreBenchmarks fixture, string name, object value) => Field(name).SetValue(fixture, value);

    private static T Read<T>(SqlServerSagaStoreBenchmarks fixture, string name) => (T)Field(name).GetValue(fixture)!;
}