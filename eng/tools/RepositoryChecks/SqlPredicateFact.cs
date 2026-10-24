// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Records a SQL fragment's source location and resolved placement evidence.</summary>
internal sealed record SqlPredicateFact(string Path, string Name, int Line, bool Placed);
