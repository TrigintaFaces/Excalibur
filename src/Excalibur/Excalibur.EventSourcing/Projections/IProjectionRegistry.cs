// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Registry for projection registrations, used internally by the event notification
/// broker and projection processors to discover registered projections.
/// </summary>
internal interface IProjectionRegistry
{
	/// <summary>
	/// Gets the registration for a specific projection type.
	/// </summary>
	/// <param name="projectionType">The CLR type of the projection.</param>
	/// <returns>The registration if found; otherwise, <c>null</c>.</returns>
	ProjectionRegistration? GetRegistration(Type projectionType);

	/// <summary>
	/// Gets all registered projections.
	/// </summary>
	/// <returns>All projection registrations.</returns>
	IReadOnlyList<ProjectionRegistration> GetAll();

	/// <summary>
	/// Gets all projections registered with the specified mode.
	/// </summary>
	/// <param name="mode">The projection mode to filter by.</param>
	/// <returns>The matching projection registrations.</returns>
	IReadOnlyList<ProjectionRegistration> GetByMode(ProjectionMode mode);

	/// <summary>
	/// Registers or replaces a projection. A second registration for the same
	/// projection type replaces the first (idempotent, R27.37).
	/// </summary>
	/// <param name="registration">The projection registration to add.</param>
	void Register(ProjectionRegistration registration);
}
