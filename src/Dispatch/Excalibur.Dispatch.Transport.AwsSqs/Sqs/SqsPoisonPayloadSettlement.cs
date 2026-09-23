// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// The single decision for how an unprocessable (oversized) poison payload is settled on the SQS receive
/// surface, stated in one place so the two outcomes cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the Pub/Sub policy of the same shape deliberately: the question is identical on both
/// providers, and the answer must not depend on which transport a consumer happened to pick.
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Redrive policy declared</b> → <b>leave the message alone</b>. SQS's own redrive moves it to the
/// dead-letter queue once <c>maxReceiveCount</c> is exceeded, preserving a copy for investigation. That
/// counter only advances when the message becomes visible again, so the correct action is to do nothing
/// and let the visibility timeout expire.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>No redrive policy</b> → <b>delete</b> (drop). Leaving it would redeliver it forever with nowhere
/// to go — a poison loop that stalls the queue. Dropping is the fail-safe: subscription liveness over
/// retaining a message that can never succeed and has no destination.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Deleting is NOT redrive, and conflating the two is what this type exists to prevent.</b>
/// <c>DeleteMessage</c> removes the message from SQS outright; redrive is driven by the receive count on
/// a message that returns to the queue. A receiver that deletes an oversized message has destroyed it
/// even when a dead-letter queue was configured and would have caught it — the one outcome a consumer
/// who configured a DLQ is entitled to assume cannot happen.
/// </para>
/// </remarks>
internal static class SqsPoisonPayloadSettlement
{
	/// <summary>
	/// Decides whether an unprocessable poison payload must be deleted, or left for SQS redrive.
	/// </summary>
	/// <param name="hasDeadLetterQueue">
	/// <see langword="true"/> when a dead-letter queue is configured for this queue, so redrive has
	/// somewhere to put the message.
	/// </param>
	/// <returns>
	/// <see langword="true"/> to <b>delete</b> the poison payload (no dead-letter queue exists, so the
	/// alternative is an endless redelivery loop); <see langword="false"/> to <b>leave it</b> for redrive.
	/// </returns>
	public static bool ShouldDelete(bool hasDeadLetterQueue) => !hasDeadLetterQueue;
}
