// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// The single fall-through for a message type that declares no kind.
/// </summary>
/// <remarks>
/// Every site that classifies a message type routes its unclassified case through here, so the fall-through behaviour is
/// defined once rather than repeated. A message that declares no kind receives every kind, which means every middleware
/// applies to it: the type we know least about gets the most protection, not the least.
/// </remarks>
internal static class UnclassifiedMessage
{
	private const string SignalName = "dispatch.message.unclassified";

	/// <summary>
	/// Records that <paramref name="messageType"/> could not be classified and returns the kinds it is treated as.
	/// </summary>
	/// <param name="messageType"> The message type that declares no kind. </param>
	/// <returns> <see cref="MessageKinds.All"/> — every middleware applies. </returns>
	/// <remarks>
	/// Failing closed is silent, and silence is how the unprotected-message defect survived. Without a signal the fault
	/// surfaces downstream as an authorization failure, which names the wrong problem: the developer sees a rejected
	/// message and the actual cause is a missing interface. The signal names the type and the interfaces it may declare.
	/// <para>
	/// Emitted through <see cref="Activity"/> rather than a logger because classification is a static operation with no
	/// injected dependencies, and because this belongs to the trace of the dispatch that provoked it.
	/// </para>
	/// </remarks>
	internal static MessageKinds FailClosed(Type messageType)
	{
		var activity = Activity.Current;

		if (activity is not null)
		{
			activity.AddEvent(new ActivityEvent(
				SignalName,
				tags: new ActivityTagsCollection
				{
					{ "dispatch.message.type", messageType?.FullName },
					{ "dispatch.message.missing_interface", "IDispatchAction, IDispatchEvent, or IDispatchDocument" },
					{ "dispatch.message.applied_kinds", nameof(MessageKinds.All) },
				}));
		}

		return MessageKinds.All;
	}
}
