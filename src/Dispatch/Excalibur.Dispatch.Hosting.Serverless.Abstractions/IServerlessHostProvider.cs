// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Provides serverless hosting capabilities for a specific cloud platform.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core runtime capabilities of a serverless host provider.
/// Configuration capabilities (service and host setup) are available via
/// <see cref="IServerlessHostConfigurator"/>, which providers may also implement.
/// Use <see cref="GetService"/> to access optional capabilities like <see cref="IServerlessHostConfigurator"/>.
/// </para>
/// </remarks>
public interface IServerlessHostProvider
{
	/// <summary>
	/// Gets the platform this provider supports.
	/// </summary>
	/// <value>The platform this provider supports.</value>
	ServerlessPlatform Platform { get; }

	/// <summary>
	/// Gets a value indicating whether this provider is available in the current environment.
	/// </summary>
	/// <value><see langword="true"/> if this provider is available in the current environment; otherwise, <see langword="false"/>.</value>
	bool IsAvailable { get; }

	/// <summary>
	/// Creates a serverless context from the platform-specific context.
	/// </summary>
	/// <param name="platformContext"> The platform-specific context object. </param>
	/// <returns> A unified serverless context. </returns>
	IServerlessContext CreateContext(object platformContext);

	/// <summary>
	/// Handles the serverless function execution.
	/// </summary>
	/// <typeparam name="TInput"> The input type. </typeparam>
	/// <typeparam name="TOutput"> The output type. </typeparam>
	/// <param name="input"> The input data. </param>
	/// <param name="context"> The serverless context. </param>
	/// <param name="handler"> The message handler function. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The function result. </returns>
	Task<TOutput> ExecuteAsync<TInput, TOutput>(
		TInput input,
		IServerlessContext context,
		Func<TInput, IServerlessContext, CancellationToken, Task<TOutput>> handler,
		CancellationToken cancellationToken)
		where TInput : class
		where TOutput : class;

	/// <summary>
	/// Gets a service of the specified type from this provider.
	/// </summary>
	/// <param name="serviceType"> The type of service to retrieve (e.g. <see cref="IServerlessHostConfigurator"/>). </param>
	/// <returns> The service instance, or <see langword="null"/> if the service is not supported. </returns>
	object? GetService(Type serviceType);
}
