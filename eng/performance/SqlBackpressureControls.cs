// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

// Untimed real-database controls. No supplied connection strings or existing database targets.
internal static class Program
{
    private const string OutboxResource = "BenchmarkSchema.001_CreateOutboxSchema.sql";
    private const string EventResource = "BenchmarkSchema.001_CreateEventStoreSchema.sql";
    private const string OutboxSha = "105A02547E19A9805546E04E7920AFDA3BD2B3436B78329CC4F92EC178E02224";
    private static readonly string[] Methods = ["SequentialStagingBackpressure", "ConcurrentStagingBackpressure", "BurstStaging"];
    private static readonly string[] Columns = ["Id", "MessageType", "Payload", "Headers", "Destination", "CreatedAt", "ScheduledAt", "SentAt", "Status", "RetryCount", "LastError", "LastAttemptAt", "CorrelationId", "CausationId", "TenantId", "Priority", "TargetTransports", "IsMultiTransport", "LeasedAt", "LeasedBy", "PartitionKey", "GroupKey", "SequenceNumber", "NextAttemptAt", "FencingToken"];
    private static readonly List<Owned> Containers = [];
    private static int _negatives;
    private static string _ownerToken = "";
    private sealed class InvariantFailure(string message) : Exception(message);
    private sealed record Pin(long length, string sha256);
    private sealed record Owned(string id, string configuredImage, string? actualImageId, bool disposalCompleted);
    private sealed record Column(string name, string sqlType, short maxLength, bool nullable, string? collation, string? defaultSql);
    private sealed record Row(string id, byte[] payload, string messageType, string destination, int status, int retryCount, string headers, string tenant, int priority, bool multiTransport, long sequenceNumber, bool lifecycleNulls, long serverPayloadLength);

