// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Published kit reflection guard coverage, including guardless kits that rely on external verification.</summary>
internal sealed record ConformanceGuardResult(string[] GuardlessKits, string[] Failures);
