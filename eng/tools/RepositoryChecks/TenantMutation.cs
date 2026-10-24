// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>Records a concrete SQL mutation and its resolved tenant seam.</summary>
internal sealed record TenantMutation(BackendProvider Provider, string Path, bool Scoped);
