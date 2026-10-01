// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>
/// The key a data subject's values are protected under: the handle that names it, and the identifier of the
/// generation of material currently provisioned there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves travel together because they are only meaningful together, and reading them separately
/// reintroduces the defect this type exists to close.</b> A handle is derived from the data subject, so it is
/// stable and therefore re-occupiable — destroy the key and the next ordinary write provisions new material at
/// the same handle. A writer that obtained the handle and then looked up the generation in a second call could
/// be overtaken between the two and bind a generation that no longer matches the material it is about to
/// encrypt under.
/// </para>
/// <para>
/// So the resolution that provisions the key is the one that reports its generation, in the same operation.
/// </para>
/// </remarks>
/// <param name="KeyId">
/// The key handle. Derived from the data subject, so it is stable across generations and is what an erasure
/// destroys.
/// </param>
/// <param name="Generation">
/// The identifier of the material currently at <paramref name="KeyId"/>, or <see langword="null"/> when the
/// configured provider cannot identify one. A null here means a crypto-shred read cannot distinguish a
/// destroyed key from a re-provisioned one, so callers that need that distinction refuse rather than assume.
/// </param>
public readonly record struct SubjectKey(string KeyId, string? Generation);
