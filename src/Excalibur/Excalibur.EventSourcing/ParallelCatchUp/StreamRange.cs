// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.ParallelCatchUp;

/// <summary>
/// Represents a contiguous range of positions in the global event stream.
/// </summary>
/// <param name="StartPosition">The start position (inclusive).</param>
/// <param name="EndPosition">The end position (inclusive).</param>
public sealed record StreamRange(long StartPosition, long EndPosition);
