// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance;

/// <summary>
/// Thrown by <see cref="IErasureCertificateStore.SaveCertificateAsync"/> when — and only when — a
/// certificate with the same <see cref="ErasureCertificate.CertificateId"/> is already stored.
/// </summary>
/// <remarks>
/// <para>
/// A certificate is the erasure attestation itself, so this operation inserts and never overwrites:
/// replacing one in place would rewrite evidence that has already been issued and may already have been
/// shown to a data subject or an auditor.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException"/> so an existing broad
/// <c>catch (InvalidOperationException)</c> keeps working, but a caller that needs the duplicate signal
/// specifically must catch <b>this</b> type. The base type is also raised for unrelated conditions — a
/// store whose schema is absent, a disposed store, an unresolved ambient tenant — and treating any of
/// those as "already issued" would report an attestation that was never persisted.
/// </para>
/// <para>
/// Implementations raise this rather than the underlying database provider's own duplicate-key exception,
/// so a caller can handle the condition without referencing that provider. The provider's exception is
/// preserved as <see cref="Exception.InnerException"/>.
/// </para>
/// </remarks>
public sealed class DuplicateErasureCertificateException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureCertificateException"/> class.
	/// </summary>
	public DuplicateErasureCertificateException()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureCertificateException"/> class with a message.
	/// </summary>
	/// <param name="message">The error message.</param>
	public DuplicateErasureCertificateException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DuplicateErasureCertificateException"/> class with a
	/// message and inner exception.
	/// </summary>
	/// <param name="message">The error message.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, when there was one.</param>
	public DuplicateErasureCertificateException(string message, Exception? innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the identifier of the certificate that is already stored.
	/// </summary>
	/// <value>
	/// The <see cref="ErasureCertificate.CertificateId"/> that was re-issued, or <see langword="null"/>
	/// when the exception was constructed without one.
	/// </value>
	public Guid? CertificateId { get; init; }

	/// <summary>
	/// Creates an exception for a certificate identifier that is already stored.
	/// </summary>
	/// <param name="certificateId">The certificate identifier that was re-issued.</param>
	/// <param name="innerException">The underlying store failure that reported the duplicate, if any.</param>
	/// <returns>The exception to throw.</returns>
	public static DuplicateErasureCertificateException ForCertificateId(
		Guid certificateId,
		Exception? innerException = null) =>
		new($"An erasure certificate with id '{certificateId}' already exists.", innerException)
		{
			CertificateId = certificateId,
		};
}
