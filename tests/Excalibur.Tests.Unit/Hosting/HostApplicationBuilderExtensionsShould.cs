using System.Reflection;

using Excalibur.Core;
using Excalibur.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Serilog;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting;

public class HostApplicationBuilderExtensionsShould : IDisposable
{
	private readonly HostApplicationBuilder _realBuilder;
	private bool _disposed;

	public HostApplicationBuilderExtensionsShould()
	{
		_realBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
		{
			EnvironmentName = "Testing",
			ApplicationName = "TestApp",
			Configuration = new ConfigurationManager
			{
				["ApplicationContext:ApplicationName"] = "TestApp",
				["ApplicationContext:Environment"] = "Testing"
			}
		});
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenBuilderIsNullConfigureApplicationContext()
	{
		IHostApplicationBuilder builder = null!;
		var ex = Should.Throw<ArgumentNullException>(() => builder.ConfigureApplicationContext());
		ex.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void ReturnBuilderForChainingConfigureApplicationContext()
	{
		var result = _realBuilder.ConfigureApplicationContext();
		result.ShouldBeSameAs(_realBuilder);
	}

	[Fact]
	public void ReturnBuilderForChainingConfigureExcaliburLogging()
	{
		var result = _realBuilder.ConfigureExcaliburLogging();
		result.ShouldBeSameAs(_realBuilder);
	}

	[Fact]
	public void ReturnBuilderForChainingConfigureExcaliburMetrics()
	{
		var result = _realBuilder.ConfigureExcaliburMetrics();
		result.ShouldBeSameAs(_realBuilder);
	}

	[Fact]
	public void ReturnBuilderForChainingConfigureExcaliburTracing()
	{
		var result = _realBuilder.ConfigureExcaliburTracing();
		result.ShouldBeSameAs(_realBuilder);
	}

	[Fact]
	public void ConfigureApplicationContextWithDefaults()
	{
		_realBuilder.ConfigureApplicationContext();

		ApplicationContext.ApplicationName.ShouldBe("TestApp");
		ApplicationContext.ApplicationSystemName.ShouldBe("test-app");
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			var resetMethod = typeof(ApplicationContext).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic);
			_ = (resetMethod?.Invoke(null, null));
			Log.CloseAndFlush();
		}

		_disposed = true;
	}
}
