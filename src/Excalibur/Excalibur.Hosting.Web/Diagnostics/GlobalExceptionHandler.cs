// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Extensions;

using Excalibur.Hosting.Diagnostics;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ValidationException = FluentValidation.ValidationException;

namespace Excalibur.Hosting.Web.Diagnostics;

/// <summary>
/// Handles global exceptions by logging them and returning standardized problem details responses.
/// </summary>
public partial class GlobalExceptionHandler : IExceptionHandler
{
	private const string UnHandledExceptionMessage = "An unhandled exception has occurred.";
	private readonly IHostEnvironment _env;
	private readonly ProblemDetailsOptions _options;
	private readonly ILogger<GlobalExceptionHandler> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="GlobalExceptionHandler" /> class.
	/// </summary>
	/// <param name="env"> The hosting environment, used to determine if the application is in development. </param>
	/// <param name="options"> The problem details options configuration. </param>
	/// <param name="logger"> The logger instance for logging exception details. </param>
	public GlobalExceptionHandler(
		IHostEnvironment env,
		IOptions<ProblemDetailsOptions> options,
		ILogger<GlobalExceptionHandler> logger)
	{
		ArgumentNullException.ThrowIfNull(env);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_env = env;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		ArgumentNullException.ThrowIfNull(exception);

		var statusCode = exception.GetStatusCode() ?? StatusCodes.Status500InternalServerError;
		var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier ?? string.Empty;
		var exceptionId = exception is ApiException apiException ? apiException.Id : Uuid7Extensions.GenerateGuid();
		var problemDetails = BuildProblemDetails(exception, statusCode, traceId, exceptionId);

		LogException(httpContext, exception, traceId, exceptionId, statusCode, _logger);

		httpContext.Response.StatusCode = statusCode;
		httpContext.Response.ContentType = "application/problem+json";

		if (!await TryWriteToProblemDetailsServiceAsync(httpContext, exception, problemDetails).ConfigureAwait(false))
		{
			// Fallback using source-generated JSON context (AOT-safe)
			await httpContext.Response.WriteAsJsonAsync(
				problemDetails,
				ProblemDetailsJsonContext.Default.ProblemDetails,
				contentType: "application/problem+json",
				cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		return true;
	}

	[LoggerMessage(ExcaliburHostingEventId.GlobalExceptionOccurred, LogLevel.Error,
		"[APPL]==> {RequestPath}\n[APPL]<== ERROR {StatusCode}: {Message}")]
	private static partial void LogExceptionOccurred(ILogger<GlobalExceptionHandler> logger, Exception exception, string requestPath,
		int statusCode, string message);

	/// <summary>
	/// Attempts to write the problem details using an <see cref="IProblemDetailsService" />, if available.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <param name="exception"> The exception to handle. </param>
	/// <param name="problemDetails"> The problem details object to write. </param>
	/// <returns> A task that resolves to <c> true </c> if the service successfully wrote the response; otherwise, <c> false </c>. </returns>
	private static async Task<bool> TryWriteToProblemDetailsServiceAsync(HttpContext httpContext, Exception exception,
		ProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(problemDetails);

		if (httpContext.RequestServices.GetService<IProblemDetailsService>() is { } problemDetailsService)
		{
			return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
			{
				HttpContext = httpContext,
				Exception = exception,
				ProblemDetails = problemDetails,
			}).ConfigureAwait(false);
		}

		return false;
	}

	/// <summary>
	/// Logs the exception details with additional contextual information.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <param name="exception"> The exception to log. </param>
	/// <param name="traceId"> The trace identifier for the current request. </param>
	/// <param name="exceptionId"> The unique identifier for the exception. </param>
	/// <param name="statusCode"> The HTTP status code to associate with the exception. </param>
	/// <param name="logger"> The logger to use for logging. </param>
	private static void LogException(
		HttpContext httpContext,
		Exception exception,
		string traceId,
		Guid exceptionId,
		int statusCode,
		ILogger<GlobalExceptionHandler> logger)
	{
		var scopeIdentifiers =
			new Dictionary<string, object>(StringComparer.Ordinal) { ["TraceId"] = traceId, ["ExceptionId"] = exceptionId };

		using (logger.BeginScope(scopeIdentifiers))
		{
			if (logger.IsEnabled(LogLevel.Error))
			{
				LogExceptionOccurred(
					logger,
					exception,
					httpContext.Request.Path,
					statusCode,
					exception.Message);
			}
		}
	}

	private string GetLocalizedMdnStatusUrl(int statusCode)
	{
		var currentCulture = CultureInfo.CurrentUICulture.Name;
		var locale = _options.SupportedLocales.Contains(currentCulture)
			? currentCulture
			: "en-US";

		return $"{_options.StatusTypeBaseUrl}/{locale}/docs/Web/HTTP/Status/{statusCode}";
	}

	/// <summary>
	/// Builds a <see cref="ProblemDetails" /> object based on the given exception and additional context.
	/// </summary>
	/// <param name="exception"> The exception to represent as problem details. </param>
	/// <param name="statusCode"> The HTTP status code to associate with the exception. </param>
	/// <param name="traceId"> The trace identifier for the current request. </param>
	/// <param name="exceptionId"> The unique identifier for the exception. </param>
	/// <returns> A populated <see cref="ProblemDetails" /> instance. </returns>
	private ProblemDetails BuildProblemDetails(Exception exception, int statusCode, string traceId, Guid exceptionId)
	{
		var reasonPhrase = ReasonPhrases.GetReasonPhrase(statusCode);

		if (string.IsNullOrWhiteSpace(reasonPhrase))
		{
			reasonPhrase = UnHandledExceptionMessage;
		}

		var problemDetails = new ProblemDetails
		{
			Type = GetLocalizedMdnStatusUrl(statusCode),
			Title = reasonPhrase,
			Status = statusCode,
			Instance = $"urn:{_env.ApplicationName}:error:{exceptionId:D}",
			Extensions = { ["traceId"] = traceId },
		};

		if (exception is ValidationException validationException && validationException.Errors?.Any() == true)
		{
			problemDetails.Extensions["validationErrors"] = validationException.Errors;
		}

		if (!_env.IsDevelopment() && statusCode >= 500)
		{
			problemDetails.Title = UnHandledExceptionMessage;
			problemDetails.Status = StatusCodes.Status500InternalServerError;
			problemDetails.Detail = "Oops, something went wrong";
			return problemDetails;
		}

		problemDetails.Detail = exception.Message;
		problemDetails.Extensions["errorCode"] = exception.GetErrorCode();
		problemDetails.Extensions["stack"] = exception.Demystify().ToString();

		return problemDetails;
	}
}
