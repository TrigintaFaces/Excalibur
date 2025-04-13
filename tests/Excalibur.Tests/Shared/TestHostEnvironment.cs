using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Excalibur.Tests.Shared;

public static class TestHostEnvironmentExtensions
{
	public static IServiceCollection AddTestHostEnvironment(this IServiceCollection services, string appName = "TestApp")
	{
		var env = new HostingEnvironment
		{
			ApplicationName = appName,
			EnvironmentName = Environments.Development,
			ContentRootPath = AppContext.BaseDirectory
		};

		return services.AddSingleton<IHostEnvironment>(env);
	}
}

public static class TestHostEnvironmentFakes
{
	public static IHostEnvironment CreateFakeEnvironment(string appName = "TestApp", bool isDevelopment = true)
	{
		var env = A.Fake<IHostEnvironment>();
		_ = A.CallTo(() => env.ApplicationName).Returns(appName);
		_ = A.CallTo(() => env.IsDevelopment()).Returns(isDevelopment);
		_ = A.CallTo(() => env.EnvironmentName).Returns(isDevelopment ? Environments.Development : Environments.Production);

		return env;
	}
}
