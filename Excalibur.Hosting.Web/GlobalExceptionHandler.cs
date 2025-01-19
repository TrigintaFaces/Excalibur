using System.Diagnostics;

using Excalibur.Exceptions;
using Excalibur.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ValidationException = FluentValidation.ValidationException;

namespace Excalibur.Hosting.Web;

/// <summary>
///     Handles global exceptions by logging them and returning standardized problem details responses.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
	private const string UnHandledExceptionMessage = "An unhandled exception has occurred.";
	private readonly IHostEnvironment env;
	private readonly ILogger<GlobalExceptionHandler> logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="GlobalExceptionHandler" /> class.
	/// </summary>
	/// <param name="env"> The hosting environment, used to determine if the application is in development. </param>
	/// <param name="logger"> The logger instance for logging exception details. </param>
	public GlobalExceptionHandler(IHostEnvironment env, ILogger<GlobalExceptionHandler> logger)
	{
		ArgumentNullException.ThrowIfNull(env, nameof(env));
		ArgumentNullException.ThrowIfNull(logger, nameof(logger));

		this.env = env;
		this.logger = logger;
	}

	/// <inheritdoc />
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
		ArgumentNullException.ThrowIfNull(exception, nameof(exception));

		var statusCode = exception.GetStatusCode() ?? StatusCodes.Status500InternalServerError;
		var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier ?? string.Empty;
		var exceptionId = exception is ApiException apiException ? apiException.Id : Uuid7Extensions.GenerateGuid();
		var problemDetails = BuildProblemDetails(exception, statusCode, traceId, exceptionId);

		LogException(httpContext, exception, traceId, exceptionId, statusCode, logger);

		httpContext!.Response.StatusCode = statusCode;
		httpContext.Response.ContentType = "application/problem+json";

		if (!await TryWriteToProblemDetailsServiceAsync(httpContext, exception, problemDetails).ConfigureAwait(false))
		{
			// Fallback if we can't write to ProblemDetailsService
			await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		return true;
	}

	/// <summary>
	///     Attempts to write the problem details using an <see cref="IProblemDetailsService" />, if available.
	/// </summary>
	/// <param name="httpContext"> The current HTTP context. </param>
	/// <param name="exception"> The exception to handle. </param>
	/// <param name="problemDetails"> The problem details object to write. </param>
	/// <returns> A task that resolves to <c> true </c> if the service successfully wrote the response; otherwise, <c> false </c>. </returns>
	private static async Task<bool> TryWriteToProblemDetailsServiceAsync(HttpContext httpContext, Exception exception,
		ProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
		ArgumentNullException.ThrowIfNull(exception, nameof(exception));
		ArgumentNullException.ThrowIfNull(problemDetails, nameof(problemDetails));

		if (httpContext.RequestServices.GetService<IProblemDetailsService>() is { } problemDetailsService)
		{
			return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
			{
				HttpContext = httpContext,
				Exception = exception,
				ProblemDetails = problemDetails
			}).ConfigureAwait(false);
		}

		return false;
	}

	/// <summary>
	///     Logs the exception details with additional contextual information.
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
		var scopeIdentifiers = new Dictionary<string, object> { ["TraceId"] = traceId, ["ExceptionId"] = exceptionId };

		using (logger.BeginScope(scopeIdentifiers))
		{
			logger.LogError(
				exception,
				"[APPL]==> {Request}\n[APPL]<== ERROR {StatusCode}: {Message}",
				httpContext.Request.Path,
				statusCode,
				exception.Message);
		}
	}

	/// <summary>
	///     Builds a <see cref="ProblemDetails" /> object based on the given exception and additional context.
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
			Title = reasonPhrase,
			Status = statusCode,
			Instance = $"urn:{env.ApplicationName}:error:{exceptionId:D}",
			Extensions = { ["TraceId"] = traceId }
		};

		if (exception is ValidationException validationException)
		{
			problemDetails.Extensions["ValidationErrors"] = validationException.Errors;
		}

		if (!env.IsDevelopment() && statusCode >= 500)
		{
			problemDetails.Title = UnHandledExceptionMessage;
			problemDetails.Status = StatusCodes.Status500InternalServerError;
			problemDetails.Detail = "Oops, something went wrong";
			return problemDetails;
		}

		problemDetails.Detail = exception.Message;
		problemDetails.Extensions["ErrorCode"] = exception.GetErrorCode();
		problemDetails.Extensions["Stack"] = exception.Demystify().ToString();

		return problemDetails;
	}
}
