// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Identifies a concrete suite bound to a private conformance contract.</summary>
/// <param name="BaseName">The contract's simple name.</param>
/// <param name="BaseIdentity">The assembly-qualified contract identity.</param>
/// <param name="SuiteIdentity">The assembly-qualified suite identity.</param>
internal sealed record ConformanceFork(string BaseName, string BaseIdentity, string SuiteIdentity);
