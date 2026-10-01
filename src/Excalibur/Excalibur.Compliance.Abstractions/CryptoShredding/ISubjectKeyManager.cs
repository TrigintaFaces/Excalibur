// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Manages the lifecycle of per-subject encryption keys that underpin crypto-shredding.
/// </summary>
/// <remarks>
/// <para>
/// Each data subject is assigned its own key handle. Encrypting a subject's personal data under that key
/// makes the data recoverable only while the key exists, so erasing the subject's key erases the subject
/// irreversibly (crypto-shredding) without mutating every stored record. Key destruction is not performed
/// through this interface: it is an erasure operation, and the erasure service owns it so that legal holds
/// are honoured before any key is destroyed.
/// </para>
/// <para>
/// Subject identifiers are pseudonymized through the registered data-subject hasher before being used as
/// key handles, so raw identifiers never leak into the key store.
/// </para>
/// <para>
/// <b>The "one subject, one handle" sentence above holds for every value EXCEPT one, and the exception is
/// the point of it.</b> An aggregate type the deployment has declared it must keep through an erasure —
/// because the law requires the data kept — resolves to a handle of its own, so that destroying the
/// subject's handle leaves the retained record readable rather than surviving as ciphertext nobody can
/// open. A subject therefore has one handle plus one per declared retention their data touches, and an
/// erasure destroys the first and, by design, not the rest. A deployment that declares no retention has
/// exactly one handle per subject, as before.
/// </para>
/// <para>
/// Key material MUST be produced by a cryptographically secure random number generator
/// (<see cref="System.Security.Cryptography.RandomNumberGenerator"/>) via the underlying key provider —
/// never from <see cref="System.Guid"/> or <see cref="System.Random"/>. This is a security invariant of
/// crypto-shredding: predictable key material would allow shredded data to be reconstructed.
/// </para>
/// </remarks>
public interface ISubjectKeyManager
{
	/// <summary>
	/// Returns the key handle for a data subject, creating a new cryptographically-random key if the
	/// subject does not yet have one.
	/// </summary>
	/// <param name="subjectId">
	/// The raw data-subject identifier. It is pseudonymized through the registered data-subject hasher
	/// before being resolved to a key handle.
	/// </param>
	/// <param name="retentionScope">
	/// Where the value being protected sits — the aggregate type and the capacity the subject holds there —
	/// or <see cref="RetentionScope.NotInAnAggregate"/> when it is not stored inside an aggregate.
	/// <para>
	/// <b>It selects WHICH key, and it matters only for a capacity under a declared erasure retention.</b>
	/// Such a capacity is legally required to survive an erasure of the subject, so its personal fields
	/// cannot share the key the erasure destroys: they get a handle of their own, and the retained record
	/// stays readable. Every other value resolves to the subject's own handle, unchanged.
	/// </para>
	/// </param>
	/// <param name="cancellationToken">A token that is observed for cancellation.</param>
	/// <returns>
	/// A task that completes with the subject's key handle AND the identifier of the generation of material
	/// currently provisioned there. Both are returned by the one call because a writer that looked the
	/// generation up separately could be overtaken between the two reads and bind a generation that no longer
	/// matches the material it encrypts under.
	/// </returns>
	ValueTask<SubjectKey> GetOrCreateKeyAsync(string subjectId, RetentionScope retentionScope, CancellationToken cancellationToken);
}
