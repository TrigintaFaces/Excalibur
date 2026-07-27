// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Workflows.Analyzers;

/// <summary>
/// Diagnostic IDs emitted by the workflow determinism analyzers. Shared with the companion code-fix
/// package so the fixable-ID list and the analyzer stay in lockstep.
/// </summary>
/// <remarks>
/// ID range: EXWF001-EXWF099 reserved for workflow determinism diagnostics.
/// </remarks>
public static class WorkflowDiagnosticIds
{
    /// <summary>
    /// EXWF001: a non-deterministic API is used inside a durable workflow body.
    /// </summary>
    public const string NonDeterministicApiInWorkflow = "EXWF001";

    /// <summary>
    /// Diagnostic property key carrying the suggested <c>IWorkflowContext</c> replacement member name
    /// (for example <c>UtcNowAsync</c> or <c>NewGuidAsync</c>) so the code-fix knows which primitive to
    /// substitute.
    /// </summary>
    public const string ReplacementPropertyKey = "Replacement";
}
