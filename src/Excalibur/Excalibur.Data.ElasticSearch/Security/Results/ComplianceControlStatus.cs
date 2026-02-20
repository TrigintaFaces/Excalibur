// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the status of a compliance control.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ComplianceControlStatus" /> class.
/// </remarks>
/// <param name="controlId"> The unique identifier for the control. </param>
/// <param name="controlName"> The name of the control. </param>
/// <param name="status"> The compliance status of the control. </param>
/// <param name="score"> The compliance score for this control. </param>
public sealed class ComplianceControlStatus(string controlId, string controlName, string status, double score)
{
	/// <summary>
	/// Gets the unique identifier for the control.
	/// </summary>
	/// <value>
	/// The unique identifier for the control.
	/// </value>
	public string ControlId { get; } = controlId ?? throw new ArgumentNullException(nameof(controlId));

	/// <summary>
	/// Gets the name of the control.
	/// </summary>
	/// <value>
	/// The name of the control.
	/// </value>
	public string ControlName { get; } = controlName ?? throw new ArgumentNullException(nameof(controlName));

	/// <summary>
	/// Gets the compliance status of the control.
	/// </summary>
	/// <value>
	/// The compliance status of the control.
	/// </value>
	public string Status { get; } = status ?? throw new ArgumentNullException(nameof(status));

	/// <summary>
	/// Gets the compliance score for this control.
	/// </summary>
	/// <value>
	/// The compliance score for this control.
	/// </value>
	public double Score { get; } = score;

	/// <summary>
	/// Gets the findings for this control.
	/// </summary>
	/// <value>
	/// The findings for this control.
	/// </value>
	public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets the evidence supporting this control's status.
	/// </summary>
	/// <value>
	/// The evidence supporting this control's status.
	/// </value>
	public IReadOnlyDictionary<string, object> Evidence { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
