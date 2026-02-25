// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Management;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqManagementModelsShould
{
	[Fact]
	public void CreateQueueInfo()
	{
		// Arrange & Act
		var info = new QueueInfo("orders", 1500, 3, "running", "quorum");

		// Assert
		info.Name.ShouldBe("orders");
		info.Messages.ShouldBe(1500);
		info.Consumers.ShouldBe(3);
		info.State.ShouldBe("running");
		info.Type.ShouldBe("quorum");
	}

	[Fact]
	public void SupportQueueInfoRecordEquality()
	{
		var q1 = new QueueInfo("q", 0, 1, "running", "classic");
		var q2 = new QueueInfo("q", 0, 1, "running", "classic");
		q1.ShouldBe(q2);
	}

	[Fact]
	public void CreateExchangeInfo()
	{
		// Arrange & Act
		var info = new ExchangeInfo("events", "topic", true, false, false);

		// Assert
		info.Name.ShouldBe("events");
		info.Type.ShouldBe("topic");
		info.Durable.ShouldBeTrue();
		info.AutoDelete.ShouldBeFalse();
		info.Internal.ShouldBeFalse();
	}

	[Fact]
	public void SupportExchangeInfoRecordEquality()
	{
		var e1 = new ExchangeInfo("ex", "direct", true, false, true);
		var e2 = new ExchangeInfo("ex", "direct", true, false, true);
		e1.ShouldBe(e2);
	}

	[Fact]
	public void CreateConnectionInfo()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var props = new Dictionary<string, string> { ["product"] = "MyApp" };

		// Act
		var info = new ConnectionInfo("conn-1", "running", 5, props, now);

		// Assert
		info.Name.ShouldBe("conn-1");
		info.State.ShouldBe("running");
		info.Channels.ShouldBe(5);
		info.ClientProperties.ShouldContainKeyAndValue("product", "MyApp");
		info.ConnectedAt.ShouldBe(now);
	}

	[Fact]
	public void CreateBrokerOverview()
	{
		// Arrange & Act
		var overview = new BrokerOverview("rabbit@node1", "3.13.0", "26.2", 42, 10);

		// Assert
		overview.ClusterName.ShouldBe("rabbit@node1");
		overview.RabbitMqVersion.ShouldBe("3.13.0");
		overview.ErlangVersion.ShouldBe("26.2");
		overview.TotalQueues.ShouldBe(42);
		overview.TotalConnections.ShouldBe(10);
	}

	[Fact]
	public void SupportBrokerOverviewRecordEquality()
	{
		var b1 = new BrokerOverview("cluster", "3.13.0", "26.2", 10, 5);
		var b2 = new BrokerOverview("cluster", "3.13.0", "26.2", 10, 5);
		b1.ShouldBe(b2);
	}
}
