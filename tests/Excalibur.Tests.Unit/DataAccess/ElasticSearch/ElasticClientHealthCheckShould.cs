using Elastic.Clients.Elasticsearch.Cluster;

using Excalibur.DataAccess.ElasticSearch;

using FakeItEasy;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch;

public class ElasticClientHealthCheckShould
{
	[Fact]
	public async Task CheckHealthShouldReturnUnhealthyWhenElasticsearchThrows()
	{
		// Arrange
		var client = A.Fake<IElasticsearchHealthClient>();
#pragma warning disable CA2201 // Do not raise reserved exception types
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		_ = A.CallTo(() => client.GetClusterHealthAsync(A<CancellationToken>.Ignored))
			.ThrowsAsync(new Exception("Connection failed"));
#pragma warning restore CA1303 // Do not pass literals as localized parameters
#pragma warning restore CA2201 // Do not raise reserved exception types

		var healthCheck = new ElasticClientHealthCheck(client);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		_ = result.Exception.ShouldNotBeNull();
		result.Description.ShouldContain("Exception while checking");
	}

	[Fact]
	public async Task ShouldReturnUnhealthyWhenClusterStatusIsRed()
	{
		var client = A.Fake<IElasticsearchHealthClient>();
		_ = A.CallTo(() => client.GetClusterHealthAsync(A<CancellationToken>._))
			.Returns(new HealthResponse { Status = Elastic.Clients.Elasticsearch.HealthStatus.Red });

		var check = new ElasticClientHealthCheck(client);

		var result = await check.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldBe("Cluster is in RED state.");
	}

	[Fact]
	public async Task ShouldReturnHealthyWhenClusterStatusIsGreen()
	{
		// Arrange
		var client = A.Fake<IElasticsearchHealthClient>();
		_ = A.CallTo(() => client.GetClusterHealthAsync(A<CancellationToken>._))
			.Returns(new HealthResponse { Status = Elastic.Clients.Elasticsearch.HealthStatus.Green });

		var healthCheck = new ElasticClientHealthCheck(client);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldBe("Cluster status: Green");
	}
}
