// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.HealthChecking;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSnsHealthCheckerShould
{
	private readonly IAmazonSimpleNotificationService _snsClient;

	public AwsSnsHealthCheckerShould()
	{
		_snsClient = A.Fake<IAmazonSimpleNotificationService>();
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSnsHealthChecker(null!, _snsClient));
	}

	[Fact]
	public void ThrowWhenSnsClientIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new AwsSnsHealthChecker(NullLogger<AwsSnsHealthChecker>.Instance, null!));
	}

	[Fact]
	public async Task ReturnHealthyWhenTopicArnCheckSucceeds()
	{
		// Arrange
		var response = new GetTopicAttributesResponse
		{
			HttpStatusCode = HttpStatusCode.OK,
			Attributes = new Dictionary<string, string>
			{
				["SubscriptionsConfirmed"] = "3",
			},
		};
		A.CallTo(() => _snsClient.GetTopicAttributesAsync(
				A<GetTopicAttributesRequest>._, A<CancellationToken>._))
			.Returns(response);

		var checker = new AwsSnsHealthChecker(
			NullLogger<AwsSnsHealthChecker>.Instance, _snsClient,
			"arn:aws:sns:us-east-1:123456789:test-topic");

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldContainKey("TopicArn");
		result.Data.ShouldContainKey("SubscriptionsConfirmed");
		result.Data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public async Task ReturnHealthyWhenListTopicsSucceeds()
	{
		// Arrange — no test topic ARN, falls back to ListTopics
		A.CallTo(() => _snsClient.ListTopicsAsync(
				A<ListTopicsRequest>._, A<CancellationToken>._))
			.Returns(new ListTopicsResponse { HttpStatusCode = HttpStatusCode.OK });

		var checker = new AwsSnsHealthChecker(
			NullLogger<AwsSnsHealthChecker>.Instance, _snsClient);

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldContainKey("ResponseTimeMs");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenTopicCheckReturnsNonOk()
	{
		// Arrange
		A.CallTo(() => _snsClient.GetTopicAttributesAsync(
				A<GetTopicAttributesRequest>._, A<CancellationToken>._))
			.Returns(new GetTopicAttributesResponse { HttpStatusCode = HttpStatusCode.Forbidden });

		var checker = new AwsSnsHealthChecker(
			NullLogger<AwsSnsHealthChecker>.Instance, _snsClient,
			"arn:aws:sns:us-east-1:123456789:test-topic");

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenListTopicsReturnsNonOk()
	{
		// Arrange
		A.CallTo(() => _snsClient.ListTopicsAsync(
				A<ListTopicsRequest>._, A<CancellationToken>._))
			.Returns(new ListTopicsResponse { HttpStatusCode = HttpStatusCode.InternalServerError });

		var checker = new AwsSnsHealthChecker(
			NullLogger<AwsSnsHealthChecker>.Instance, _snsClient);

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
		A.CallTo(() => _snsClient.ListTopicsAsync(
				A<ListTopicsRequest>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("SNS unavailable"));

		var checker = new AwsSnsHealthChecker(
			NullLogger<AwsSnsHealthChecker>.Instance, _snsClient);

		// Act
		var result = await checker.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("SNS unavailable");
		result.Data.ShouldContainKey("Exception");
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}
}
