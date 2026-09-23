// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// netstandard2.0 polyfill: enables records / init-only members in this Roslyn-component project.
namespace System.Runtime.CompilerServices;

/// <summary>Reserved compiler type required for <c>init</c> accessors on netstandard2.0.</summary>
internal static class IsExternalInit
{
}
