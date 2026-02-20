// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Net;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using AspNetHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Health checker for AWS SNS connections.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsSnsHealthChecker" /> class. </remarks>
/// <param name="logger"> The logger. </param>
/// <param name="snsClient"> The SNS client. </param>
/// <param name="testTopicArn"> Optional test topic ARN for health checks. </param>
public sealed class AwsSnsHealthChecker(
	ILogger<AwsSnsHealthChecker> logger,
	IAmazonSimpleNotificationService snsClient,
	string? testTopicArn = null) : IHealthCheck
{
	private readonly ILogger<AwsSnsHealthChecker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IAmazonSimpleNotificationService _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));

	/// <summary>
	/// Checks the health of an SNS connection.
	/// </summary>
	/// <param name="context"> The health check context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The health check result. </returns>
	public async Task<AspNetHealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// If we have a test topic ARN, check it specifically
			if (!string.IsNullOrEmpty(testTopicArn))
			{
				var request = new GetTopicAttributesRequest { TopicArn = testTopicArn };

				var response = await _snsClient.GetTopicAttributesAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.HttpStatusCode == HttpStatusCode.OK)
				{
					stopwatch.Stop();

					var subscriptionCount = response.Attributes.GetValueOrDefault("SubscriptionsConfirmed", "unknown");

					_logger.LogDebug(
						"SNS health check succeeded for topic {TopicArn} in {ElapsedMs}ms",
						testTopicArn,
						stopwatch.ElapsedMilliseconds);

					data["TopicArn"] = testTopicArn;
					data["SubscriptionsConfirmed"] = subscriptionCount;
					data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
					return AspNetHealthCheckResult.Healthy(
						$"SNS connection is healthy. Topic accessible: {testTopicArn}",
						data);
				}
			}
			else
			{
				// Fall back to listing topics (lightweight operation)
				var request = new ListTopicsRequest { NextToken = null };

				var response = await _snsClient.ListTopicsAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.HttpStatusCode == HttpStatusCode.OK)
				{
					stopwatch.Stop();

					_logger.LogDebug(
						"SNS health check succeeded in {ElapsedMs}ms",
						stopwatch.ElapsedMilliseconds);

					data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
					return AspNetHealthCheckResult.Healthy(
						"SNS connection is healthy",
						data);
				}
			}

			stopwatch.Stop();

			_logger.LogWarning("SNS health check returned non-OK status");

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			return AspNetHealthCheckResult.Unhealthy(
				"SNS health check failed: non-OK status",
				data: data);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();

			_logger.LogWarning(ex, "SNS health check failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			data["Exception"] = ex.GetType().Name;
			return AspNetHealthCheckResult.Unhealthy(
				$"SNS health check failed: {ex.Message}",
				ex,
				data);
		}
	}
}
