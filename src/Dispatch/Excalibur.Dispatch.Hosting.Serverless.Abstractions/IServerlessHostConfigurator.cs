// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Provides configuration capabilities for a serverless host provider.
/// </summary>
/// <remarks>
/// <para>
/// This interface is separated from <see cref="IServerlessHostProvider"/> following the
/// Interface Segregation Principle (ISP). Not all consumers need to configure the host;
/// many only need to create contexts and execute functions.
/// </para>
/// <para>
/// Implementations that support both runtime and configuration should implement
/// both <see cref="IServerlessHostProvider"/> and <see cref="IServerlessHostConfigurator"/>.
/// </para>
/// </remarks>
public interface IServerlessHostConfigurator
{
	/// <summary>
	/// Configures services for this serverless platform.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="options"> Configuration options for the provider. </param>
	void ConfigureServices(IServiceCollection services, ServerlessHostOptions options);

	/// <summary>
	/// Configures the host for this serverless platform.
	/// </summary>
	/// <param name="hostBuilder"> The host builder to configure. </param>
	/// <param name="options"> Configuration options for the provider. </param>
	void ConfigureHost(IHostBuilder hostBuilder, ServerlessHostOptions options);
}
