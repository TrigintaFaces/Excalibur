// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.HealthChecking;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSqsHealthCheckerShould
{
	private readonly IAmazonSQS _sqsClient;

	public AwsSqsHealthCheckerShould()
	{
		_sqsClient = A.Fake<IAmazonSQS>();
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSqsHealthChecker(null!, _sqsClient));
	}

	[Fact]
	public void ThrowWhenSqsClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSqsHealthChecker(NullLogger<AwsSqsHealthChecker>.Instance, null!));
	}

	[Fact]
	public async Task ReturnHealthyWhenQueueUrlCheckSucceeds()
	{
		// Arrange
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(
				A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse { HttpStatusCode = HttpStatusCode.OK });

		var checker = new AwsSqsHealthChecker(
			NullLogger<AwsSqsHealthChecker>.Instance, _sqsClient, "https://sqs.us-east-1.amazonaws.com/123/test");

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldContainKey("QueueUrl");
		result.Data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public async Task ReturnHealthyWhenListQueuesSucceeds()
	{
		// Arrange — no test queue URL, falls back to ListQueues
		A.CallTo(() => _sqsClient.ListQueuesAsync(
				A<ListQueuesRequest>._, A<CancellationToken>._))
			.Returns(new ListQueuesResponse { HttpStatusCode = HttpStatusCode.OK });

		var checker = new AwsSqsHealthChecker(
			NullLogger<AwsSqsHealthChecker>.Instance, _sqsClient);

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenQueueUrlCheckReturnsNonOk()
	{
		// Arrange
		A.CallTo(() => _sqsClient.GetQueueAttributesAsync(
				A<GetQueueAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetQueueAttributesResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable });

		var checker = new AwsSqsHealthChecker(
			NullLogger<AwsSqsHealthChecker>.Instance, _sqsClient, "https://sqs.us-east-1.amazonaws.com/123/test");

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenListQueuesReturnsNonOk()
	{
		// Arrange
		A.CallTo(() => _sqsClient.ListQueuesAsync(
				A<ListQueuesRequest>._, A<CancellationToken>._))
			.Returns(new ListQueuesResponse { HttpStatusCode = HttpStatusCode.InternalServerError });

		var checker = new AwsSqsHealthChecker(
			NullLogger<AwsSqsHealthChecker>.Instance, _sqsClient);

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _sqsClient.ListQueuesAsync(
				A<ListQueuesRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Connection failed"));

		var checker = new AwsSqsHealthChecker(
			NullLogger<AwsSqsHealthChecker>.Instance, _sqsClient);

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("Connection failed");
		result.Data.ShouldContainKey("Exception");
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}
}
