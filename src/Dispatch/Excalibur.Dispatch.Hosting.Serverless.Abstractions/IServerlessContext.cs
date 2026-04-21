// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Platform-agnostic context for serverless function execution.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core execution context properties needed by most consumers.
/// Platform-specific details (function version, memory limits, cloud provider info) are available via
/// <see cref="IServerlessPlatformDetails"/>, which context implementations may also implement.
/// Use <see cref="GetService"/> to access optional capabilities like <see cref="IServerlessPlatformDetails"/>.
/// </para>
/// </remarks>
public interface IServerlessContext : IServerlessExecutionContext, IServerlessPlatformContext, IDisposable
{
	/// <summary>
	/// Gets a service of the specified type from this context.
	/// </summary>
	/// <param name="serviceType"> The type of service to retrieve (e.g. <see cref="IServerlessPlatformDetails"/>). </param>
	/// <returns> The service instance, or <see langword="null"/> if the service is not supported. </returns>
	object? GetService(Type serviceType);
}
