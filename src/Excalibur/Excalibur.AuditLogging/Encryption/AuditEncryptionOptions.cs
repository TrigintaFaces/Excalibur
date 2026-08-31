// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

namespace Excalibur.AuditLogging.Encryption;

/// <summary>
/// Options controlling which audit event fields are encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// By default, only the <see cref="EncryptActorId"/> and <see cref="EncryptIpAddress"/> fields
/// are encrypted, as these are the most common PII-bearing fields in audit events.
/// </para>
/// <para>
/// The <see cref="EncryptionPurpose"/> is passed to the <see cref="EncryptionContext"/>
/// to allow key selection policies to differentiate audit encryption from other uses.
/// </para>
/// <para>
/// <b>Encrypting a field costs you the ability to query by it.</b> The cipher is randomized, so equal
/// values do not produce equal stored values and no equality comparison can find them; a query filtering
/// on an encrypted field is refused with <see cref="NotSupportedException"/> rather than answered with an
/// empty result. Two of these fields have a matching filter on <see cref="AuditQuery"/> and so carry that
/// cost -- <see cref="EncryptActorId"/> and <see cref="EncryptIpAddress"/>. <see cref="EncryptReason"/>
/// and <see cref="EncryptUserAgent"/> do not, because there is no filter over them to lose. Decide each
/// field on which you need more: the value unreadable to anyone holding the database, or the ability to
/// ask the trail about it.
/// </para>
/// </remarks>
public sealed class AuditEncryptionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="AuditEvent.ActorId"/> field is encrypted.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	/// <remarks>
	/// While this is on, <see cref="AuditQuery.ActorId"/> cannot be served and a query naming it is
	/// refused. Set it to <see langword="false"/> when "what did this actor do" is a question the trail
	/// must be able to answer; the actor id is then stored in the clear and visible to anyone with
	/// database access.
	/// </remarks>
	public bool EncryptActorId { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="AuditEvent.IpAddress"/> field is encrypted.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	/// <remarks>
	/// Carries the same cost as <see cref="EncryptActorId"/>, against
	/// <see cref="AuditQuery.IpAddress"/>: while this is on, a query filtering by address is refused
	/// rather than answered emptily.
	/// </remarks>
	public bool EncryptIpAddress { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="AuditEvent.Reason"/> field is encrypted.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EncryptReason { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the <see cref="AuditEvent.UserAgent"/> field is encrypted.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EncryptUserAgent { get; set; }

	/// <summary>
	/// Gets or sets the encryption purpose passed to the encryption context for key selection.
	/// </summary>
	/// <value>"audit-event-field" by default.</value>
	public string EncryptionPurpose { get; set; } = "audit-event-field";
}
