// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Outbox;
using Excalibur.Outbox.Redis;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Excalibur.Integration.Tests.Redis.Outbox;

[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Outbox")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxAcquisitionSnapshotShould(RedisContainerFixture fixture)
{
    [Fact]
    public async Task PreserveAcquisitionSnapshotWhenSuccessorChangesRetryEvidenceBeforeResponseDelivery()
    {
        fixture.DockerAvailable.ShouldBeTrue("This control requires actual Redis execution, not fabricated script results.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var firstConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        await using var secondConnection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var firstDatabase = firstConnection.GetDatabase();
        var secondDatabase = secondConnection.GetDatabase();
        var prefix = "snapshot-red-" + Guid.NewGuid().ToString("N");
        var serverExecuted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scriptCalls = 0;
        var postClaimHashReads = 0;

        // A proxy at the response boundary; every operation used below reaches actual Redis.
        var databaseProxy = A.Fake<IDatabase>();
        A.CallTo(() => databaseProxy.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily(async (string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                Interlocked.Increment(ref scriptCalls);
                var actualResult = await firstDatabase.ScriptEvaluateAsync(script, keys, values, flags);
                serverExecuted.TrySetResult(values[2].ToString());
                await releaseResponse.Task.WaitAsync(deadline.Token);
                return actualResult;
            });
        // Required to let the unfixed implementation perform its genuine later hash reads.
        A.CallTo(() => databaseProxy.HashGetAllAsync(A<RedisKey>._, A<CommandFlags>._))
            .ReturnsLazily((RedisKey key, CommandFlags flags) =>
            {
                Interlocked.Increment(ref postClaimHashReads);
                return firstDatabase.HashGetAllAsync(key, flags);
            });
        var connectionProxy = A.Fake<IConnectionMultiplexer>();
        A.CallTo(() => connectionProxy.GetDatabase(A<int>._, A<object>._)).Returns(databaseProxy);

        RedisOutboxOptions Configure(string owner) => new()
        {
            ConnectionString = fixture.ConnectionString, KeyPrefix = prefix, ProcessorId = owner,
            LeaseTimeoutSeconds = 120, FailureBackoffFloorSeconds = 1,
        };
        await using var first = new RedisOutboxStore(connectionProxy, Options.Create(Configure("A")),
            NullLogger<RedisOutboxStore>.Instance);
        await using var second = new RedisOutboxStore(secondConnection, Options.Create(Configure("B")),
            NullLogger<RedisOutboxStore>.Instance);
        byte[] payload = [0, 255, 128, 1, 34, 92];
        var row = new OutboundMessage("snapshot-event", payload, "destination") { TenantId = "snapshot-tenant" };
        await second.StageMessageAsync(row, deadline.Token);
        var hashKey = prefix + ":msg:" + row.Id;
        var leasedKey = prefix + ":idx:leased";
        Task<IEnumerable<OutboundMessage>>? firstTask = null;
        try
        {
            firstTask = first.GetUnsentMessagesAsync(1, deadline.Token).AsTask();
            var firstIdentity = await serverExecuted.Task.WaitAsync(deadline.Token);
            firstIdentity.ShouldNotBeNullOrWhiteSpace();
            ((string?)await secondDatabase.HashGetAsync(hashKey, "LeasedBy")).ShouldBe(firstIdentity);
            ((int)await secondDatabase.HashGetAsync(hashKey, "RetryCount")).ShouldBe(0);
            (await secondDatabase.HashGetAsync(hashKey, "LastError")).IsNull.ShouldBeTrue();
            (await secondDatabase.SortedSetScoreAsync(leasedKey, row.Id)).ShouldNotBeNull();

            // Deliberately expire this exact known lease: deterministic reclaim, not elapsed-time testing.
            await secondDatabase.SortedSetAddAsync(leasedKey, row.Id, 0);
            var successor = (await second.GetUnsentMessagesAsync(1, deadline.Token)).Single();
            successor.DispatcherId.ShouldNotBeNullOrWhiteSpace();
            successor.DispatcherId.ShouldNotBe(firstIdentity);
            var strong = ((IServiceProvider)second).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>();
            (await strong.MarkFailedAsync(row.Id, "successor-only-error", 2,
                successor.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            var observedCount = (int)await secondDatabase.HashGetAsync(hashKey, "RetryCount");
            var observedError = (string?)await secondDatabase.HashGetAsync(hashKey, "LastError");
            observedCount.ShouldBe(2);
            observedError.ShouldBe("successor-only-error");
            R08OwnedRedisRunReceipt.PositiveReclaim(firstIdentity, successor.DispatcherId!, observedCount, observedError);

            releaseResponse.TrySetResult();
            var captured = (await firstTask.WaitAsync(deadline.Token)).Single();
            captured.Id.ShouldBe(row.Id);
            captured.DispatcherId.ShouldBe(firstIdentity);
            captured.Payload.ShouldBe(payload);
            captured.TenantId.ShouldBe("snapshot-tenant");
            // These are the primary RED assertions: current source returns successor count/error.
            captured.RetryCount.ShouldBe(0, "Retry evidence must belong to the same acquisition as the returned authority.");
            captured.LastError.ShouldBeNull();
            scriptCalls.ShouldBe(1);
            postClaimHashReads.ShouldBe(0, "The acquisition snapshot must not be repaired by reading current hashes.");
        }
        finally
        {
            releaseResponse.TrySetResult();
            if (firstTask is not null)
            {
                // Observe the task on every path. This is bounded cleanup, not a claim release.
                await firstTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        A.CallTo(() => connectionProxy.Dispose()).MustNotHaveHappened();
    }
}

// Append in the same proposed new test source file. TEST ONLY; dormant unless Root supplies all guards.
internal static class R08OwnedRedisRunReceipt
{
    private const string Prefix = "EXCALIBUR_R08_";
    private sealed record Guard(string Directory, string Nonce, string Phase, int ControllerPid);

    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255:The ModuleInitializer attribute should not be used in libraries",
        Justification = "Gated test-only pre-fixture resource-session ownership receipt for bounded Root verification cleanup.")]
    internal static void BeforeFixtures()
    {
        var guard = ReadGuard();
        if (guard is null) { return; }
        WriteSession(guard); // No ResourceReaper access occurs before the complete guard check.
    }

    private static Guard? ReadGuard()
    {
        var directory = Environment.GetEnvironmentVariable(Prefix + "RECEIPT_DIRECTORY");
        var nonce = Environment.GetEnvironmentVariable(Prefix + "RUN_NONCE");
        var phase = Environment.GetEnvironmentVariable(Prefix + "PHASE");
        var controller = Environment.GetEnvironmentVariable(Prefix + "CONTROLLER_PID");
        if (directory is null && nonce is null && phase is null && controller is null) { return null; }
        if (!OperatingSystem.IsWindows() || directory is null || nonce is null
            || !Guid.TryParseExact(nonce, "N", out _) || phase is not ("discovery" or "execution")
            || !int.TryParse(controller, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var controllerPid) || controllerPid <= 0
            || !Path.IsPathFullyQualified(directory))
        {
            throw new InvalidOperationException("Incomplete or invalid Root R08 ownership guard.");
        }
        var full = Path.GetFullPath(directory);
        var allowed = Path.GetFullPath("C:/Users/thoma/Documents/Codex/2026-11-01/give-me-a-prompt-to-continue/artifacts")
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full))
        {
            throw new InvalidOperationException("Root R08 receipt directory is outside the existing C evidence tree.");
        }
        for (var node = new DirectoryInfo(full); node is not null; node = node.Parent)
        {
            if ((node.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Root R08 receipt path contains a reparse point.");
            }
        }
        return new Guard(full, nonce, phase!, controllerPid);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void WriteSession(Guard guard)
    {
        // Pinned Testcontainers 4.11.0: this creates its process-local session ID, not a Docker resource.
        var session = DotNet.Testcontainers.Containers.ResourceReaper.DefaultSessionId;
        if (session == Guid.Empty) { throw new InvalidOperationException("Empty resource session is not attributable."); }
        Write(guard, "session", writer => writer.WriteString("sessionId", session.ToString("D")));
    }

    internal static void PositiveReclaim(string predecessor, string successor, int count, string? error)
    {
        var guard = ReadGuard();
        if (guard is null) { return; }
        if (guard.Phase != "execution" || predecessor == successor || count != 2 || error != "successor-only-error")
        {
            throw new InvalidOperationException("Root R08 positive reclaim evidence is inconsistent.");
        }
        Write(guard, "positive-reclaim", writer =>
        {
            writer.WriteString("sessionId", DotNet.Testcontainers.Containers.ResourceReaper.DefaultSessionId.ToString("D"));
            writer.WriteString("predecessorClaim", predecessor);
            writer.WriteString("successorClaim", successor);
            writer.WriteNumber("persistedRetryCount", count);
            writer.WriteString("persistedError", error);
        });
    }

    private static void Write(Guard guard, string kind, Action<System.Text.Json.Utf8JsonWriter> fields)
    {
        var file = Path.Combine(guard.Directory,
            $"{guard.Nonce}-{guard.Phase}-{Environment.ProcessId}-{kind}.json");
        using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
            bufferSize: 4096, FileOptions.WriteThrough);
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("kind", kind);
            writer.WriteString("nonce", guard.Nonce);
            writer.WriteString("phase", guard.Phase);
            writer.WriteNumber("processId", Environment.ProcessId);
            writer.WriteNumber("controllerPid", guard.ControllerPid);
            fields(writer);
            writer.WriteEndObject();
            writer.Flush();
        }
        stream.Flush(flushToDisk: true);
    }
}
