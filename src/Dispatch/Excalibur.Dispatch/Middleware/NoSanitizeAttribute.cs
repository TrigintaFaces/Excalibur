// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Attribute to mark properties that should not be sanitized.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NoSanitizeAttribute : Attribute;
