// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
