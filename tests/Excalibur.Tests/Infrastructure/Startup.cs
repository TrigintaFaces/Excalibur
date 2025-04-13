using System.Data;

using Excalibur.Application;
using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Core.Extensions;
using Excalibur.Data;
using Excalibur.Domain;
using Excalibur.Hosting;
using Excalibur.Tests.Fakes;
using Excalibur.Tests.Fakes.A3;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;

namespace Excalibur.Tests.Infrastructure;

internal static class Startup
{
	public static WebApplicationBuilder CreateHostBuilder(params ILogEventSink[] additionalLogSinks)
	{
		var builder = WebApplication.CreateBuilder([]);

		_ = builder
			.ConfigureApplicationContext()
			.ConfigureExcaliburLogging(additionalLogSinks);

		return builder;
	}

	public static WebApplicationBuilder BaseConfigureHostServices(
		this WebApplicationBuilder builder,
		IDatabaseContainerFixture fixture,
		Action<WebApplicationBuilder, IDatabaseContainerFixture> registry)
	{
		// things the Host normally sets up
		builder.Services.AddHostServices(builder.Configuration, fixture);

		registry.Invoke(builder, fixture);

		return builder;
	}

	public static IServiceProvider ConfigurePersistenceOnlyServices(
		IDatabaseContainerFixture fixture,
		Action<IServiceCollection, IConfiguration> registry)
	{
		Environment.SetEnvironmentVariable("APP_ENTRYPOINT", typeof(Startup).Namespace);

		var configuration = new ConfigurationBuilder()
			.AddParameterStoreSettings(Environment.GetEnvironmentVariable("RL_APP_NAME"), "test")
			.AddInMemoryCollection(new Dictionary<string, string>
			{
				{ "DataProcessing:TableName", "DataProcessor.DataTaskRequests" },
				{ "DataProcessing:MaxAttempts", "3" },
				{ "OutboxConfiguration:TableName", "Outbox.Outbox" },
				{ "OutboxConfiguration:DeadLetterTableName", "Outbox.DeadLetterOutbox" },
				{ "OutboxConfiguration:MaxAttempts", "3" },
				{ "OutboxConfiguration:DispatcherTimeoutMilliseconds", "60000" },
				{ "OutboxConfiguration:QueueSize", "100" },
				{ "OutboxConfiguration:ProducerBatchSize", "10" },
				{ "OutboxConfiguration:ConsumerBatchSize", "5" }
			}!)
			.Build();

		ApplicationContext.Init(configuration.GetApplicationContextConfiguration());

		var services = new ServiceCollection();

		AddSeriLog(services);

		registry?.Invoke(services, configuration);

		// things the Host normally sets up
		AddPersistenceOnlyServices(fixture, services, configuration);

		return services.BuildServiceProvider();
	}

	private static void AddHostServices(this IServiceCollection services, IConfigurationRoot configuration,
		IDatabaseContainerFixture fixture) =>
		_ = services
			.AddSingleton<IConfiguration>(_ => configuration)
			.AddSingleton(typeof(ILogger<>), typeof(Logger<>))
			.AddExcaliburDataServices(configuration, typeof(AssemblyMarker).Assembly)
			.AddExcaliburApplicationServices(typeof(AssemblyMarker).Assembly)
			.AddScoped<IDbConnection>(sp => fixture.CreateDbConnection())
			.AddScoped<IDomainDb, TestDb>()
			.AddScoped<TestDb>()
			.AddTransient<ICorrelationId, CorrelationId>()
			.AddTransient<IETag, ETag>()
			.AddTransient<ITenantId>(_ => new TenantId(WellKnownId.TestTenant))
			.AddTransient<IClientAddress, ClientAddress>(_ => new ClientAddress("127.0.0.1"));

	private static void AddPersistenceOnlyServices(IDatabaseContainerFixture fixture, IServiceCollection services,
		IConfigurationRoot configuration) =>
		_ = services
			.AddSingleton<IConfiguration>(_ => configuration)
			.AddSingleton<IHostApplicationLifetime, TestAppLifetime>()
			.AddSingleton(typeof(ILogger<>), typeof(Logger<>))
			.AddExcaliburDataServices(configuration, typeof(AssemblyMarker).Assembly)
			.AddExcaliburApplicationServices(typeof(AssemblyMarker).Assembly)
			.AddScoped<IDbConnection>(sp => fixture.CreateDbConnection())
			.AddScoped<IDomainDb, TestDb>()
			.AddScoped<TestDb>()
			.AddTransient<ICorrelationId, CorrelationId>()
			.AddTransient<IETag, ETag>()
			.AddTransient<ITenantId>(_ => new TenantId(WellKnownId.TestTenant))
			.AddTransient<IClientAddress, ClientAddress>(_ => new ClientAddress("127.0.0.1"))
			.AddTransient(_ => AccessTokenFakes.AccessToken)
			.AddTransient(_ => MetricsFakes.Metrics);

	private static void AddSeriLog(IServiceCollection services)
	{
		Log.Logger = new LoggerConfiguration().CreateLogger();

		_ = services.AddLogging(builder =>
		{
			_ = builder.ClearProviders();
			_ = builder.AddSerilog(Log.Logger, true);
		});

		_ = services.AddSerilog(Log.Logger, true);
	}
}
