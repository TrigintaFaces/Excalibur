// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Dispatch.Delivery;
using Microsoft.Extensions.Options;

var stage = "settings";
try
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var connection = Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Missing test connection.");
    var id = Guid.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_ID")!);
    var leaseSeconds = int.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_LEASE_SECONDS")!, System.Globalization.CultureInfo.InvariantCulture);
    stage = "construct-store";
    var store = new SqlServerScheduleStore(Options.Create(new SqlServerScheduleStoreOptions
    {
        ConnectionString = connection, LeaseDuration = TimeSpan.FromSeconds(leaseSeconds),
        CommandTimeoutSeconds = 10, LockTimeoutMilliseconds = 1000,
    }));
    stage = "read-snapshot";
    var snapshot = (await store.GetExecutionSnapshotsAsync(deadline.Token)).Single(row => row.Message.Id == id);
    stage = "acquire";
    var lease = await store.TryAcquireExecutionAsync(id, snapshot.Generation, snapshot.Message.NextExecutionUtc!.Value, deadline.Token)
        ?? throw new InvalidOperationException("Admission lost.");
    var message = snapshot.Message;
    stage = "write-acquired";
    Write(new ProbeReply("acquired", Environment.ProcessId, lease, new ScheduledMessage
    {
        Id = message.Id, MessageName = message.MessageName, MessageBody = message.MessageBody,
        CronExpression = message.CronExpression, TimeZoneId = message.TimeZoneId, Interval = message.Interval,
        NextExecutionUtc = message.NextExecutionUtc, LastExecutionUtc = message.LastExecutionUtc, Enabled = message.Enabled,
        MissedExecutionBehavior = message.MissedExecutionBehavior, CorrelationId = message.CorrelationId,
        TraceParent = message.TraceParent, TenantId = message.TenantId, UserId = message.UserId,
    }, null));
    stage = "commands";
    while (await Console.In.ReadLineAsync(deadline.Token) is { } command)
    {
        if (command == "commit")
        {
            var success = await store.TryUpdateProgressAsync(lease, new ScheduleExecutionResult(lease.OccurrenceUtc, null, true), deadline.Token);
            Write(new ProbeReply("committed", Environment.ProcessId, lease, null, success));
        }
        else if (command == "release")
        {
            var success = await store.ReleaseExecutionAsync(lease, deadline.Token);
            Write(new ProbeReply("released", Environment.ProcessId, lease, null, success));
            return success ? 0 : 2;
        }
        else { throw new InvalidOperationException("Unknown command."); }
    }
    return 3;
}
catch (Exception error)
{
    // Never print provider exception messages: they can contain connection details.
    var dependency = error is FileNotFoundException missing && missing.FileName is { } file
        ? " dependency=" + Path.GetFileName(file) : string.Empty;
    await Console.Error.WriteLineAsync($"stage={stage} exception={error.GetType().FullName}{dependency}");
    return 1;
}

static void Write(ProbeReply reply)
{
    Console.WriteLine(JsonSerializer.Serialize(reply, ProbeJsonContext.Default.ProbeReply));
    Console.Out.Flush();
}

internal sealed record ProbeReply(string Stage, int ProcessId, ScheduleExecutionLease Lease, ScheduledMessage? Message, bool? Success);

[JsonSerializable(typeof(ProbeReply))]
internal sealed partial class ProbeJsonContext : JsonSerializerContext;
