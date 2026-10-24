// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace RepositoryChecks;

/// <summary>One independently enumerated source or test project and its validated compiler evidence.</summary>
internal sealed record RepositoryCompilation(string Path, bool Shipping, Compilation Compilation);
