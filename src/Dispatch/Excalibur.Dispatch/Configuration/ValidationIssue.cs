// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Represents a validation issue during synthesis.
/// </summary>
public sealed record ValidationIssue(ValidationSeverity Severity, string Message);
