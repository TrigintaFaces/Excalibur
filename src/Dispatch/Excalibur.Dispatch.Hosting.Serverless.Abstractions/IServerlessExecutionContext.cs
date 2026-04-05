// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Execution-specific context for a serverless function invocation.
/// </summary>
public interface IServerlessExecutionContext
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
}
