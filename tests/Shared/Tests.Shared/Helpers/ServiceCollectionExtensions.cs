using Xunit.Abstractions;

namespace Tests.Shared.Helpers;

/// <summary>
/// Extension methods for configuring test services.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds test logging that outputs to xUnit test output.
	/// </summary>
	public static IServiceCollection AddTestLogging(this IServiceCollection services, ITestOutputHelper output)
	{
		_ = services.AddLogging(builder =>
		{
			_ = builder.ClearProviders();
			_ = builder.SetMinimumLevel(LogLevel.Debug);
			_ = builder.Services.AddSingleton(output);
			_ = builder.Services.AddSingleton(typeof(ILogger<>), typeof(TestLogger<>));
		});

		return services;
	}
}
