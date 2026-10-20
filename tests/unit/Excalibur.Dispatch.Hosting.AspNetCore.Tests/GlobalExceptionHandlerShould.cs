// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Dispatch.Exceptions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Handler = Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.GlobalExceptionHandler;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Pattern", "Regression")]
public sealed class GlobalExceptionHandlerShould
{
    [Fact]
    public async Task Preserve_Standard_Async_Diagnostics_In_Lean_Development_Handler()
    {
        var exception = await CaptureAsyncException();
        var original = exception.ToString();
        var environment = A.Fake<IHostEnvironment>();
        A.CallTo(() => environment.EnvironmentName).Returns(Environments.Development);
        using var provider = new ServiceCollection().BuildServiceProvider();
        await using var stream = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Response.Body = stream;
        var handler = new Handler(environment,
            Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics.ProblemDetailsOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Handler>.Instance);
        await handler.TryHandleAsync(context, exception, CancellationToken.None);
        stream.Position = 0;
        using var json = await JsonDocument.ParseAsync(stream);
        original.ShouldContain(nameof(ThrowAsyncException));
        json.RootElement.GetProperty("stack").GetString().ShouldBe(original);
        json.RootElement.TryGetProperty("errorCode", out _).ShouldBeFalse();
        exception.ToString().ShouldBe(original);
    }

    private static async Task<Exception> CaptureAsyncException()
    {
        try { await ThrowAsyncException(); }
        catch (InvalidOperationException exception) { return exception; }
        throw new InvalidOperationException("Expected exception");
    }

    private static async Task ThrowAsyncException()
    {
        await Task.Yield();
        throw new InvalidOperationException("ASYNC-DIAGNOSTIC");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Preserve_Mapped_Http_And_Body_Status_Without_Production_Details(int status)
    {
        var exception = new ApiException(status, "PRIVATE-SENTINEL", null);
        exception.Data["ErrorCode"] = "PRIVATE-ERROR-CODE";
        var (actual, body) = await InvokeThroughHttpAsync(exception, Environments.Production);
        using var json = JsonDocument.Parse(body);
        actual.ShouldBe(status);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(status);
        json.RootElement.GetProperty("type").GetString().ShouldStartWith("https://custom.example/");
        body.ShouldNotContain("PRIVATE");
        json.RootElement.TryGetProperty("stack", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("errorCode", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Preserve_Public_Validation_Errors_In_Production()
    {
        var exception = new ValidationException("PRIVATE-SENTINEL");
        exception.AddError("Name", "Name is required");
        var (status, body) = await InvokeThroughHttpAsync(exception, Environments.Production);
        using var json = JsonDocument.Parse(body);
        status.ShouldBe(400);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        json.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString().ShouldBe("Name is required");
        body.ShouldNotContain("PRIVATE-SENTINEL");
        json.RootElement.TryGetProperty("stack", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Preserve_Problem_Type_And_Title_Across_Environments(int status)
    {
        var (_, development) = await InvokeThroughHttpAsync(new ApiException(status, "PRIVATE-DEVELOPMENT", null), Environments.Development);
        var (_, production) = await InvokeThroughHttpAsync(new ApiException(status, "PRIVATE-PRODUCTION", null), Environments.Production);
        using var developmentJson = JsonDocument.Parse(development);
        using var productionJson = JsonDocument.Parse(production);
        productionJson.RootElement.GetProperty("type").GetString().ShouldBe(developmentJson.RootElement.GetProperty("type").GetString());
        productionJson.RootElement.GetProperty("title").GetString().ShouldBe(developmentJson.RootElement.GetProperty("title").GetString());
        production.ShouldNotContain("PRIVATE");
    }

    [Fact]
    public async Task Handle_Ordinary_Exception_Through_Http_Without_A_Dispatch_Pipeline()
    {
        var (status, body) = await InvokeThroughHttpAsync(new InvalidOperationException("PRIVATE-ORDINARY-EXCEPTION"), Environments.Production);
        using var json = JsonDocument.Parse(body);
        status.ShouldBe(500);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Internal Server Error");
        json.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        body.ShouldNotContain("PRIVATE-ORDINARY-EXCEPTION");
        json.RootElement.TryGetProperty("stack", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("errorCode", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Include_Diagnostic_Details_In_Development()
    {
        var (status, body) = await InvokeThroughHttpAsync(new ApiException(400, "DEVELOPMENT-SENTINEL", null), Environments.Development);
        using var json = JsonDocument.Parse(body);
        status.ShouldBe(400);
        json.RootElement.GetProperty("detail").GetString().ShouldBe("DEVELOPMENT-SENTINEL");
        json.RootElement.GetProperty("stack").GetString().ShouldContain("DEVELOPMENT-SENTINEL");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PUBLIC-CODE")]
    public async Task Emit_Only_Available_String_Error_Codes_In_Development(string? errorCode)
    {
        var exception = new InvalidOperationException("DEVELOPMENT-CONTROL");
        if (errorCode is not null) { exception.Data["ErrorCode"] = errorCode; }
        var (status, body) = await InvokeThroughHttpAsync(exception, Environments.Development);
        using var json = JsonDocument.Parse(body);
        status.ShouldBe(500);
        var available = json.RootElement.TryGetProperty("errorCode", out var value);
        available.ShouldBe(errorCode is not null);
        if (available) { value.GetString().ShouldBe(errorCode); }
        json.RootElement.GetProperty("stack").GetString().ShouldContain("DEVELOPMENT-CONTROL");
    }

    [Fact]
    public void Register_Only_One_Handler_On_Repeated_Registration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(A.Fake<IHostEnvironment>());
        services.AddDispatchExceptionHandler();
        services.AddDispatchExceptionHandler();
        using var provider = services.BuildServiceProvider();
        provider.GetServices<IExceptionHandler>().OfType<Handler>().Count().ShouldBe(1);
    }

    private static async Task<(int Status, string Body)> InvokeThroughHttpAsync(Exception exception, string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddDispatchExceptionHandler(options => options.StatusTypeBaseUrl = "https://custom.example");
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/throw", (Func<IResult>)(() => throw exception));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds the in-process HTTP request, so a hang fails this test rather than stalling the shard
        try
        {
            await app.StartAsync(deadline.Token);
            using var client = app.GetTestClient();
            using var response = await client.GetAsync(new Uri("/throw", UriKind.Relative), deadline.Token);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(deadline.Token));
        }
        finally
        {
            using var stopDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: bounds host shutdown in the finally block
            await app.StopAsync(stopDeadline.Token);
        }
    }
}
