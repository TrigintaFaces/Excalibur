// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using Excalibur.Compliance;
using Excalibur.AuditLogging.GoogleCloud;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.AuditLogging.GoogleCloud.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Resilience")]
public sealed class GoogleCloudResponseBodyTimeoutShould
{
    [Fact]
    public async Task BoundStalledBodyWithoutRetryingAcceptedPost()
    {
        using var content = new StalledContent();
        using var handler = new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var provider = CreateProvider(handler);
        var operation = provider.GetRequiredService<IAuditLogExporter>().ExportAsync(CreateEvent("stalled"), CancellationToken.None);
        await content.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(6));
            result.Success.ShouldBeFalse();
            result.IsTransientError.ShouldBeTrue();
            content.Cancelled.ShouldBeTrue();
            content.WasDisposed.ShouldBeTrue();
            handler.Calls.ShouldBe(1);
        }
        finally
        {
            content.Release.TrySetResult();
            await operation;
        }
    }

    [Fact]
    public async Task RecordTimedOutChunkAndContinueHealthyChunk()
    {
        using var content = new StalledContent();
        using var handler = new ResponseHandler(call => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = call == 1 ? content : new StringContent("accepted") });
        using var provider = CreateProvider(handler);
        var operation = provider.GetRequiredService<IAuditLogExporter>().ExportBatchAsync(
            [CreateEvent("first"), CreateEvent("second")], CancellationToken.None);
        await content.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(6));
            result.TotalCount.ShouldBe(2);
            result.FailedCount.ShouldBe(1);
            result.SuccessCount.ShouldBe(1);
            result.FailedEventIds.ShouldNotBeNull().ShouldBe(["first"]);
            content.Cancelled.ShouldBeTrue();
            content.WasDisposed.ShouldBeTrue();
            handler.Calls.ShouldBe(2);
        }
        finally
        {
            content.Release.TrySetResult();
            try { await operation; } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreserveCallerCancellationWithoutAnotherChunk(bool batch)
    {
        using var content = new StalledContent();
        using var handler = new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var provider = CreateProvider(handler);
        using var cancellation = new CancellationTokenSource();
        var exporter = provider.GetRequiredService<IAuditLogExporter>();
        Task operation = batch
            ? exporter.ExportBatchAsync([CreateEvent("first"), CreateEvent("second")], cancellation.Token)
            : exporter.ExportAsync(CreateEvent("first"), cancellation.Token);
        await content.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            content.Cancelled.ShouldBeTrue();
            handler.Calls.ShouldBe(1);
        }
        finally
        {
            content.Release.TrySetResult();
            try { await operation.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task PreserveHealthyAndPermanentResponseBody(HttpStatusCode status, bool success)
    {
        using var handler = new ResponseHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("bounded-body") });
        using var provider = CreateProvider(handler);
        var result = await provider.GetRequiredService<IAuditLogExporter>().ExportAsync(CreateEvent("healthy"), CancellationToken.None);
        result.Success.ShouldBe(success);
        if (!success) { result.ErrorMessage.ShouldNotBeNull().ShouldContain("bounded-body"); }
        handler.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundHealthBodyAndPreserveHealthyOutcome(bool stalled)
    {
        using var content = new StalledContent();
        using var handler = new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = stalled ? content : new StringContent("healthy") });
        using var provider = CreateProvider(handler, bindConfiguration: true);
        var operation = provider.GetRequiredService<IAuditLogExporter>().CheckHealthAsync(CancellationToken.None);
        try
        {
            var result = await operation.WaitAsync(TimeSpan.FromSeconds(6));
            result.IsHealthy.ShouldBe(!stalled);
            if (stalled) { content.Cancelled.ShouldBeTrue(); content.WasDisposed.ShouldBeTrue(); }
            handler.Calls.ShouldBe(1);
        }
        finally { content.Release.TrySetResult(); await operation; }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ApplyConfiguredRetryBudgetToActualPipeline(int retries)
    {
        using var handler = new ResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("transient") });
        using var provider = CreateProvider(handler, retries, bindConfiguration: true);
        var result = await provider.GetRequiredService<IAuditLogExporter>().ExportAsync(CreateEvent("budget"), CancellationToken.None);
        result.Success.ShouldBeFalse();
        result.IsTransientError.ShouldBeTrue();
        handler.Calls.ShouldBe(retries + 1);
    }

    private static ServiceProvider CreateProvider(HttpMessageHandler handler, int retries = 1, bool bindConfiguration = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (bindConfiguration)
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:ProjectId"] = "test-only", 
                ["Audit:Timeout"] = "00:00:01",
                ["Audit:MaxRetryAttempts"] = retries.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Audit:RetryBaseDelay"] = "00:00:00.010", ["Audit:MaxBatchSize"] = "1"
            }).Build());
            services.AddGoogleCloudAuditExporter(builder => builder.BindConfiguration("Audit"));
        }
        else
        {
            services.AddGoogleCloudAuditExporter(builder => builder.ProjectId("test-only"));
            services.Configure<GoogleCloudAuditOptions>(options =>
            {
                options.Timeout = TimeSpan.FromSeconds(1);
                options.MaxRetryAttempts = retries;
                options.RetryBaseDelay = TimeSpan.FromMilliseconds(10);
                options.MaxBatchSize = 1;
            });
        }
        services.AddHttpClient<GoogleCloudLoggingAuditExporter>().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static AuditEvent CreateEvent(string id) => new()
    {
        EventId = id, EventType = AuditEventType.DataAccess, Action = "Read", Outcome = AuditOutcome.Success,
        Timestamp = DateTimeOffset.UtcNow, ActorId = "test", ResourceId = "record"
    };

    private sealed class ResponseHandler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response(Calls));
        }
    }

    private sealed class StalledContent : HttpContent
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cancelled { get; private set; }
        public bool WasDisposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try { await Release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing) { WasDisposed = true; base.Dispose(disposing); }
    }
}
