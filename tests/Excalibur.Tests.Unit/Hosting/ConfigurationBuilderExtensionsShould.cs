using Excalibur.Hosting;

using FakeItEasy;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

using Shouldly;

namespace Excalibur.Tests.Unit.Hosting;

public class ConfigurationBuilderExtensionsShould : IDisposable
{
	private readonly Dictionary<string, string> _originalEnvironmentVariables = new();
	private bool _disposed;

	// Keep track of environment variables to restore them after tests
	public ConfigurationBuilderExtensionsShould()
	{
		// Save original environment variables
		foreach (var key in new[] { "RL_APP_CONTEXT", "RL_APP_NAME", "RL_CLUSTER_NAME" })
		{
			_originalEnvironmentVariables[key] = Environment.GetEnvironmentVariable(key);
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void AddEnvironmentVariablesWithoutParameters()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		Environment.SetEnvironmentVariable("RL_APP_NAME", "excalibur-app");

		// Act
		var result = configBuilder.AddParameterStoreSettings();

		// Assert
		result.ShouldBe(configBuilder);
		configBuilder.Sources.OfType<EnvironmentVariablesConfigurationSource>().ShouldNotBeEmpty();
	}

	[Fact]
	public void AddJsonFileWhenLocalContextIsProvided()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		var appName = "excalibur-app";
		var localContext = "development";
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "local");

		// Act
		_ = configBuilder.AddParameterStoreSettings(appName, localContext);

		// Assert
		var jsonSource = configBuilder.Sources
			.OfType<JsonConfigurationSource>()
			.FirstOrDefault(s => s.Path == $"appsettings.{localContext}.json");

		jsonSource.ShouldNotBeNull();
		jsonSource.Optional.ShouldBeTrue(); // if optional was passed as true
	}

	[Fact]
	public void NotAddJsonFileWhenLocalContextIsEmpty()
	{
		// Arrange
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "local");
		var configBuilder = new ConfigurationBuilder();

		// Act
		var result = configBuilder.AddParameterStoreSettings("excalibur-app", null);

		// Assert
		result.ShouldBe(configBuilder);
		configBuilder.Sources.OfType<JsonConfigurationSource>().ShouldBeEmpty();
		configBuilder.Sources.OfType<EnvironmentVariablesConfigurationSource>().ShouldNotBeEmpty();
	}

	[Fact]
	public void AddEnvironmentVariablesShouldBeIncluded()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "local");

		// Act
		_ = configBuilder.AddParameterStoreSettings("some-app");

		// Assert
		var envSource = configBuilder.Sources
			.OfType<EnvironmentVariablesConfigurationSource>()
			.FirstOrDefault();

		envSource.ShouldNotBeNull();
	}

	[Fact]
	public void AddSystemsManagerPathsWhenRemote()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "remote");
		Environment.SetEnvironmentVariable("RL_CLUSTER_NAME", "dev-cluster");

		// Act
		_ = configBuilder.AddParameterStoreSettings("excalibur");

		// Assert
		var sources = configBuilder.Sources
			.Where(s => s.GetType().Name.Contains("SystemsManager", StringComparison.InvariantCultureIgnoreCase))
			.ToList();

		sources.Count.ShouldBeGreaterThanOrEqualTo(2); // common and app-specific paths
	}

	[Fact]
	public void AddParameterStoreSettingsWithApplicationNameOnly()
	{
		// Arrange
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "local");
		var configBuilder = new ConfigurationBuilder();

		// Act
		var result = configBuilder.AddParameterStoreSettings("excalibur-app");

		// Assert
		result.ShouldBe(configBuilder);
		configBuilder.Sources.OfType<EnvironmentVariablesConfigurationSource>().ShouldNotBeEmpty();
	}

	[Fact]
	public void AddSystemsManagerWhenContextIsRemote()
	{
		// Arrange
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "remote");
		var configBuilder = new ConfigurationBuilder();

		// Act
		var result = configBuilder.AddParameterStoreSettings("excalibur-app", "dev");

		// Assert
		result.ShouldBe(configBuilder);
		configBuilder.Sources.Count(s => s.GetType().Name.Contains("SystemsManager", StringComparison.InvariantCultureIgnoreCase))
			.ShouldBe(2);
	}

	[Fact]
	public void HandleAllOverloads()
	{
		// Arrange
		Environment.SetEnvironmentVariable("RL_APP_NAME", "excalibur-app");
		Environment.SetEnvironmentVariable("RL_APP_CONTEXT", "local");

		var configBuilder = new ConfigurationBuilder();

		// Act
		var result1 = configBuilder.AddParameterStoreSettings();
		var result2 = configBuilder.AddParameterStoreSettings("test-app");
		var result3 = configBuilder.AddParameterStoreSettings("test-app", "dev");

		// Assert
		result1.ShouldBe(configBuilder);
		result2.ShouldBe(configBuilder);
		result3.ShouldBe(configBuilder);

		configBuilder.Sources.OfType<EnvironmentVariablesConfigurationSource>().ShouldNotBeEmpty();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			foreach (var kvp in _originalEnvironmentVariables)
			{
				Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
			}
		}

		_disposed = true;
	}
}