    public static async Task Main(string[] args)
    {
        try
        {
            Require(args.Length == 4 && Regex.IsMatch(args[3], "^[a-f0-9]{32}$"), "Bundle, canonical outbox/event scripts and owner token required.");
            _ownerToken = args[3]; var bundle = Path.GetFullPath(args[0]); var before = Pins(bundle);
            var mainPath = Path.Combine(bundle, "Excalibur.Dispatch.Benchmarks.dll"); var resolver = new AssemblyDependencyResolver(mainPath);
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var path = resolver.ResolveAssemblyToPath(name) ?? Path.Combine(bundle, name.Name + ".dll");
                return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
            {
                var path = resolver.ResolveUnmanagedDllToPath(name); return path is null ? IntPtr.Zero : NativeLibrary.Load(path);
            };
            var main = AssemblyLoadContext.Default.LoadFromAssemblyPath(mainPath);
            var outboxBytes = File.ReadAllBytes(args[1]); var eventBytes = File.ReadAllBytes(args[2]);
            Require(ShaBytes(outboxBytes) == OutboxSha && Resource(main, OutboxResource).SequenceEqual(outboxBytes) &&
                Resource(main, EventResource).SequenceEqual(eventBytes), "Actual embedded scripts differ from canonical shipped bytes.");
            var scenarios = await Run(main);
            var after = Pins(bundle); Require(before.Count == after.Count && before.All(p => after.TryGetValue(p.Key, out var value) && value == p.Value), "Runtime bundle changed.");
            Require(Sha(args[1]) == OutboxSha && Sha(args[2]) == ShaBytes(eventBytes), "Canonical SQL files changed.");
            Require(scenarios.Count == 4 && _negatives == 40 && Containers.Count == 4 && Containers.All(c => c.disposalCompleted), "Incomplete real-SQL controls/cleanup population.");
            Console.WriteLine(JsonSerializer.Serialize(new { schema = "sql-backpressure-schema-controls-r1", passed = true,
                configurations = 4, originalMethodsPerConfiguration = 3, cyclesPerMethod = 2, verifiedMethodCalls = 24,
                rejectingNegativeControls = _negatives, scenarios, ownedContainers = Containers, ownerToken = _ownerToken,
                outboxEmbeddedResourceSha256 = OutboxSha, eventEmbeddedResourceSha256 = ShaBytes(eventBytes), actualMainSha256 = Sha(mainPath),
                runtimeFilesBefore = before, runtimeFilesAfter = after, runtimeBundleUnchanged = true,
                nativeAotVerified = false, timingOrThroughputMeasured = false, timedIterationsValidated = false,
                databaseUpgradeCompatibilityVerified = false, fullSuiteComplete = false,
                scope = "Four fresh fixture-owned SQL Server containers. Full original setup and three awaited staging bodies twice each after original SQL reset; real rows/IDs/envelope/headers/payload lengths/defaults and exact schema checked. Existing event-schema entry also executed on same owned containers. No existing user database, production schema edit, measured throughput, fault recovery, AOT, or all timed iteration claim. Disposal completed for held fixture containers; forced-termination cleanup remains best effort." }));
        }
        catch (Exception exception)
        {
            // Never print private fixture connection strings, credentials, headers or exception messages.
            Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, errorType = exception.GetType().FullName,
                innerErrorType = exception.InnerException?.GetType().FullName, ownerToken = _ownerToken, privateExceptionMessagesSuppressed = true }));
            Environment.ExitCode = 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<List<object>> Run(Assembly main)
    {
        var type = main.GetType("Excalibur.Dispatch.Benchmarks.LoadTests.OutboxBackpressureLoadTest", true)!;
        var schemaType = main.GetType("Excalibur.Dispatch.Benchmarks.Patterns.SqlServerBenchmarkSchema", true)!;
        var summaries = new List<object>();
        foreach (var count in new[] { 100, 1000 })
        foreach (var size in new[] { 1, 10 })
        {
            var fixture = Activator.CreateInstance(type)!; type.GetProperty("MessageCount")!.SetValue(fixture, count);
            type.GetProperty("PayloadSizeKb")!.SetValue(fixture, size); object? container = null; string? id = null;
            try
            {
                var setup = (Task)type.GetMethod("GlobalSetup")!.Invoke(fixture, null)!;
                while (!setup.IsCompleted)
                {
                    container = Field(type, "_sqlContainer").GetValue(fixture);
                    if (container is not null) id = Remember(container);
                    await Task.WhenAny(setup, Task.Delay(250));
                }
                await setup; container = Field(type, "_sqlContainer").GetValue(fixture)!; id = Remember(container);
                var ownedId = id ?? throw new InvariantFailure("Original fixture container identity unavailable.");
                var imageId = await DockerImage(ownedId); SetOwned(ownedId, imageId, false);
                // Connection string stays in memory, obtained only from this fresh original fixture.
                var connectionString = (string)Field(type, "_connectionString").GetValue(fixture)!;
                await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
                var columns = await Schema(connection); ValidateSchema(columns);
                Reject(() => ValidateSchema(columns.Where(c => c.name != "PartitionKey").ToArray()), "missing schema column");
                var version = (string)(await Scalar(connection, "SELECT CONVERT(nvarchar(max),@@VERSION);"))!;
                Require((int)(await Scalar(connection, "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('OutboxMessages','OutboxMessageTransports','OutboxFence','DeadLetterQueue');"))! == 4, "Canonical outbox table population differs.");
                var eventTask = (Task)schemaType.GetMethod("CreateAsync", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [container])!;
                await eventTask;
                Require((int)(await Scalar(connection, "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('EventStoreEvents','EventStoreEventsPosition');"))! == 2,
                    "Existing event-schema entry failed on the same owned SQL container.");
                var runs = new List<object>(); var oldIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var method in Methods)
                for (var cycle = 0; cycle < 2; cycle++)
                {
                    type.GetMethod("IterationSetup")!.Invoke(fixture, null);
                    Require((long)(await Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.OutboxMessages;"))! == 0 &&
                        (long)(await Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.OutboxMessageTransports;"))! == 0, "Original reset left previous rows.");
                    await (Task)type.GetMethod(method)!.Invoke(fixture, null)!;
                    var rows = await ReadRows(connection); ValidateRows(rows, count, size * 1024, oldIds);
                    Require((long)(await Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.OutboxMessageTransports;"))! == 0, "Single-destination staging wrote transport children.");
                    if (cycle == 0)
                    {
                        Reject(() => ValidateRows(rows, count + 1, size * 1024, oldIds), "wrong row count");
                        var badPayload = (Row[])rows.Clone(); badPayload[0] = badPayload[0] with { payload = new byte[size * 1024 + 1] };
                        Reject(() => ValidateRows(badPayload, count, size * 1024, oldIds), "wrong payload length");
                        var badStatus = (Row[])rows.Clone(); badStatus[0] = badStatus[0] with { status = 1 };
                        Reject(() => ValidateRows(badStatus, count, size * 1024, oldIds), "wrong initial status");
                    }
                    foreach (var row in rows) Require(oldIds.Add(row.id), "Message ID repeated after original SQL reset.");
                    type.GetMethod("IterationSetup")!.Invoke(fixture, null);
                    Require((long)(await Scalar(connection, "SELECT COUNT_BIG(*) FROM dbo.OutboxMessages;"))! == 0, "Post-call original reset did not clear staged rows.");
                    runs.Add(new { method, cycle, rows = rows.Length, distinctIds = rows.Select(r => r.id).Distinct().Count(), payloadBytes = size * 1024 });
                }
                summaries.Add(new { messageCount = count, payloadSizeKb = size, sqlVersion = version, containerId = id,
                    configuredImage = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04", actualImageId = imageId, columns, runs,
                    eventSchemaEntryExecuted = true, outboxResourceVerified = true });
            }
            finally
            {
                container ??= Field(type, "_sqlContainer").GetValue(fixture);
                if (container is not null) id ??= Remember(container);
                await (Task)type.GetMethod("GlobalCleanup")!.Invoke(fixture, null)!;
                if (id is not null) SetOwned(id, null, true);
            }
        }
        return summaries;
    }

    private static async Task<Row[]> ReadRows(SqlConnection connection)
    {
        const string sql = "SELECT Id,Payload,MessageType,Destination,Status,RetryCount,Headers,TenantId,Priority,IsMultiTransport,SequenceNumber,CASE WHEN ScheduledAt IS NULL AND SentAt IS NULL AND LastError IS NULL AND LastAttemptAt IS NULL AND LeasedAt IS NULL AND LeasedBy IS NULL AND PartitionKey IS NULL AND GroupKey IS NULL AND NextAttemptAt IS NULL AND FencingToken IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,DATALENGTH(Payload) FROM dbo.OutboxMessages;";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 }; await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Row>(); while (await reader.ReadAsync()) rows.Add(new Row(reader.GetString(0), (byte[])reader.GetValue(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetBoolean(9), reader.GetInt64(10), reader.GetBoolean(11), reader.GetInt64(12)));
        return rows.ToArray();
    }
    private static void ValidateRows(Row[] rows, int count, int bytes, HashSet<string> prior)
    {
        Require(rows.Length == count && rows.Select(r => r.id).Distinct(StringComparer.Ordinal).Count() == count, "Real row count/ID population differs.");
        foreach (var row in rows)
        {
            Require(Guid.TryParse(row.id, out _) && !prior.Contains(row.id) && row.payload.Length == bytes && row.serverPayloadLength == bytes && row.messageType == "BackpressureTestEvent" && row.destination == "test-destination" && row.status == 0 && row.retryCount == 0 && row.tenant == "__untenanted__" && row.priority == 0 && !row.multiTransport && row.sequenceNumber == 0 && row.lifecycleNulls, "Staged payload/envelope/defaults differ.");
            using var headers = JsonDocument.Parse(row.headers); var properties = headers.RootElement.EnumerateObject().ToArray();
            Require(properties.Length == 2 && Guid.TryParse(headers.RootElement.GetProperty("CorrelationId").GetString(), out _) &&
                int.TryParse(headers.RootElement.GetProperty("ProducerId").GetString(), out var producer) && producer > 0, "Real persisted original headers differ.");
        }
    }
    private static async Task<Column[]> Schema(SqlConnection connection)
    {
        await using var command = new SqlCommand("SELECT c.name,TYPE_NAME(c.user_type_id),c.max_length,c.is_nullable,c.collation_name,d.definition FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id WHERE c.object_id=OBJECT_ID('dbo.OutboxMessages') ORDER BY c.column_id;", connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(); var result = new List<Column>();
        while (await reader.ReadAsync()) result.Add(new Column(reader.GetString(0), reader.GetString(1), reader.GetInt16(2), reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        return result.ToArray();
    }
    private static void ValidateSchema(Column[] columns)
    {
        Require(columns.Length == Columns.Length && columns.Select(c => c.name).ToHashSet(StringComparer.Ordinal).SetEquals(Columns), "Canonical schema column population differs.");
        var map = columns.ToDictionary(c => c.name, StringComparer.Ordinal);
        Require(map["Id"].sqlType == "nvarchar" && map["Id"].maxLength == 510 && !map["Id"].nullable && map["Destination"].maxLength == 510 &&
            map["Payload"].sqlType == "varbinary" && map["Payload"].maxLength == -1 && !map["Payload"].nullable && map["TenantId"].maxLength == 128 &&
            !map["TenantId"].nullable && map["TenantId"].collation == "Latin1_General_BIN2", "Canonical schema type/width/nullability/collation differs.");
        foreach (var name in new[] { "CreatedAt", "Status", "RetryCount", "TenantId", "Priority", "IsMultiTransport", "SequenceNumber" }) Require(map[name].defaultSql is not null, "Canonical SQL default missing.");
    }
    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    { await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 }; return await command.ExecuteScalarAsync(); }
    private static string? Remember(object container)
    {
        string? id;
        try { id = (string?)container.GetType().GetProperty("Id")!.GetValue(container); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return null; }
        if (string.IsNullOrEmpty(id)) return null;
        Require(Regex.IsMatch(id, "^[a-f0-9]{64}$"), "Owned container ID malformed.");
        if (!Containers.Any(c => c.id == id)) Containers.Add(new Owned(id, "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04", null, false));
        SaveOwned(); return id;
    }
    private static void SetOwned(string id, string? image, bool disposed)
    { var index = Containers.FindIndex(c => c.id == id); Require(index >= 0, "Cleanup identity was never captured from the fixture."); var prior = Containers[index]; Containers[index] = prior with { actualImageId = image ?? prior.actualImageId, disposalCompleted = disposed || prior.disposalCompleted }; SaveOwned(); }
    private static void SaveOwned() => File.WriteAllText("owned-containers.json", JsonSerializer.Serialize(new { schema = "sql-backpressure-owned-containers-r1", ownerToken = _ownerToken, containers = Containers }));
    private static async Task<string> DockerImage(string id)
    {
        var info = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "inspect", "--type", "container", "--format", "{{.Image}}", id }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvariantFailure("Read-only image inspect did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token); var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token); var value = (await stdout).Trim(); var error = await stderr;
            Require(process.ExitCode == 0 && error.Length < 65536 && value.Length < 65536 && Regex.IsMatch(value, "^sha256:[a-f0-9]{64}$"), "Actual image inspect failed or was ambiguous."); return value;
        }
        finally { if (!process.HasExited) { try { process.Kill(true); } catch { /* Only this held read-only inspect process is eligible. */ } } }
    }
    private static byte[] Resource(Assembly assembly, string name) { using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvariantFailure("Embedded shipped resource missing."); using var output = new MemoryStream(); stream.CopyTo(output); return output.ToArray(); }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Reject(Action validator, string label) { try { validator(); } catch (InvariantFailure) { _negatives++; return; } throw new InvariantFailure("Negative oracle accepted " + label); }
    private static Dictionary<string, Pin> Pins(string root)
    {
        var result = new Dictionary<string, Pin>(StringComparer.Ordinal); var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            Require((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "Reparse runtime directory refused.");
            foreach (var path in Directory.EnumerateFileSystemEntries(directory)) { var attributes = File.GetAttributes(path); Require((attributes & FileAttributes.ReparsePoint) == 0, "Reparse member refused."); if ((attributes & FileAttributes.Directory) != 0) pending.Push(path); else result.Add(Path.GetRelativePath(root, path).Replace('\\', '/'), new Pin(new FileInfo(path).Length, Sha(path))); }
        }
        Require(result.Count > 0, "Empty runtime bundle."); return result;
    }
    private static string Sha(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string ShaBytes(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static void Require(bool condition, string message) { if (!condition) throw new InvariantFailure(message); }
}
