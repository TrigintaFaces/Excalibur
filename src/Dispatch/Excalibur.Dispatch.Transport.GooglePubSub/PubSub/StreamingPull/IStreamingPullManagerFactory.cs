// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Factory for creating streaming pull managers.
/// </summary>
public interface IStreamingPullManagerFactory
{
	/// <summary>
	/// Creates a new streaming pull manager.
	/// </summary>
	/// <param name="subscriptionName"> The subscription name. </param>
	/// <param name="processor"> The message processor delegate. </param>
	/// <returns> A new streaming pull manager instance. </returns>
	StreamingPullManager CreateManager(
		SubscriptionName subscriptionName,
		MessageStreamProcessor.MessageProcessor processor);
}
