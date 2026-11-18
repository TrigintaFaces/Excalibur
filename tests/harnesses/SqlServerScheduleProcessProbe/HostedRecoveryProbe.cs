// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Buffers;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.Harnesses.SqlSchedule;

internal static class HostedRecoveryProbe
{
    internal static async Task<int> RunAsync()
    {
        var stage = "hosted-settings";
        try
        {
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var token = lifetime.Token;
            var mode = Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_MODE");
            if (mode is not ("hosted-produce" or "hosted-resume" or "hosted-resume-failure")) { throw new InvalidOperationException("Unknown hosted mode."); }
            var run = Guid.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_RUN")!);
            var connection = Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_CONNECTION")
                ?? throw new InvalidOperationException("Missing connection.");
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Logging.ClearProviders();
            var probeState = new HostedProbeState(run, mode);
            builder.Services.AddSingleton(probeState);
            builder.Logging.AddProvider(new HostedConfigurationDiagnosticProvider(probeState));
            builder.Services.AddScoped<HostedProbeScope>();
            builder.Services.AddSingleton<IHandlerActivator, AotHandlerActivator>();
            builder.Services.AddAotEventSerializer(HostedProbeJsonContext.Default);
            // Event persistence and scheduler payloads use distinct serializer families.
            // Register before AddDispatch's TryAdd default; the container owns this pooled serializer.
            builder.Services.AddSingleton(static services => new DispatchJsonSerializer(
                jsonContext: HostedProbeJsonContext.Default,
                bufferManager: services.GetService<IPooledBufferService>()));
            builder.Services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());
            builder.Services.AddTimeAwareScheduling(options => options.PollInterval = TimeSpan.FromMilliseconds(100));
            builder.Services.Configure<SchedulerOptions>(options =>
            {
                options.PollInterval = TimeSpan.FromMilliseconds(100);
                options.EnableAdaptivePolling = false;
            });
            builder.Services.AddSqlServerScheduleStore(options =>
            {
                options.ConnectionString = connection;
                options.LeaseDuration = TimeSpan.FromSeconds(10);
                options.CommandTimeoutSeconds = 10;
                options.LockTimeoutMilliseconds = 1000;
            });
            MessageTypeRegistry.RegisterType<HostedRecoveryDocument>();
            stage = "hosted-build";
            using var host = builder.Build();
            var state = host.Services.GetRequiredService<HostedProbeState>();
            var store = host.Services.GetRequiredService<IScheduleStore>();
            if (mode == "hosted-produce")
            {
                stage = "hosted-produce";
                await host.Services.GetRequiredService<IDispatchScheduler>().ScheduleOnceAsync(
                    DateTimeOffset.UtcNow.AddSeconds(1), new HostedRecoveryDocument(run, HostedProbeState.Payload, state.Tenant, run), token);
                var row = (await store.GetAllAsync(token)).Single();
                if (!row.Enabled || row.LastExecutionUtc is not null) { throw new InvalidOperationException("Producer inventory invalid."); }
                state.ScheduleId = row.Id;
                state.Write("produced");
            }
            else
            {
                state.ScheduleId = Guid.Parse(Environment.GetEnvironmentVariable("EXCALIBUR_SCHEDULE_TEST_ID")!);
                stage = "hosted-read-existing";
                var row = (await store.GetAllAsync(token)).Single();
                if (row.Id != state.ScheduleId || !row.Enabled || row.LastExecutionUtc is not null
                    || row.TenantId != state.Tenant || row.CorrelationId != run.ToString())
                {
                    throw new InvalidOperationException("Existing public inventory invalid.");
                }
                state.Write("existing");
            }
            try
            {
                stage = "hosted-start";
                await host.StartAsync(token);
                state.Write("worker-ready");
                state.Ready.TrySetResult();
                stage = "hosted-handler";
                await state.Entered.Task.WaitAsync(token);
                if (mode == "hosted-produce")
                {
                    // A never completes voluntarily. Deadline/worker timeout is failure, not the intended crash-stop.
                    await state.UnexpectedHandlerExit.Task.WaitAsync(token);
                    throw new InvalidOperationException("A handler ended before the intended owned-process termination.");
                }
                stage = "hosted-allow-complete";
                var command = await Console.In.ReadLineAsync(token);
                if (command != "allow-complete") { throw new InvalidOperationException("Missing completion command."); }
                state.AllowComplete.TrySetResult();
                stage = "hosted-observe-settlement";
                DateTimeOffset? settledExecution = null;
                while (true)
                {
                    var row = (await store.GetAllAsync(token)).Single();
                    if (row.Id != state.ScheduleId) { throw new InvalidOperationException("Schedule identity changed."); }
                    if (mode == "hosted-resume-failure")
                    {
                        var failureStore = (IScheduleExecutionFailureStore)store;
                        var snapshot = (await failureStore.GetExecutionSnapshotsAsync(token)).Single();
                        var failure = await failureStore.GetOccurrenceFailureAsync(row.Id, snapshot.Generation,
                            row.NextExecutionUtc!.Value, state.Tenant, token);
                        if (!row.Enabled && failure is { Disposition: ScheduleFailureDisposition.Quarantined })
                        {
                            if (row.LastExecutionUtc is not null || failure.AdmissionAttempts != 6 || failure.MaxAdmissionAttempts != 5)
                            { throw new InvalidOperationException("Failed current occurrence was settled or its captured budget changed."); }
                            state.Write("quarantined");
                            break;
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(50), token); // delay-ok: Poll the actual durable row/quarantine/settlement state under the owning cancellation bound; elapsed cadence is not completion evidence.
                        continue;
                    }
                    if (!row.Enabled && row.LastExecutionUtc is not null)
                    {
                        settledExecution = row.LastExecutionUtc;
                        break;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(50), token); // delay-ok: Poll the actual durable row/quarantine/settlement state under the owning cancellation bound; elapsed cadence is not completion evidence.
                }
                if (mode != "hosted-resume-failure") { state.Write("settled", lastExecution: settledExecution); }
            }
            finally
            {
                stage = "hosted-stop";
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // deadline-ok: independent stop budget starts in finally, after workload waits.
                await host.StopAsync(stop.Token);
            }
            if (host.Services.GetServices<IHostedService>().OfType<BackgroundService>()
                .Any(service => service.ExecuteTask is { IsCompleted: false } or { IsFaulted: true }))
            {
                throw new InvalidOperationException("Hosted workers not drained.");
            }
            state.AssertDrained();
            state.Write("stopped");
            return 0;
        }
        catch (Exception error)
        {
            var dependency = error is FileNotFoundException missing && missing.FileName is { } file
                ? " dependency=" + Path.GetFileName(file) : string.Empty;
            await Console.Error.WriteLineAsync($"stage={stage} exception={error.GetType().FullName}{dependency}");
            await WriteSafeFailureFramesAsync(error);
            return 1;
        }
    }

    private static async Task WriteSafeFailureFramesAsync(Exception error)
    {
        // StackTrace constructed without file information contains method frames, not exception messages,
        // connection arguments, exception.Data or arbitrary exception-object serialization.
        Exception? current = error;
        for (var depth = 0; current != null && depth < 4; depth++, current = current.InnerException)
        {
            await Console.Error.WriteLineAsync($"diagnostic-depth={depth} exception={current.GetType().FullName}");
            var frames = new System.Diagnostics.StackTrace(current, fNeedFileInfo: false).ToString();
            foreach (var frame in frames.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Take(32))
            {
                await Console.Error.WriteLineAsync(frame.Length > 1024 ? frame[..1024] : frame);
            }
        }
    }
}

