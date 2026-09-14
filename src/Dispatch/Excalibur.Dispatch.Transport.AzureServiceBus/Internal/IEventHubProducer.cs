// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Excalibur.Dispatch.Transport.AzureServiceBus.Internal;

/// <summary>
/// The Event Hubs send operations this transport depends on, owned by this framework rather than by the
/// Azure SDK.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists so the publish path can be substituted without faking a vendor type.</b> A dynamic
/// proxy over an SDK class works only while that class stays open: a patch-level SDK refresh that seals a
/// type, or resolves a member differently, stops the proxy intercepting — and the call then reaches the
/// live client while the suite still reports green. That failure is silent in the worst possible way,
/// because the test that was supposed to isolate the SDK is the thing that stopped doing it. An interface
/// this framework declares cannot be changed out from under us by a dependency bump.
/// </para>
/// <para>
/// <b>Deliberately narrow: it is the send path and nothing else.</b> It carries exactly the members the
/// bus calls, so it stays a seam rather than becoming a re-declaration of
/// <see cref="EventHubProducerClient"/>. Anything the transport does not use does not belong here.
/// </para>
/// <para>
/// <see cref="EventDataBatch"/> still crosses this boundary because the SDK's size accounting lives
/// inside it and cannot be reproduced faithfully. It is a data-shaped type constructed through the SDK's
/// own supported model factory, never a dynamic proxy, so it does not carry the interception fragility
/// this seam exists to remove.
/// </para>
/// </remarks>
internal interface IEventHubProducer : IAsyncDisposable
{
	/// <summary>
	/// Creates an empty batch sized by the service.
	/// </summary>
	/// <param name="cancellationToken">A token to observe while creating the batch.</param>
	/// <returns>A batch that events are added to before sending.</returns>
	ValueTask<EventDataBatch> CreateBatchAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Publishes a batch to the Event Hub.
	/// </summary>
	/// <param name="batch">The batch to publish.</param>
	/// <param name="cancellationToken">A token to observe while publishing.</param>
	/// <returns>A task that completes when the service has accepted the batch.</returns>
	Task SendAsync(EventDataBatch batch, CancellationToken cancellationToken);
}
