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
public interface IServerlessContext : IDisposable
{
	/// <summary>
	/// Gets the unique identifier for this function invocation.
	/// </summary>
	/// <value>The unique identifier for this function invocation.</value>
	string RequestId { get; }

	/// <summary>
	/// Gets the name of the executing function.
	/// </summary>
	/// <value>The name of the executing function.</value>
	string FunctionName { get; }

	/// <summary>
	/// Gets the logger for this context.
	/// </summary>
	/// <value>The logger for this context.</value>
	ILogger Logger { get; }

	/// <summary>
	/// Gets the execution deadline.
	/// </summary>
	/// <value>The execution deadline.</value>
	DateTimeOffset ExecutionDeadline { get; }

	/// <summary>
	/// Gets the elapsed execution time.
	/// </summary>
	/// <value>The elapsed execution time.</value>
	TimeSpan ElapsedTime { get; }

	/// <summary>
	/// Gets the remaining execution time.
	/// </summary>
	/// <value>The remaining execution time.</value>
	TimeSpan RemainingTime { get; }

	/// <summary>
	/// Gets custom properties for context extension.
	/// </summary>
	/// <value>The custom properties for context extension.</value>
	IDictionary<string, object> Properties { get; }

	/// <summary>
	/// Gets the trace context for distributed tracing.
	/// </summary>
	/// <value>The trace context for distributed tracing.</value>
	TraceContext? TraceContext { get; }

	/// <summary>
	/// Gets the serverless platform.
	/// </summary>
	/// <value>The serverless platform.</value>
	ServerlessPlatform Platform { get; }

	/// <summary>
	/// Gets the original platform-specific context object.
	/// </summary>
	/// <value>The original platform-specific context object.</value>
	object PlatformContext { get; }

	/// <summary>
	/// Gets a service of the specified type from this context.
	/// </summary>
	/// <param name="serviceType"> The type of service to retrieve (e.g. <see cref="IServerlessPlatformDetails"/>). </param>
	/// <returns> The service instance, or <see langword="null"/> if the service is not supported. </returns>
	object? GetService(Type serviceType);
}
