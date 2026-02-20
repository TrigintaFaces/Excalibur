// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.EventBridge;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsEventBridgeTransportBuilderShould
{
	[Fact]
	public void SetEventBusName()
	{
		// Arrange
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		// Act
		var result = builder.EventBusName("my-bus");

		// Assert
		result.ShouldBe(builder);
		options.EventBusName.ShouldBe("my-bus");
	}

	[Fact]
	public void ThrowWhenEventBusNameIsEmpty()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		Should.Throw<ArgumentException>(() => builder.EventBusName(""));
	}

	[Fact]
	public void SetRegion()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.Region("us-west-2");

		options.Region.ShouldBe("us-west-2");
	}

	[Fact]
	public void ThrowWhenRegionIsEmpty()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		Should.Throw<ArgumentException>(() => builder.Region(""));
	}

	[Fact]
	public void SetDefaultSource()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.DefaultSource("com.test.app");

		options.DefaultSource.ShouldBe("com.test.app");
	}

	[Fact]
	public void SetDefaultDetailType()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.DefaultDetailType("MyEvent");

		options.DefaultDetailType.ShouldBe("MyEvent");
	}

	[Fact]
	public void EnableArchiving()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.EnableArchiving(retentionDays: 30, archiveName: "my-archive");

		options.EnableArchiving.ShouldBeTrue();
		options.ArchiveRetentionDays.ShouldBe(30);
		options.ArchiveName.ShouldBe("my-archive");
	}

	[Fact]
	public void EnableArchivingWithDefaults()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.EnableArchiving();

		options.EnableArchiving.ShouldBeTrue();
		options.ArchiveRetentionDays.ShouldBe(7);
		options.ArchiveName.ShouldBeNull();
	}

	[Fact]
	public void ConfigureOptionsViaAction()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.ConfigureOptions(o => o.Name = "custom");

		options.Name.ShouldBe("custom");
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		Should.Throw<ArgumentNullException>(() => builder.ConfigureOptions(null!));
	}

	[Fact]
	public void MapDetailType()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		builder.MapDetailType<string>("StringEvent");

		options.DetailTypeMappings.ShouldContainKey(typeof(string));
		options.DetailTypeMappings[typeof(string)].ShouldBe("StringEvent");
	}

	[Fact]
	public void ThrowWhenDetailTypeIsEmpty()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		Should.Throw<ArgumentException>(() => builder.MapDetailType<string>(""));
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var options = new AwsEventBridgeTransportOptions();
		var builder = CreateBuilder(options);

		var result = builder
			.EventBusName("my-bus")
			.Region("us-east-1")
			.DefaultSource("com.test")
			.DefaultDetailType("TestEvent")
			.EnableArchiving(14)
			.MapDetailType<string>("StringEvent");

		result.ShouldBe(builder);
		options.EventBusName.ShouldBe("my-bus");
		options.Region.ShouldBe("us-east-1");
	}

	private static IAwsEventBridgeTransportBuilder CreateBuilder(AwsEventBridgeTransportOptions options)
	{
		// Use the DI extension to get the builder indirectly —
		// since AwsEventBridgeTransportBuilder is internal, we create it through DI
		var services = new ServiceCollection();
		IAwsEventBridgeTransportBuilder? capturedBuilder = null;

		services.AddAwsEventBridgeTransport("test", b =>
		{
			capturedBuilder = b;
			// Builder configures options internally; let's directly configure
		});

		// Actually, the builder gets the transport options from AddAwsEventBridgeTransport,
		// not our options. Let's use a simpler approach: configure via lambda
		// Since the builder is internal, we test it through the public DI extension API
		// But for isolated unit testing, let's capture it from the DI method.
		// We'll just re-test through the extension method instead.

		// Workaround: create via reflection or just test through DI
		// Actually, let's test the builder through its public interface using the services method
		IAwsEventBridgeTransportBuilder? builder = null;
		services = new ServiceCollection();
		services.AddAwsEventBridgeTransport("test-builder", b => builder = b);

		// The builder already ran its configure action, so this isn't quite right.
		// Instead, test the builder behaviors through the extension method outcomes.
		// For proper testing, use a fresh options object by invoking the internal builder.
		var type = typeof(AwsEventBridgeTransportServiceCollectionExtensions).Assembly
			.GetTypes()
			.First(t => t.Name == "AwsEventBridgeTransportBuilder");
		return (IAwsEventBridgeTransportBuilder)Activator.CreateInstance(type, options)!;
	}
}
