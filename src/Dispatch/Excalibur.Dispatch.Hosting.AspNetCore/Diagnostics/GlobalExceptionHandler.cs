// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Messaging;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Diagnostics;

/// <summary>Maps framework exceptions to HTTP problem details without requiring Excalibur hosting packages.</summary>
/// <remarks>Production responses retain mapped statuses and public validation errors, while internal details remain in server logs.</remarks>
public partial class GlobalExceptionHandler : IExceptionHandler
{
    private const string UnhandledMessage = "An unhandled exception has occurred.";
    private readonly IHostEnvironment _environment;
    private readonly ProblemDetailsOptions _options;
    private readonly ILogger _logger;

    /// <summary>Initializes the exception handler.</summary>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="options">The response configuration.</param>
    /// <param name="logger">The server diagnostic logger.</param>
    public GlobalExceptionHandler(IHostEnvironment environment, IOptions<ProblemDetailsOptions> options,
        ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        var status = exception.GetStatusCode() ?? StatusCodes.Status500InternalServerError;
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var exceptionId = exception is ApiException api ? api.Id : Uuid7Extensions.GenerateGuid();
        var problem = BuildProblemDetails(exception, status, traceId, exceptionId);

        using (_logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal)
            { ["TraceId"] = traceId, ["ExceptionId"] = exceptionId }))
        {
            LogExceptionOccurred(_logger, exception, httpContext.Request.Path, status, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        if (httpContext.RequestServices.GetService<IProblemDetailsService>() is { } service &&
            await service.TryWriteAsync(new ProblemDetailsContext
                { HttpContext = httpContext, Exception = exception, ProblemDetails = problem }).ConfigureAwait(false))
        {
            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(problem, ProblemDetailsJsonContext.Default.ProblemDetails,
            contentType: "application/problem+json", cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private ProblemDetails BuildProblemDetails(Exception exception, int status, string traceId, Guid exceptionId)
    {
        var locale = _options.SupportedLocales.Contains(CultureInfo.CurrentUICulture.Name)
            ? CultureInfo.CurrentUICulture.Name : "en-US";
        var problem = new ProblemDetails
        {
            Type = $"{_options.StatusTypeBaseUrl}/{locale}/docs/Web/HTTP/Status/{status}",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Instance = $"urn:{_environment.ApplicationName}:error:{exceptionId:D}",
            Extensions = { ["traceId"] = traceId },
        };
        if (string.IsNullOrWhiteSpace(problem.Title))
        {
            problem.Title = UnhandledMessage;
        }

        var validationErrors = GetValidationErrors(exception);
        if (validationErrors is { Count: > 0 })
        {
            // A subclass can return a different dictionary implementation. Own a closed serializer
            // type and copy arrays so response serialization does not retain the caller's collection.
            problem.Extensions["errors"] = validationErrors.ToDictionary(
                pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        }

        if (_environment.IsDevelopment())
        {
            problem.Detail = exception.Message;
            if (exception.GetErrorCode() is { } errorCode)
            {
                problem.Extensions["errorCode"] = errorCode;
            }
            problem.Extensions["stack"] = FormatDiagnosticStack(exception);
        }
        else
        {
            problem.Detail = validationErrors is { Count: > 0 }
                ? "One or more validation errors occurred."
                : "The request could not be completed.";
        }

        return problem;
    }

    /// <summary>Formats the text written to the <c>stack</c> extension member of a development response.</summary>
    /// <remarks>
    /// Called only while the host environment is Development. An override therefore shapes development
    /// responses alone and cannot widen what a production response discloses.
    /// </remarks>
    /// <param name="exception">The exception being handled.</param>
    /// <returns>The diagnostic text to disclose. The base implementation returns the full exception string.</returns>
    protected virtual string FormatDiagnosticStack(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToString();
    }

    /// <summary>Extracts the per-property validation errors to publish in the <c>errors</c> extension member.</summary>
    /// <remarks>
    /// The base implementation recognizes the framework's own validation exception and returns its errors;
    /// every other exception yields no errors. Override this to surface errors raised by another validation
    /// library. Whatever is returned is written in <b>every</b> environment, production included, and is not
    /// sanitized further — so it must carry only what the caller is permitted to see. The returned dictionary
    /// is copied before serialization and is not retained.
    /// </remarks>
    /// <param name="exception">The exception being handled.</param>
    /// <returns>The validation errors to disclose, or <see langword="null"/> when the exception carries none.</returns>
    protected virtual IDictionary<string, string[]>? GetValidationErrors(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is Excalibur.Dispatch.Exceptions.ValidationException validation ? validation.ValidationErrors : null;
    }

    [LoggerMessage(162510, LogLevel.Error, "[APPL]==> {RequestPath}\n[APPL]<== ERROR {StatusCode}: {Message}")]
    private static partial void LogExceptionOccurred(ILogger logger, Exception exception, string requestPath,
        int statusCode, string message);
}
