// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Net;

using Amazon.SQS;
using Amazon.SQS.Model;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using AspNetHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Health checker for AWS SQS connections.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AwsSqsHealthChecker" /> class. </remarks>
/// <param name="logger"> The logger. </param>
/// <param name="sqsClient"> The SQS client. </param>
/// <param name="testQueueUrl"> Optional test queue URL for health checks. </param>
public sealed class AwsSqsHealthChecker(
	ILogger<AwsSqsHealthChecker> logger,
	IAmazonSQS sqsClient,
	string? testQueueUrl = null) : IHealthCheck
{
	private readonly ILogger<AwsSqsHealthChecker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IAmazonSQS _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));

	/// <summary>
	/// Checks the health of a SQS connection.
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
			// If we have a test queue URL, check it specifically
			if (!string.IsNullOrEmpty(testQueueUrl))
			{
				var request = new GetQueueAttributesRequest { QueueUrl = testQueueUrl, AttributeNames = ["All"] };

				var response = await _sqsClient.GetQueueAttributesAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.HttpStatusCode == HttpStatusCode.OK)
				{
					stopwatch.Stop();

					_logger.LogDebug(
						"SQS health check succeeded for queue {QueueUrl} in {ElapsedMs}ms",
						testQueueUrl,
						stopwatch.ElapsedMilliseconds);

					data["QueueUrl"] = testQueueUrl;
					data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
					return AspNetHealthCheckResult.Healthy(
						$"SQS connection is healthy. Queue accessible: {testQueueUrl}",
						data);
				}
			}
			else
			{
				// Fall back to listing queues (lightweight operation)
				var request = new ListQueuesRequest { MaxResults = 1 };

				var response = await _sqsClient.ListQueuesAsync(request, cancellationToken)
					.ConfigureAwait(false);

				if (response.HttpStatusCode == HttpStatusCode.OK)
				{
					stopwatch.Stop();

					_logger.LogDebug(
						"SQS health check succeeded in {ElapsedMs}ms",
						stopwatch.ElapsedMilliseconds);

					data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
					return AspNetHealthCheckResult.Healthy(
						"SQS connection is healthy",
						data);
				}
			}

			stopwatch.Stop();

			_logger.LogWarning("SQS health check returned non-OK status");

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			return AspNetHealthCheckResult.Unhealthy(
				"SQS health check failed: non-OK status",
				data: data);
		}
		catch (Exception ex)
		{
			stopwatch.Stop();

			_logger.LogWarning(ex, "SQS health check failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

			data["ResponseTimeMs"] = stopwatch.ElapsedMilliseconds;
			data["Exception"] = ex.GetType().Name;
			return AspNetHealthCheckResult.Unhealthy(
				$"SQS health check failed: {ex.Message}",
				ex,
				data);
		}
	}
}
