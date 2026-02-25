// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportServiceCollectionExtensions"/> covering
/// GrpcChannel factory registration with MaxMessageSize options, ValidateOnStart registration,
/// and service descriptor idempotency.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void RegisterGrpcChannelAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001");

		// Assert — GrpcChannel descriptor should be registered
		services.ShouldContain(sd => sd.ServiceType == typeof(GrpcChannel));
	}

	[Fact]
	public void RegisterOptionsWithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001");

		// Assert — IValidateOptions<GrpcTransportOptions> should be registered
		// ValidateDataAnnotations registers an IValidateOptions validator
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<GrpcTransportOptions>));
	}

	[Fact]
	public void ResolveOptionsWithCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://custom-host:9090";
			options.DeadlineSeconds = 120;
			options.Destination = "my-queue";
			options.SendMethodPath = "/custom/Send";
			options.SendBatchMethodPath = "/custom/SendBatch";
			options.ReceiveMethodPath = "/custom/Receive";
			options.SubscribeMethodPath = "/custom/Subscribe";
		});

		// Act
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;

		// Assert
		options.ServerAddress.ShouldBe("https://custom-host:9090");
		options.DeadlineSeconds.ShouldBe(120);
		options.Destination.ShouldBe("my-queue");
		options.SendMethodPath.ShouldBe("/custom/Send");
		options.SendBatchMethodPath.ShouldBe("/custom/SendBatch");
		options.ReceiveMethodPath.ShouldBe("/custom/Receive");
		options.SubscribeMethodPath.ShouldBe("/custom/Subscribe");
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001");

		// Assert — should return the same IServiceCollection for fluent chaining
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void NotRegisterDuplicateGrpcChannel_WhenCalledTwice()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options => options.ServerAddress = "https://server1:5001");
		services.AddGrpcTransport(options => options.ServerAddress = "https://server2:5001");

		// Assert — TryAddSingleton prevents duplicate GrpcChannel registration
		services.Count(sd => sd.ServiceType == typeof(GrpcChannel)).ShouldBe(1);
	}

	[Fact]
	public void RegisterMaxSendMessageSizeOption()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
			options.MaxSendMessageSize = 4_194_304;
		});

		// Act
		var sp = services.BuildServiceProvider();
		var opts = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;

		// Assert
		opts.MaxSendMessageSize.ShouldBe(4_194_304);
	}

	[Fact]
	public void RegisterMaxReceiveMessageSizeOption()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
			options.MaxReceiveMessageSize = 8_388_608;
		});

		// Act
		var sp = services.BuildServiceProvider();
		var opts = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;

		// Assert
		opts.MaxReceiveMessageSize.ShouldBe(8_388_608);
	}

	[Fact]
	public void RegisterAllThreeTransportInterfaces()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001");

		// Assert — all three transport interfaces registered as singletons
		var senderDescriptor = services.Single(sd => sd.ServiceType == typeof(ITransportSender));
		senderDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		var receiverDescriptor = services.Single(sd => sd.ServiceType == typeof(ITransportReceiver));
		receiverDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);

		var subscriberDescriptor = services.Single(sd => sd.ServiceType == typeof(ITransportSubscriber));
		subscriberDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterGrpcChannelAsSingletonLifetime()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001");

		// Assert
		var channelDescriptor = services.Single(sd => sd.ServiceType == typeof(GrpcChannel));
		channelDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ConfigureOptionsWithDefaultValues()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
			// Leave all other options at defaults
		});

		// Act
		var sp = services.BuildServiceProvider();
		var opts = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;

		// Assert — verify defaults are preserved when only ServerAddress is set
		opts.DeadlineSeconds.ShouldBe(30);
		opts.MaxSendMessageSize.ShouldBeNull();
		opts.MaxReceiveMessageSize.ShouldBeNull();
		opts.Destination.ShouldBe("grpc-default");
	}
}
