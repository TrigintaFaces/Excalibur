// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Records the independently measured providers and static suite bindings for one published contract.</summary>
internal sealed record BackendCoverageFact(string Contract, string ContractName, string Kit, string[] Providers, string[] Bindings, string[] Missing);
