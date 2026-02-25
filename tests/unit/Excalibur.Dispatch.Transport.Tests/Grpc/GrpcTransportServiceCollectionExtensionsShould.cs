// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcTransportServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterGrpcTransportServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var optionsInstance = sp.GetRequiredService<IOptions<GrpcTransportOptions>>();
		optionsInstance.Value.ServerAddress.ShouldBe("https://localhost:5001");
	}

	[Fact]
	public void RegisterTransportSender()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
		});

		// Assert - verify service descriptor was registered
		services.ShouldContain(sd => sd.ServiceType == typeof(ITransportSender));
	}

	[Fact]
	public void RegisterTransportReceiver()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITransportReceiver));
	}

	[Fact]
	public void RegisterTransportSubscriber()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITransportSubscriber));
	}

	[Fact]
	public void ThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddGrpcTransport(options => options.ServerAddress = "https://localhost:5001"));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddGrpcTransport(null!));
	}

	[Fact]
	public void ConfigureOptionsWithValidateDataAnnotations()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
			options.DeadlineSeconds = 60;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;
		options.DeadlineSeconds.ShouldBe(60);
	}

	[Fact]
	public void ConfigureMaxMessageSizeOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options =>
		{
			options.ServerAddress = "https://localhost:5001";
			options.MaxSendMessageSize = 4_194_304;
			options.MaxReceiveMessageSize = 8_388_608;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<GrpcTransportOptions>>().Value;
		options.MaxSendMessageSize.ShouldBe(4_194_304);
		options.MaxReceiveMessageSize.ShouldBe(8_388_608);
	}

	[Fact]
	public void NotAddDuplicateServicesWhenCalledTwice()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddGrpcTransport(options => options.ServerAddress = "https://server1:5001");
		services.AddGrpcTransport(options => options.ServerAddress = "https://server2:5001");

		// Assert - TryAddSingleton should prevent duplicates
		services.Count(sd => sd.ServiceType == typeof(ITransportSender)).ShouldBe(1);
		services.Count(sd => sd.ServiceType == typeof(ITransportReceiver)).ShouldBe(1);
		services.Count(sd => sd.ServiceType == typeof(ITransportSubscriber)).ShouldBe(1);
	}
}
