// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.Cluster;

using Excalibur.Data.ElasticSearch;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticClientHealthCheckShould
{
	private readonly IElasticsearchHealthClient _healthClient;
	private readonly ElasticClientHealthCheck _sut;

	public ElasticClientHealthCheckShould()
	{
		_healthClient = A.Fake<IElasticsearchHealthClient>();
		_sut = new ElasticClientHealthCheck(_healthClient);
	}

	[Fact]
	public async Task ReturnUnhealthyWhenExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _healthClient.GetClusterHealthAsync(A<CancellationToken>._))
			.ThrowsAsync(new HttpRequestException("Connection refused"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
		result.Description.ShouldContain("Failed to check");
		result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnUnhealthyWhenTimeoutExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _healthClient.GetClusterHealthAsync(A<CancellationToken>._))
			.ThrowsAsync(new TaskCanceledException("Request timed out"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
		result.Description.ShouldContain("Failed to check");
		result.Exception.ShouldBeOfType<TaskCanceledException>();
	}

	[Fact]
	public async Task ReturnUnhealthyWhenGenericExceptionOccurs()
	{
		// Arrange
		A.CallTo(() => _healthClient.GetClusterHealthAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Something unexpected"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
		result.Exception.ShouldBeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task CallHealthClientWithCancellationToken()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		A.CallTo(() => _healthClient.GetClusterHealthAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("test"));

		// Act
		await _sut.CheckHealthAsync(new HealthCheckContext(), token);

		// Assert
		A.CallTo(() => _healthClient.GetClusterHealthAsync(token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleNullContextGracefully()
	{
		// Arrange
		A.CallTo(() => _healthClient.GetClusterHealthAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("test"));

		// Act - null context should not cause NRE since CheckHealthAsync doesn't use context
		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy);
	}
}
