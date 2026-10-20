// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>Bounds SDK send operations with one retry owner and a total deadline.</summary>
internal sealed partial class AwsSqsSendClient
{
	private readonly IMessageFailureClassifier _classifier;
	private readonly IAmazonSQS _client;
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _deadline;
	private readonly ResiliencePipeline _foreground;

	internal AwsSqsSendClient(IAmazonSQS client, AwsSqsOptions options, ILogger logger, IMessageFailureClassifier? classifier = null)
	{
		_client = client;
		_logger = logger;
		_classifier = classifier ?? new DefaultMessageFailureClassifier();
		_deadline = new ResiliencePipelineBuilder().AddTimeout(options.RequestTimeout).Build();
		var builder = new ResiliencePipelineBuilder().AddTimeout(options.RequestTimeout);
		if (options.MaxRetryAttempts > 0)
		{
			builder.AddRetry(new RetryStrategyOptions
			{
				MaxRetryAttempts = options.MaxRetryAttempts,
				Delay = TimeSpan.FromMilliseconds(options.RetryPolicy.BaseDelayMs),
				MaxDelay = TimeSpan.FromMilliseconds(options.RetryPolicy.MaxDelayMs),
				BackoffType = options.RetryPolicy.UseExponentialBackoff ? DelayBackoffType.Exponential : DelayBackoffType.Constant,
				UseJitter = true,
				ShouldHandle = new PredicateBuilder().Handle<Exception>(error =>
					IsKnownTransient(error) && _classifier.Classify(error) == MessageFailureKind.Transient),
			});
		}
		_foreground = builder.Build();
	}

	internal async Task SendAsync(SendMessageRequest request, IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var owned = RetryOwnershipScope.Owns(message, context);
		var zeroRetries = HasZeroSdkRetries(owned);
		var owner = owned ? "dispatch" : zeroRetries ? "transport" : "sdk";
		var pipeline = !owned && zeroRetries ? _foreground : _deadline;
		var attempt = 0;
		await pipeline.ExecuteAsync(async token =>
		{
			token.ThrowIfCancellationRequested();
			attempt++;
			Activity.Current?.SetTag("excalibur.sqs.retry.owner", owner);
			Activity.Current?.SetTag("excalibur.sqs.sdk.operation.attempt", attempt);
			LogSendAttempt(_logger, attempt, owner, context.MessageId);
			try
			{
				_ = await _client.SendMessageAsync(request, token).ConfigureAwait(false);
			}
			catch (Exception error) when (token.IsCancellationRequested && error is not OperationCanceledException)
			{
				throw new OperationCanceledException("SQS send canceled.", error, token);
			}
			catch (AmazonServiceException error) when (owned)
			{
				var permanent = NormalizePermanent(error);
				if (permanent is not null) { throw permanent; }
				throw;
			}
		}, cancellationToken).ConfigureAwait(false);
	}

	private static bool IsKnownTransient(Exception error) => error switch
	{
		OperationCanceledException => false,
		AmazonServiceException aws => !IsKnownPermanent(aws) &&
			(aws.ErrorCode is "RequestThrottled" or "KmsThrottled" or "Throttling" or "ThrottlingException" or "RequestTimeout" or "RequestTimeoutException" ||
			 aws.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)aws.StatusCode is >= 500 and <= 599),
		HttpRequestException http => http.StatusCode is null or HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout ||
			(int?)http.StatusCode is >= 500 and <= 599,
		IOException or SocketException or TimeoutException => true,
		_ => false,
	};

	private static bool IsKnownPermanent(AmazonServiceException error) =>
		error.StatusCode == HttpStatusCode.Forbidden || error.ErrorCode is
			"AccessDenied" or "AccessDeniedException" or "KmsAccessDenied" or "InvalidMessageContents" or
			"QueueDoesNotExist" or "AWS.SimpleQueueService.NonExistentQueue" or "InvalidAddress" or "InvalidSecurity" or
			"KmsInvalidKeyUsage" or "KmsNotFound" or "KmsOptInRequired" or "UnsupportedOperation";

	private static Exception? NormalizePermanent(AmazonServiceException error) => error.ErrorCode switch
	{
		"AccessDenied" or "AccessDeniedException" or "KmsAccessDenied" => new ConfigurationException(error.Message, error),
		"InvalidMessageContents" => new DispatchSerializationException(error.Message, error),
		"QueueDoesNotExist" or "AWS.SimpleQueueService.NonExistentQueue" or "InvalidAddress" or "InvalidSecurity" or
			"KmsInvalidKeyUsage" or "KmsNotFound" or "KmsOptInRequired" or "UnsupportedOperation" => new ConfigurationException(error.Message, error),
		_ when error.StatusCode == HttpStatusCode.Forbidden => new ConfigurationException(error.Message, error),
		_ => null,
	};

	private bool HasZeroSdkRetries(bool owned)
	{
		try
		{
			var config = _client.Config;
			if (config is null)
			{
				if (owned) { throw new ConfigurationException("Owned SQS sends require readable SDK retry configuration."); }
				return false;
			}
			if (config.MaxErrorRetry == 0 && config.MaxStaleConnectionRetries == 0) { return true; }
			if (owned)
			{
				throw new ConfigurationException("Owned SQS sends require MaxErrorRetry=0 and MaxStaleConnectionRetries=0; configure a separate send client.");
			}
			return false;
		}
		catch (ConfigurationException) { throw; }
		catch (Exception error)
		{
			if (owned) { throw new ConfigurationException("Owned SQS sends require readable SDK retry configuration.", error); }
			return false;
		}
	}

	[LoggerMessage(LogLevel.Debug, "SQS SDK operation attempt {Attempt}; retry owner {RetryOwner}; message {MessageId}")]
	private static partial void LogSendAttempt(ILogger logger, int attempt, string retryOwner, string? messageId);
}
