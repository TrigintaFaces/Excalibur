// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>One concrete suite's static reference coverage and explicit partial-suite deferral.</summary>
internal sealed record ArmSuite(string Identity, string Name, string Kit, int Arms, int EntryPoints, string[] Missing, string? PartialSibling, bool IsHelper = false);
