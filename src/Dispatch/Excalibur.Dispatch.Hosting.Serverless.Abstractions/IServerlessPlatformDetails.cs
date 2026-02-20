// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Provides platform-specific details for a serverless function execution context.
/// </summary>
/// <remarks>
/// <para>
/// This interface is separated from <see cref="IServerlessContext"/> following the
/// Interface Segregation Principle (ISP). Not all consumers need platform-specific
/// details such as function version, memory limits, or cloud provider information.
/// Many only need the core execution context (request ID, function name, deadlines).
/// </para>
/// <para>
/// Implementations that support both core context and platform details should implement
/// both <see cref="IServerlessContext"/> and <see cref="IServerlessPlatformDetails"/>.
/// The <see cref="ServerlessContextBase"/> base class implements both interfaces.
/// </para>
/// </remarks>
public interface IServerlessPlatformDetails
{
	/// <summary>
	/// Gets the version or alias of the function being executed.
	/// </summary>
	/// <value>The version or alias of the function being executed.</value>
	string FunctionVersion { get; }

	/// <summary>
	/// Gets the ARN or resource identifier of the function.
	/// </summary>
	/// <value>The ARN or resource identifier of the function.</value>
	string InvokedFunctionArn { get; }

	/// <summary>
	/// Gets the memory limit allocated to the function in MB.
	/// </summary>
	/// <value>The memory limit allocated to the function in MB.</value>
	int MemoryLimitInMB { get; }

	/// <summary>
	/// Gets the log group name.
	/// </summary>
	/// <value>The log group name.</value>
	string LogGroupName { get; }

	/// <summary>
	/// Gets the log stream name.
	/// </summary>
	/// <value>The log stream name.</value>
	string LogStreamName { get; }

	/// <summary>
	/// Gets the cloud provider name.
	/// </summary>
	/// <value>The cloud provider name.</value>
	string CloudProvider { get; }

	/// <summary>
	/// Gets the region where the function is running.
	/// </summary>
	/// <value>The region where the function is running.</value>
	string Region { get; }

	/// <summary>
	/// Gets the account ID.
	/// </summary>
	/// <value>The account ID.</value>
	string AccountId { get; }
}
