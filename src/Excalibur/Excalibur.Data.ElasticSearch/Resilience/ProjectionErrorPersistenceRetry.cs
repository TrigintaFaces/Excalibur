// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net.Sockets;

using Elastic.Transport;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>Scalar retry eligibility policies for create-only projection error records.</summary>
/// <remarks>
/// The generic client pipeline handles exceptions and circuit state; error persistence additionally
/// needs response and per-item classification. Its result is retry eligibility, not write success. It owns its budget and disables SDK retries per request.
/// Only policy is shared: responses, pending records and failure counts stay in each execution's closure.
/// </remarks>
internal static class ProjectionErrorPersistenceRetry
{
	internal static bool IsTransientStatus(int? status) => status is 429 or 502 or 503 or 504;

	internal static bool IsRetryableFailure(int? status, Exception? error) =>
		status is { } knownStatus ? IsTransientStatus(knownStatus) : IsTransientFailure(error);

	internal static bool IsTransientFailure(Exception? exception) => exception switch
	{
		HttpRequestException request => (request.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.ResponseEnded or HttpRequestError.NameResolutionError) ||
			(request.HttpRequestError == HttpRequestError.Unknown && request.InnerException is IOException { InnerException: SocketException socket } && IsTransientSocket(socket.SocketErrorCode)),
		HttpIOException io => io.HttpRequestError is HttpRequestError.ResponseEnded,
		TimeoutException => true,
		TaskCanceledException { InnerException: TimeoutException } => true,
		TransportException transport => transport.ApiCallDetails?.HttpStatusCode is { } knownStatus
			? IsTransientStatus(knownStatus)
			: IsKnownNetworkFailure(transport.ApiCallDetails?.OriginalException ?? transport.InnerException),
		_ => false,
	};

	private static bool IsKnownNetworkFailure(Exception? exception) => exception is not TransportException && IsTransientFailure(exception);

	private static bool IsTransientSocket(SocketError error) =>
		error is SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.ConnectionRefused or SocketError.NetworkUnreachable or SocketError.TimedOut;

	internal static ResiliencePipeline<bool> Create(ProjectionRetryOptions settings, int maxAttempts,
		ILogger logger, string operation)
	{
		var builder = new ResiliencePipelineBuilder<bool>();
		if (!settings.Enabled)
		{
			return builder.Build();
		}

		ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
		if (settings.BaseDelay < TimeSpan.Zero || settings.MaxDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(settings), "Retry delays must be non-negative.");
		}
		if (!double.IsFinite(settings.JitterFactor) || settings.JitterFactor is < 0 or > 1)
		{
			throw new ArgumentOutOfRangeException(nameof(settings), "JitterFactor must be finite and between 0 and 1.");
		}

		if (maxAttempts > 1)
		{
			_ = builder.AddRetry(new RetryStrategyOptions<bool>
			{
				MaxRetryAttempts = maxAttempts - 1,
				ShouldHandle = args => ValueTask.FromResult(!args.Context.CancellationToken.IsCancellationRequested &&
					(IsTransientFailure(args.Outcome.Exception) ||
						(args.Outcome.Exception is null && args.Outcome.Result))),
				DelayGenerator = args => ValueTask.FromResult<TimeSpan?>(CalculateDelay(settings, args.AttemptNumber, Random.Shared.NextDouble())),
				OnRetry = args =>
				{
					logger.LogWarning("Retrying projection error persistence {Operation}: attempt {Attempt}/{MaxAttempts} after {RetryDelay}",
						operation, args.AttemptNumber + 2, maxAttempts, args.RetryDelay);
					return ValueTask.CompletedTask;
				},
			});
		}
		return builder.Build();
	}

	internal static TimeSpan CalculateDelay(ProjectionRetryOptions settings, int attemptNumber, double sample)
	{
		var jitter = 1 + (settings.JitterFactor * ((2 * sample) - 1));
		if (settings.BaseDelay == TimeSpan.Zero || jitter <= 0)
		{
			return TimeSpan.Zero;
		}
		var multiplier = settings.UseExponentialBackoff ? Math.Pow(2, attemptNumber) : 1;
		var ticks = settings.BaseDelay.Ticks * multiplier * jitter;
		// Custom Polly delay generators must apply their own cap. Also avoid an overflowing double-to-long cast.
		return ticks >= settings.MaxDelay.Ticks ? settings.MaxDelay : TimeSpan.FromTicks((long)ticks);
	}
}
