using Microsoft.Extensions.Configuration;

namespace Excalibur.Tests.Shared;

public static class ConfigurationTestHelper
{
	public static IConfigurationRoot BuildConfiguration(Dictionary<string, string?> values)
	{
		ArgumentNullException.ThrowIfNull(values);

		return new ConfigurationBuilder()
			.AddInMemoryCollection(values)
			.Build();
	}

	public static T BindSettings<T>(Dictionary<string, string?> values, string section) where T : class, new()
	{
		ArgumentNullException.ThrowIfNull(values);
		ArgumentException.ThrowIfNullOrWhiteSpace(section);

		var config = BuildConfiguration(values);

		var settings = new T();
		config.GetSection(section).Bind(settings);
		return settings;
	}
}
