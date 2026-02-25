using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigurationTypesShould
{
	[Fact]
	public void CompressionType_HaveExpectedValues()
	{
		CompressionType.None.ShouldBe((CompressionType)0);
		CompressionType.Gzip.ShouldBe((CompressionType)1);
		CompressionType.Deflate.ShouldBe((CompressionType)2);
		CompressionType.Lz4.ShouldBe((CompressionType)3);
		CompressionType.Brotli.ShouldBe((CompressionType)4);
	}

	[Fact]
	public void TransportConfiguration_HaveDefaults()
	{
		var config = new TransportConfiguration();

		config.Name.ShouldBe(string.Empty);
		config.Enabled.ShouldBeTrue();
		config.Priority.ShouldBe(100);
	}

	[Fact]
	public void TransportConfiguration_AllowSettingProperties()
	{
		var config = new TransportConfiguration
		{
			Name = "rabbitmq",
			Enabled = false,
			Priority = 10,
		};

		config.Name.ShouldBe("rabbitmq");
		config.Enabled.ShouldBeFalse();
		config.Priority.ShouldBe(10);
	}

	[Fact]
	public void MiddlewareRegistration_StoreConstructorValues()
	{
		var reg = new MiddlewareRegistration(
			typeof(object),
			DispatchMiddlewareStage.PreProcessing,
			50);

		reg.MiddlewareType.ShouldBe(typeof(object));
		reg.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
		reg.Order.ShouldBe(50);
	}

	[Fact]
	public void MiddlewareRegistration_DefaultOrder()
	{
		var reg = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PostProcessing);

		reg.Order.ShouldBe(100);
	}

	[Fact]
	public void MiddlewareRegistration_ThrowOnNullType()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MiddlewareRegistration(null!, DispatchMiddlewareStage.PreProcessing));
	}

	[Fact]
	public void MiddlewareRegistration_AcceptConfigureOptions()
	{
		Action<IServiceCollection> configure = _ => { };
		var reg = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing, configureOptions: configure);

		reg.ConfigureOptions.ShouldBe(configure);
	}

	[Fact]
	public void MiddlewareRegistration_IsEnabledDefaultTrue()
	{
		var reg = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing);

		reg.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public void MiddlewareRegistration_AllowDisabling()
	{
		var reg = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing);
		reg.IsEnabled = false;

		reg.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void MiddlewareRegistration_AllowChangingOrder()
	{
		var reg = new MiddlewareRegistration(typeof(object), DispatchMiddlewareStage.PreProcessing);
		reg.Order = 200;

		reg.Order.ShouldBe(200);
	}
}
