// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Platform-specific context for a serverless function invocation.
/// </summary>
public interface IServerlessPlatformContext
{
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
}
