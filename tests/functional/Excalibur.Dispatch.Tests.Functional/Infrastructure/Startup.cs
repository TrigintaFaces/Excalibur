// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.AspNetCore.Builder;

using Serilog;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Infrastructure;

internal static class Startup
{
	public static WebApplicationBuilder CreateHostBuilder()
	{
		var builder = WebApplication.CreateBuilder([]);

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

	public static IServiceProvider ConfigurePersistenceOnlyServices()
	{
		var services = new ServiceCollection();

		AddSeriLog(services);

		return services.BuildServiceProvider();
	}

	private static void AddHostServices(this IServiceCollection services, IConfigurationRoot configuration,
		IDatabaseContainerFixture fixture) => _ = services
		.AddSingleton<IConfiguration>(_ => configuration)
		.AddSingleton(typeof(ILogger<>), typeof(Logger<>))
		.AddScoped(sp => fixture.CreateDbConnection())
		.AddTransient<ICorrelationId, CorrelationId>();

	private static void AddPersistenceOnlyServices(IDatabaseContainerFixture fixture, IServiceCollection services,
		IConfigurationRoot configuration) => _ = services
		.AddSingleton<IConfiguration>(_ => configuration)
		.AddScoped(sp => fixture.CreateDbConnection())
		.AddTransient<ICorrelationId, CorrelationId>();

	private static void AddSeriLog(IServiceCollection services)
	{
		Log.Logger = new LoggerConfiguration().CreateLogger();

		_ = services.AddLogging(static builder => _ = builder.ClearProviders());
	}
}
