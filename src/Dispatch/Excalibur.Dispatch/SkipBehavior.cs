// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Defines the behavior when message processing is skipped due to filtering, conditions, or validation failures. Controls how the messaging
/// system responds to scenarios where normal processing cannot or should not proceed.
/// </summary>
/// <remarks>
/// <para>
/// Skip behaviors provide fine-grained control over messaging flow when messages cannot be processed through the normal pipeline. This
/// enables applications to handle edge cases, implement circuit breakers, and provide appropriate feedback for monitoring and debugging purposes.
/// </para>
/// <para>
/// The choice of skip behavior affects both the runtime behavior and observability characteristics of the messaging system, making it
/// important to select the appropriate option based on operational requirements and monitoring needs.
/// </para>
/// </remarks>
public enum SkipBehavior
{
	/// <summary>
	/// Skip processing silently without any logging or feedback. The message is discarded and no trace of the skip operation is recorded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This option provides the best performance as it avoids any logging overhead, but may make troubleshooting difficult in production
	/// environments where visibility into skipped messages is important.
	/// </para>
	/// <para> Use this option when message skipping is expected behavior and doesn't require monitoring or debugging support. </para>
	/// </remarks>
	Silent = 0,

	/// <summary>
	/// Skip processing but log the skip event for monitoring and debugging purposes. Provides visibility into skipped messages without
	/// affecting the processing result.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This option balances performance with observability by recording skip events in the application logs. The log entry typically
	/// includes message metadata, skip reason, and context information for troubleshooting.
	/// </para>
	/// <para> Recommended for production environments where monitoring skipped messages is important for operational visibility and debugging. </para>
	/// </remarks>
	LogOnly = 1,

	/// <summary>
	/// Skip processing and return an explicit skipped result to the caller. Enables the calling code to take specific action based on the
	/// skip event.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This option provides the most control by allowing calling code to implement custom logic when messages are skipped. The result
	/// typically includes information about why the message was skipped and any relevant context.
	/// </para>
	/// <para>
	/// Use this option when the application needs to respond differently to skipped messages, such as implementing retry logic, alternative
	/// processing paths, or user notifications.
	/// </para>
	/// </remarks>
	ReturnSkippedResult = 2,
}
