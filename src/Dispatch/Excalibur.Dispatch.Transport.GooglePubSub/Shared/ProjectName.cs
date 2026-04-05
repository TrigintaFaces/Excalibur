// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Project name wrapper for Google Cloud resources.
/// </summary>
public sealed class ProjectName
{
	/// <summary>
	/// Gets or sets the project ID.
	/// </summary>
	/// <value>
	/// The project ID.
	/// </value>
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>
	/// Creates a project name from a project ID.
	/// </summary>
	/// <param name="projectId"> The project ID. </param>
	/// <returns> A project name. </returns>
	public static ProjectName FromProjectId(string projectId) => new() { ProjectId = projectId };

	/// <summary>
	/// Creates a project name from a project ID (alias for FromProjectId).
	/// </summary>
	/// <param name="projectId"> The project ID. </param>
	/// <returns> A project name. </returns>
	public static ProjectName FromProject(string projectId) => FromProjectId(projectId);

	/// <summary>
	/// Implicit conversion from string.
	/// </summary>
	/// <param name="projectId"> The project ID. </param>
	/// <returns> A project name. </returns>
	public static implicit operator ProjectName(string projectId) => FromProjectId(projectId);

	/// <summary>
	/// Implicit conversion to string.
	/// </summary>
	/// <param name="projectName"> The project name. </param>
	/// <returns> The project ID. </returns>
	public static implicit operator string(ProjectName projectName) => projectName.ProjectId;

	/// <summary>
	/// Converts the project name to a string.
	/// </summary>
	/// <returns> The project ID. </returns>
	public override string ToString() => ProjectId;

	/// <summary>
	/// Returns this instance as a <see cref="ProjectName"/>.
	/// Provides an alternate method for the implicit string to <see cref="ProjectName"/> operator (CA2225).
	/// </summary>
	/// <returns>This instance.</returns>
	public ProjectName ToProjectName() => this;
}
