// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Certificate management operations for erasure compliance.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface of <see cref="IErasureStore"/> isolates certificate
/// persistence concerns per the Interface Segregation Principle (ISP).
/// </para>
/// <para>
/// Consumers that only need certificate operations can depend on this
/// interface directly. Implementations that also implement <see cref="IErasureStore"/>
/// expose this interface via <c>GetService(typeof(IErasureCertificateStore))</c>.
/// </para>
/// </remarks>
public interface IErasureCertificateStore
{
	/// <summary>
	/// Saves an erasure certificate.
	/// </summary>
	/// <param name="certificate">The certificate to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="DuplicateErasureCertificateException">
	/// A certificate with the same <see cref="ErasureCertificate.CertificateId"/> already exists — this
	/// exception is raised for that condition and for no other. This operation inserts; it does not
	/// overwrite. A certificate is the erasure attestation itself, so replacing one in place would rewrite
	/// evidence that has already been issued and may already have been shown to a data subject or an
	/// auditor.
	/// </exception>
	/// <exception cref="ErasureStoreNotProvisionedException">
	/// The store's backing schema is absent, or is present but missing columns its statements bind. A
	/// deployment fault rather than an outcome of this call: the certificate was <b>not</b> stored.
	/// </exception>
	/// <remarks>
	/// Implementations raise these exceptions rather than the underlying database provider's own
	/// duplicate-key exception, so a caller can handle the condition without referencing that provider.
	/// A caller must catch <see cref="DuplicateErasureCertificateException"/> specifically rather than
	/// <see cref="InvalidOperationException"/>: the base type is also raised for conditions that mean the
	/// certificate was never persisted, and reading one of those as "already issued" would report an
	/// attestation that does not exist.
	/// </remarks>
	Task SaveCertificateAsync(
		ErasureCertificate certificate,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a certificate by request ID.
	/// </summary>
	/// <param name="requestId">The request ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The certificate, or null if not found.</returns>
	Task<ErasureCertificate?> GetCertificateAsync(
		Guid requestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets a certificate by certificate ID.
	/// </summary>
	/// <param name="certificateId">The certificate ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The certificate, or null if not found.</returns>
	Task<ErasureCertificate?> GetCertificateByIdAsync(
		Guid certificateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes expired certificates past their retention period.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of certificates deleted.</returns>
	Task<int> CleanupExpiredCertificatesAsync(
		CancellationToken cancellationToken);
}
