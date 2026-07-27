// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Marks the property whose value is the raw data-subject identifier for a record, so that
/// <see cref="PersonalDataAttribute"/>-annotated fields on the same record are encrypted under
/// <em>that subject's</em> key (crypto-shredding by subject).
/// </summary>
/// <remarks>
/// <para>
/// Per-subject crypto-shredding needs to know which data subject a record belongs to before it can
/// select the subject's key. This marker names the identifying property; the value is hashed
/// (pseudonymized) to a stable key handle, and each <see cref="PersonalDataAttribute"/> field is then
/// encrypted under that per-subject key. Destroying the subject's key renders only that subject's PII
/// unrecoverable, leaving every other subject's data intact.
/// </para>
/// <para>
/// A record with no <see cref="DataSubjectIdAttribute"/> falls back to the configured purpose/registry
/// key — per-subject shredding is opt-in per record, additive over existing at-rest encryption.
/// </para>
/// <para>
/// At most one property per record may carry this marker. A record referencing multiple data subjects
/// is not covered by this v1 contract.
/// </para>
/// </remarks>
/// <example>
/// <code>
///public class CustomerRecord
///{
///[DataSubjectId]
///public string CustomerId { get; set; }
///
///[PersonalData(Category = PersonalDataCategory.ContactInfo)]
///public string Email { get; set; }
///}
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class DataSubjectIdAttribute : Attribute
{
}
