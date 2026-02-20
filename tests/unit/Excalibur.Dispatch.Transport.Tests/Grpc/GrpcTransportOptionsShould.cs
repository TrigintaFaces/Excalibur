// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcTransportOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new GrpcTransportOptions();

		// Assert
		options.ServerAddress.ShouldBe(string.Empty);
		options.DeadlineSeconds.ShouldBe(30);
		options.MaxSendMessageSize.ShouldBeNull();
		options.MaxReceiveMessageSize.ShouldBeNull();
		options.SendMethodPath.ShouldBe("/dispatch.transport.DispatchTransport/Send");
		options.SendBatchMethodPath.ShouldBe("/dispatch.transport.DispatchTransport/SendBatch");
		options.ReceiveMethodPath.ShouldBe("/dispatch.transport.DispatchTransport/Receive");
		options.SubscribeMethodPath.ShouldBe("/dispatch.transport.DispatchTransport/Subscribe");
		options.Destination.ShouldBe("grpc-default");
	}

	[Fact]
	public void AllowSettingServerAddress()
	{
		// Arrange
		var options = new GrpcTransportOptions();

		// Act
		options.ServerAddress = "https://localhost:5001";

		// Assert
		options.ServerAddress.ShouldBe("https://localhost:5001");
	}

	[Fact]
	public void AllowSettingDeadlineSeconds()
	{
		// Arrange
		var options = new GrpcTransportOptions();

		// Act
		options.DeadlineSeconds = 60;

		// Assert
		options.DeadlineSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingMaxSendMessageSize()
	{
		// Arrange
		var options = new GrpcTransportOptions();

		// Act
		options.MaxSendMessageSize = 4_194_304;

		// Assert
		options.MaxSendMessageSize.ShouldBe(4_194_304);
	}

	[Fact]
	public void AllowSettingMaxReceiveMessageSize()
	{
		// Arrange
		var options = new GrpcTransportOptions();

		// Act
		options.MaxReceiveMessageSize = 8_388_608;

		// Assert
		options.MaxReceiveMessageSize.ShouldBe(8_388_608);
	}

	[Fact]
	public void AllowSettingCustomMethodPaths()
	{
		// Arrange
		var options = new GrpcTransportOptions
		{
			SendMethodPath = "/custom.Service/Send",
			SendBatchMethodPath = "/custom.Service/SendBatch",
			ReceiveMethodPath = "/custom.Service/Receive",
			SubscribeMethodPath = "/custom.Service/Subscribe",
		};

		// Assert
		options.SendMethodPath.ShouldBe("/custom.Service/Send");
		options.SendBatchMethodPath.ShouldBe("/custom.Service/SendBatch");
		options.ReceiveMethodPath.ShouldBe("/custom.Service/Receive");
		options.SubscribeMethodPath.ShouldBe("/custom.Service/Subscribe");
	}

	[Fact]
	public void AllowSettingDestination()
	{
		// Arrange
		var options = new GrpcTransportOptions();

		// Act
		options.Destination = "my-grpc-service";

		// Assert
		options.Destination.ShouldBe("my-grpc-service");
	}
}
