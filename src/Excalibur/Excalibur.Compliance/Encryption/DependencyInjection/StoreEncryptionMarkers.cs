// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Encryption.DependencyInjection;

/// <summary>
/// Marker registered by <c>AddInboxEncryption()</c> — and only when it actually decorated a registered inbox
/// store — to record that the inbox is wired for at-rest payload encryption under the configured context (not per data subject).
/// </summary>
/// <remarks>
/// The marker attests that decoration <em>happened</em>, never that the method was called: a marker recorded
/// unconditionally would claim at-rest encryption for a container where no inbox store existed to decorate, so
/// the startup guard would pass over plaintext PII. No proxy may stand in for the property it represents.
/// </remarks>
internal sealed class InboxEncryptionMarker;

/// <summary>
/// Marker registered by <c>AddOutboxEncryption()</c> — and only when it actually decorated a registered outbox
/// store — to record that the outbox is wired for at-rest payload encryption under the configured context (not per data subject).
/// </summary>
/// <remarks>
/// The marker attests that decoration <em>happened</em>, never that the method was called (see
/// <see cref="InboxEncryptionMarker"/>).
/// </remarks>
internal sealed class OutboxEncryptionMarker;
