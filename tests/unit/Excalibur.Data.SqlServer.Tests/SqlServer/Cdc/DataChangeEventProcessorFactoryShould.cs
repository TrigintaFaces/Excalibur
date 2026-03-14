// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="DataChangeEventProcessorFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class DataChangeEventProcessorFactoryShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		var appLifetime = A.Fake<IHostApplicationLifetime>();
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new DataChangeEventProcessorFactory(null!, appLifetime, policyFactory));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenAppLifetimeIsNull()
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		var policyFactory = A.Fake<IDataAccessPolicyFactory>();

		Should.Throw<ArgumentNullException>(() =>
			new DataChangeEventProcessorFactory(serviceProvider, null!, policyFactory));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPolicyFactoryIsNull()
	{
		var serviceProvider = A.Fake<IServiceProvider>();
		var appLifetime = A.Fake<IHostApplicationLifetime>();

		Should.Throw<ArgumentNullException>(() =>
			new DataChangeEventProcessorFactory(serviceProvider, appLifetime, null!));
	}

	[Fact]
	public void Create_ThrowsArgumentNullException_WhenDbConfigIsNull()
	{
		var factory = CreateFactory();

		Should.Throw<ArgumentNullException>(() =>
			factory.Create(
				null!,
				new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true"),
				new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true")));
	}

	[Fact]
	public void Create_ThrowsArgumentNullException_WhenCdcConnectionIsNull()
	{
		var factory = CreateFactory();
		var dbConfig = A.Fake<IDatabaseConfig>();

		Should.Throw<ArgumentNullException>(() =>
			factory.Create(
				dbConfig,
				null!,
				new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true")));
	}

	[Fact]
	public void Create_ThrowsArgumentNullException_WhenStateStoreConnectionIsNull()
	{
		var factory = CreateFactory();
		var dbConfig = A.Fake<IDatabaseConfig>();

		Should.Throw<ArgumentNullException>(() =>
			factory.Create(
				dbConfig,
				new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true"),
				null!));
	}

	[Fact]
	public void Create_ReturnsProcessorInstance()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(A.Fake<IHostApplicationLifetime>());
		services.AddSingleton(A.Fake<IDataAccessPolicyFactory>());
		var provider = services.BuildServiceProvider();

		var factory = new DataChangeEventProcessorFactory(
			provider,
			provider.GetRequiredService<IHostApplicationLifetime>(),
			provider.GetRequiredService<IDataAccessPolicyFactory>());

		var dbConfig = A.Fake<IDatabaseConfig>();
		A.CallTo(() => dbConfig.QueueSize).Returns(32);
		A.CallTo(() => dbConfig.ProducerBatchSize).Returns(16);
		A.CallTo(() => dbConfig.ConsumerBatchSize).Returns(8);
		A.CallTo(() => dbConfig.CaptureInstances).Returns(["dbo_orders"]);

		var result = factory.Create(
			dbConfig,
			new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true"),
			new SqlConnection("Server=localhost;Encrypt=false;TrustServerCertificate=true"));

		result.ShouldNotBeNull();
		result.ShouldBeAssignableTo<IDataChangeEventProcessor>();
	}

	private static DataChangeEventProcessorFactory CreateFactory()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		var provider = services.BuildServiceProvider();

		return new DataChangeEventProcessorFactory(
			provider,
			A.Fake<IHostApplicationLifetime>(),
			A.Fake<IDataAccessPolicyFactory>());
	}
}
