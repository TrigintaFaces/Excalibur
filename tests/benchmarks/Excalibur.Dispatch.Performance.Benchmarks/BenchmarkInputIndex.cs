// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Performance.Benchmarks;

internal static class BenchmarkInputIndex
{
    internal static int FromTickCount(int tickCount, int length) => (int)(unchecked((uint)tickCount) % (uint)length);
}