public sealed record HostedRecoveryDocument(Guid RunId, string Payload, string TenantId, Guid? CorrelationId)
    : IDispatchDocument, ITenantAware, ICorrelationAware
{
    public Guid? CorrelationId { get; set; } = CorrelationId;
}

public sealed class HostedProbeScope : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public sealed class HostedProbeState(Guid runId, string mode)
{
    private readonly object _output = new();
    private int _invocations;
    private Exception? _fault;
    private int _faults;
    private int _futureConfigurationBlocks;
    public const string Payload = "hosted-recovery\n用户";
    public Guid RunId { get; } = runId;
    public string Mode { get; } = mode;
    public string Tenant { get; } = "hosted-" + runId.ToString("N");
    public Guid ScheduleId { get; set; }
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource AllowComplete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int RecordInvocation() => Interlocked.Increment(ref _invocations);
    public TaskCompletionSource UnexpectedHandlerExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void RecordFault(Exception error)
    {
        Interlocked.Increment(ref _faults);
        Interlocked.CompareExchange(ref _fault, error, null);
        UnexpectedHandlerExit.TrySetResult();
        Entered.TrySetException(new InvalidOperationException("Hosted handler fault."));
    }
    public void AssertDrained()
    {
        if (Mode == "hosted-resume-failure")
        {
            if (Volatile.Read(ref _invocations) != 4 || Volatile.Read(ref _faults) != 4
                || Volatile.Read(ref _fault) is not InvalidOperationException { Message: "controlled hosted current-occurrence failure" }
                || Volatile.Read(ref _futureConfigurationBlocks) != 0)
            { throw new InvalidOperationException("Expected four actual failed handler calls and no future-configuration pause."); }
            return;
        }
        if (Volatile.Read(ref _invocations) < 1 || Volatile.Read(ref _fault) is not null)
        {
            throw new InvalidOperationException("Hosted handler fault or missing invocation.");
        }
    }
    public void RecordFutureConfigurationBlock()
    {
        Interlocked.Increment(ref _futureConfigurationBlocks);
        Write("future-configuration-blocked");
    }
    public void Write(string stage, Guid? scope = null, string? tenant = null, string? ambient = null, string? correlation = null, DateTimeOffset? lastExecution = null)
    {
        lock (_output)
        {
            var reply = new HostedProbeReply(stage, Mode, RunId, Environment.ProcessId, ScheduleId, Payload,
                tenant ?? Tenant, ambient, correlation ?? RunId.ToString(), scope, Volatile.Read(ref _invocations), Volatile.Read(ref _faults), Volatile.Read(ref _fault) is not null, TimeProvider.System.GetUtcNow(), lastExecution, Volatile.Read(ref _futureConfigurationBlocks));
            Console.WriteLine(JsonSerializer.Serialize(reply, HostedProbeJsonContext.Default.HostedProbeReply));
            Console.Out.Flush();
        }
    }
}

