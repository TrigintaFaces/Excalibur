// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Result of pipeline profile synthesis.
/// </summary>
/// <remarks> Creates a new synthesis result. </remarks>
public sealed class SynthesisResult(
	IReadOnlyDictionary<string, IPipelineProfile> profiles,
	IReadOnlyDictionary<MessageKinds, string> mappings,
	ValidationIssue[] validationIssues)
{
	/// <summary>
	/// Gets the synthesized pipeline profiles.
	/// </summary>
	/// <value>
	/// The synthesized pipeline profiles.
	/// </value>
	public IReadOnlyDictionary<string, IPipelineProfile> Profiles { get; } = profiles ?? throw new ArgumentNullException(nameof(profiles));

	/// <summary>
	/// Gets the message kind to profile mappings.
	/// </summary>
	/// <value>
	/// The message kind to profile mappings.
	/// </value>
	public IReadOnlyDictionary<MessageKinds, string> Mappings { get; } = mappings ?? throw new ArgumentNullException(nameof(mappings));

	/// <summary>
	/// Gets validation issues encountered during synthesis.
	/// </summary>
	/// <value>
	/// Validation issues encountered during synthesis.
	/// </value>
	public ValidationIssue[] ValidationIssues { get; } = validationIssues ?? throw new ArgumentNullException(nameof(validationIssues));

	/// <summary>
	/// Gets a value indicating whether synthesis encountered any errors.
	/// </summary>
	/// <value>
	/// A value indicating whether synthesis encountered any errors.
	/// </value>
	public bool HasErrors => ValidationIssues.Any(static v => v.Severity == ValidationSeverity.Error);

	/// <summary>
	/// Gets a value indicating whether synthesis encountered any warnings.
	/// </summary>
	/// <value>
	/// A value indicating whether synthesis encountered any warnings.
	/// </value>
	public bool HasWarnings => ValidationIssues.Any(static v => v.Severity == ValidationSeverity.Warning);

	/// <summary>
	/// Gets error validation issues.
	/// </summary>
	/// <value>The current <see cref="Errors"/> value.</value>
	public IEnumerable<ValidationIssue> Errors =>
		ValidationIssues.Where(static v => v.Severity == ValidationSeverity.Error);

	/// <summary>
	/// Gets warning validation issues.
	/// </summary>
	/// <value>The current <see cref="Warnings"/> value.</value>
	public IEnumerable<ValidationIssue> Warnings =>
		ValidationIssues.Where(static v => v.Severity == ValidationSeverity.Warning);
}
