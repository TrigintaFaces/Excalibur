// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// The one definition of an erasure certificate payload's canonical bytes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Signing, verification and storage all go through here, and that is the whole point.</b> A second
/// notion of "the bytes that represent this certificate" is the drift that turns an honest document into a
/// reported forgery — and it does not announce itself, because each side is individually correct. The audit
/// subsystem already separates canonicalization from the MAC for the same reason; this is that shape,
/// applied to the certificate.
/// </para>
/// <para>
/// <b>Why a store persists this rather than a column per claim.</b> A column has a type, and a type may be
/// unable to represent the value handed to it. Postgres <c>TIMESTAMPTZ</c> holds microseconds where a .NET
/// <see cref="DateTimeOffset"/> holds hundreds of nanoseconds, so a certificate projected into timestamp
/// columns comes back with three of its instants quietly rounded — a document that no longer matches its
/// own signature, through no fault of anyone's field list. Persisting the canonical form removes the
/// question: what is read back is what was signed, whatever the engine's column types can express.
/// </para>
/// </remarks>
internal static class ErasureCertificateCanonicalizer
{
	/// <summary>Renders <paramref name="payload"/> to its canonical JSON form.</summary>
	/// <param name="payload">The payload to render.</param>
	/// <returns>The canonical JSON. These are the bytes the signature covers.</returns>
	public static string ToCanonicalJson(ErasureCertificatePayload payload) =>
		JsonSerializer.Serialize(payload, ErasureCertificateJsonContext.Default.ErasureCertificatePayload);

	/// <summary>Reads a payload back from the canonical JSON a store persisted.</summary>
	/// <param name="canonicalJson">The canonical JSON, exactly as <see cref="ToCanonicalJson"/> produced it.</param>
	/// <returns>The payload.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the stored form cannot be read back. Fail closed: a certificate row whose payload cannot
	/// be parsed is evidence that cannot be produced, and returning a partially-reconstructed one would hand
	/// a caller an attestation nobody issued.
	/// </exception>
	public static ErasureCertificatePayload FromCanonicalJson(string canonicalJson)
	{
		ErasureCertificatePayload? payload;

		try
		{
			payload = JsonSerializer.Deserialize(
				canonicalJson,
				ErasureCertificateJsonContext.Default.ErasureCertificatePayload);
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException(
				"A stored erasure certificate's payload could not be read back: the persisted canonical form is "
				+ "not valid for this scheme. The certificate cannot be produced as evidence, because what it "
				+ "asserted cannot be recovered.",
				ex);
		}

		return payload
			?? throw new InvalidOperationException(
				"A stored erasure certificate's payload column holds a JSON null. The certificate cannot be "
				+ "produced as evidence, because what it asserted was not persisted.");
	}
}
