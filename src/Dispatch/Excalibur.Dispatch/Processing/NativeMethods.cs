// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Native methods for thread affinity management.
/// </summary>
internal static partial class NativeMethods
{
	[LibraryImport("kernel32.dll")]
	internal static partial IntPtr GetCurrentThread();

	[LibraryImport("kernel32.dll")]
	internal static partial IntPtr SetThreadAffinityMask(
			IntPtr hThread,
			IntPtr dwThreadAffinityMask);
}
