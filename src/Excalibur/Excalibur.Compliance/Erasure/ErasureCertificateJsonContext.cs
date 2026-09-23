// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible erasure-certificate signing.
/// </summary>
/// <remarks>
/// Source-generated rather than reflection-based so the signing path stays AOT- and trim-safe, matching
/// how <c>ErasureVerificationJsonContext</c> already handles verification hashing in this namespace. The
/// generated writer emits properties in declaration order, which is what makes the signed input stable
/// across processes — a signature over a non-deterministic serialization would fail verification
/// intermittently.
/// </remarks>
// EVERY option that can move the emitted bytes is PINNED here rather than inherited. A signature is a
// promise about exact bytes, so an inherited default is a dependency on something nobody in this repo
// controls: a future SDK could change indentation, naming, number handling or default-value omission and
// silently invalidate every signature already issued, with no code change on our side and no test to catch
// it. These values are chosen to be stable, not pretty -- the payload is signed, not read by a human.
//
// UseStringEnumConverter = TRUE, and it is the one option here chosen for the READER rather than for
// byte-stability. Two reasons:
//
//   1. This canonical form is evidence. If these bytes are ever produced as proof of what was attested,
//      `"Method":0` cannot be interpreted without our source at the matching version, whereas
//      `"Method":"CryptographicErasure"` is self-describing. An attestation that requires the producing
//      code to read it is a poor attestation, which is the whole subject of this type.
//   2. It removes an entire hazard class instead of mitigating it. With numbers, the signed bytes of an
//      UNCHANGED logical certificate depend on the enum's numbering -- so inserting or renumbering a
//      member silently invalidates every signature already issued. C# permits duplicate enum values, so
//      nothing catches a careless reuse. With names, insert/reorder/renumber all become safe and the
//      remaining hazard is a RENAME, which is loud: it shows in a diff and moves the public API baseline.
//
// Scoped deliberately: this governs the SIGNED form only. The stores persist Method and LegalBasis as
// INT columns (`Method INT NOT NULL`), which is an independent representation this flip does not touch --
// so enum numbering stays load-bearing for PERSISTED ROWS even though it no longer is for signatures.
[JsonSourceGenerationOptions(
	WriteIndented = false,
	PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
	DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	NumberHandling = JsonNumberHandling.Strict,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(ErasureCertificatePayload))]
internal sealed partial class ErasureCertificateJsonContext : JsonSerializerContext;
