// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Compliance;

namespace GdprCompliance.Domain;

/// <summary>
/// A support note, stored outside the event store, whose personal fields are protected by
/// <b>crypto-shredding</b>: each field is encrypted at rest under a key dedicated to the data subject named
/// by <see cref="CustomerId"/>, so destroying that one key renders this record's personal data unrecoverable
/// while every other subject's notes stay readable.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a different mechanism from the one <see cref="Customer"/> demonstrates, and the two must not be
/// confused.</b> The erase-in-place endpoints overwrite a <see cref="PersonalDataAttribute"/> field with
/// <see langword="null"/> — the plaintext was in the store until the moment it was cleared, and clearing it is
/// a write that has to reach every copy. Crypto-shredding never stores the plaintext at all: the store holds
/// ciphertext from the first write, and the erasure destroys one key rather than visiting every row.
/// </para>
/// <para>
/// Two markers do the work, and both are required here. <c>[DataSubjectId]</c> names the property whose value
/// identifies <em>whose</em> key protects this record; <c>[PersonalData]</c> names each property encrypted
/// under it. A type carrying the first and none of the second is refused rather than silently stored in the
/// clear.
/// </para>
/// <para>
/// A mutable <see langword="record"/> rather than a class, for one reason the walkthrough depends on:
/// encryption and decryption both mutate the instance in place, so reading the stored form back needs a copy,
/// and a <c>with</c> expression is that copy.
/// </para>
/// </remarks>
public sealed record SupportNote
{
	/// <summary>Gets or sets the data subject this note belongs to. Not personal data itself — it selects the key.</summary>
	[DataSubjectId]
	public string CustomerId { get; set; } = string.Empty;

	/// <summary>Gets or sets the customer's name, encrypted at rest under their own key.</summary>
	[PersonalData(Category = PersonalDataCategory.Identity)]
	public string? FullName { get; set; }

	/// <summary>Gets or sets the customer's email address, encrypted at rest under their own key.</summary>
	[PersonalData(Category = PersonalDataCategory.ContactInfo)]
	public string? EmailAddress { get; set; }

	/// <summary>Gets or sets the subject line. Not annotated, so it stays plaintext and still reads after erasure.</summary>
	public string Subject { get; set; } = string.Empty;
}
