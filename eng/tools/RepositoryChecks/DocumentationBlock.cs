// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace RepositoryChecks;

/// <summary>A Markdown fence with its original location and independently selected diff scope.</summary>
internal sealed record DocumentationBlock(string File, int StartLine, string Tier, string Code, bool Selected = true);
