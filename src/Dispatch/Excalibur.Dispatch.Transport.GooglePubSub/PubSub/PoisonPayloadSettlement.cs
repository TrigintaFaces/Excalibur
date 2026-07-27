// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// The single, shared decision for how an unprocessable (oversized) poison payload is settled on both
/// Pub/Sub receive surfaces (streaming subscriber and pull receiver), so the two cannot diverge.
/// </summary>
/// <remarks>
/// A poison payload can never be processed. How it is settled depends on whether a native dead-letter
/// policy is declared:
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Dead-letter policy declared</b> → <b>Nack</b>. Pub/Sub routes the message to the dead-letter topic
/// after <c>maxDeliveryAttempts</c>, preserving a diagnostic copy for investigation.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>No dead-letter policy</b> → <b>Ack</b> (drop). A permanent Nack with no dead-letter topic to catch
/// the message would redeliver it forever — a poison loop that blocks the subscription. Dropping the
/// unprocessable payload is the fail-safe: liveness of the subscription over retaining a message that
/// can never succeed and has nowhere to go.
/// </description>
/// </item>
/// </list>
/// Both surfaces route their oversized-poison settlement through <see cref="ShouldDeadLetter"/> so the
/// policy is single-sourced (the divergence between the two surfaces this consolidates was the original
/// defect).
/// </remarks>
internal static class PoisonPayloadSettlement
{
	/// <summary>
	/// Decides whether an unprocessable poison payload should be dead-lettered (Nack) or dropped (Ack).
	/// </summary>
	/// <param name="hasDeadLetterPolicy">
	/// <see langword="true"/> when a native dead-letter topic is configured for the subscription.
	/// </param>
	/// <returns>
	/// <see langword="true"/> to <b>Nack</b> (route to the dead-letter topic); <see langword="false"/> to
	/// <b>Ack</b>-drop the poison payload (breaking the redelivery loop when there is no dead-letter topic).
	/// </returns>
	public static bool ShouldDeadLetter(bool hasDeadLetterPolicy) => hasDeadLetterPolicy;
}
