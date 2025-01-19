using System.Diagnostics;

using Excalibur.Application.Requests;
using Excalibur.Exceptions;

using MediatR;

using Microsoft.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Application.Behaviors;

/// <summary>
///     Implements a pipeline behavior for logging the request and response details.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

	/// <summary>
	///     Initializes a new instance of the <see cref="LoggingBehavior{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance used for logging. </param>
	public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
	{
		ArgumentNullException.ThrowIfNull(logger);

		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		var scopeData = new Dictionary<string, object>();

		if (request is IAmCorrelatable correlationRequest)
		{
			scopeData.Add(nameof(CorrelationId), correlationRequest.CorrelationId);
		}

		if (request is IAmMultiTenant tenantRequest)
		{
			scopeData.Add(nameof(TenantId), tenantRequest.TenantId);
		}

		using (_logger.BeginScope(scopeData))
		{
			try
			{
				var response = await next().ConfigureAwait(false);
				var requestTypeName = TypeNameHelper.GetTypeDisplayName(typeof(TRequest), fullName: false);

				if (!Equals(response, default(TResponse)))
				{
					var responseTypeName = TypeNameHelper.BuiltInTypeNames.ContainsKey(typeof(TResponse)) || response is Guid
						? response!.ToString()
						: TypeNameHelper.GetTypeDisplayName(typeof(TResponse), fullName: false);

					_logger.LogRequestWithResponse(requestTypeName, responseTypeName);
				}
				else
				{
					_logger.LogRequestWithoutResponse(requestTypeName);
				}

				return response;
			}
			catch (AggregateException ex) when (ex.InnerException != null)
			{
				_logger.LogError(
					TypeNameHelper.GetTypeDisplayName(typeof(TRequest), fullName: false),
					ApiException.GetStatusCode(ex.InnerException),
					ex.InnerException.Message,
					ex.InnerException);

				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					TypeNameHelper.GetTypeDisplayName(typeof(TRequest), fullName: false),
					ApiException.GetStatusCode(ex),
					ex.Message,
					ex);

				throw;
			}
		}
	}
}

public static class LoggingExtensions
{
	private static readonly Action<ILogger, string, string?, Exception?> SLogRequestWithResponse =
		LoggerMessage.Define<string, string?>(
			LogLevel.Debug,
			new EventId(1, "LogRequestWithResponse"),
			"[APPL]==> {Request}\n[APPL]<== OK: {Response}");

	private static readonly Action<ILogger, string, Exception?> SLogRequestWithoutResponse =
		LoggerMessage.Define<string>(
			LogLevel.Debug,
			new EventId(2, "LogRequestWithoutResponse"),
			"[APPL]==> {Request}\n[APPL]<== OK");

	private static readonly Action<ILogger, string, int?, string, Exception?> SLogError =
		LoggerMessage.Define<string, int?, string>(
			LogLevel.Error,
			new EventId(3, "LogError"),
			"[APPL]==> {Request}\n[APPL]<== ERROR {StatusCode}: {Message}");

	public static void LogRequestWithResponse(this ILogger logger, string requestName, string? responseName) =>
		SLogRequestWithResponse(logger, requestName, responseName, null);

	public static void LogRequestWithoutResponse(this ILogger logger, string requestName) =>
		SLogRequestWithoutResponse(logger, requestName, null);

	public static void LogError(this ILogger logger, string requestName, int? statusCode, string message, Exception exception) =>
		SLogError(logger, requestName, statusCode, message, exception);
}
