// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Application;

/// <summary>
/// Provides a marker for the current assembly.
/// </summary>
/// <remarks>
/// This class is typically used as a reference point to identify the containing assembly for purposes such as:
/// - Configuring dependency injection by scanning the assembly.
/// - Locating resources or types within the assembly during runtime.
/// - Associating metadata with the assembly. /// It is an empty class and does not contain any functionality or state.
/// </remarks>
public static class AssemblyMarker;