public sealed class HostedRecoveryDocumentHandler(HostedProbeState state, HostedProbeScope scope)
    : IDocumentHandler<HostedRecoveryDocument>, IMessageContextAware
{
    private IMessageContext? _context;
    public void SetContext(IMessageContext context) => _context = context;
    public async Task HandleAsync(HostedRecoveryDocument document, CancellationToken cancellationToken)
    {
        var invocation = state.RecordInvocation();
        try
        {
            var context = _context ?? throw new InvalidOperationException("Missing handler context.");
            if (document.RunId != state.RunId || document.Payload != HostedProbeState.Payload
                || document.TenantId != state.Tenant || document.CorrelationId != state.RunId
                || context.GetTenantId() != state.Tenant || TenantContextHolder.Current != state.Tenant
                || context.CorrelationId != state.RunId.ToString() || scope.Id == Guid.Empty || scope.IsDisposed)
            {
                throw new InvalidOperationException("Hosted payload/context/scope mismatch.");
            }
            await state.Ready.Task.WaitAsync(cancellationToken);
            if (invocation == 1)
            {
                state.Write("handler-entered", scope.Id, context.GetTenantId(), TenantContextHolder.Current, context.CorrelationId);
                state.Entered.TrySetResult();
            }
            // Duplicate delivery remains permitted; each invocation validates and faults independently.
            await state.AllowComplete.Task.WaitAsync(cancellationToken);
            if (state.Mode == "hosted-resume-failure")
            { throw new InvalidOperationException("controlled hosted current-occurrence failure"); }
        }
        catch (Exception error)
        {
            state.RecordFault(error);
            throw;
        }
    }
}

internal sealed record HostedProbeReply(string Stage, string Mode, Guid RunId, int ProcessId, Guid ScheduleId,
    string Payload, string Tenant, string? AmbientTenant, string Correlation, Guid? ScopedInstanceId, int Invocations, int HandlerFaults, bool HandlerFault, DateTimeOffset ObservedUtc, DateTimeOffset? LastExecutionUtc, int FutureConfigurationBlocks);

internal sealed class HostedConfigurationDiagnosticProvider(HostedProbeState state) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(state, categoryName);
    public void Dispose() { }

    private sealed class DiagnosticLogger(HostedProbeState probeState, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (categoryName == "Excalibur.Dispatch.Delivery.ScheduledMessageService"
                && exception?.Message.StartsWith("FutureConfigurationBlocked:", StringComparison.Ordinal) == true)
            { probeState.RecordFutureConfigurationBlock(); }
        }
    }
}

[JsonSerializable(typeof(HostedRecoveryDocument))]
[JsonSerializable(typeof(HostedProbeReply))]
internal sealed partial class HostedProbeJsonContext : JsonSerializerContext;
