// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IProjectionRegistry"/>.
/// Registered as a singleton in DI.
/// </summary>
internal sealed class InMemoryProjectionRegistry : IProjectionRegistry
{
	private readonly ConcurrentDictionary<Type, ProjectionRegistration> _registrations = new();

	/// <inheritdoc />
	public ProjectionRegistration? GetRegistration(Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		return _registrations.TryGetValue(projectionType, out var registration) ? registration : null;
	}

	/// <inheritdoc />
	public IReadOnlyList<ProjectionRegistration> GetAll()
	{
		// Avoid LINQ allocation — manual list copy
		var values = _registrations.Values;
		var result = new List<ProjectionRegistration>(values.Count);
		foreach (var registration in values)
		{
			result.Add(registration);
		}

		return result;
	}

	/// <inheritdoc />
	public IReadOnlyList<ProjectionRegistration> GetByMode(ProjectionMode mode)
	{
		// Avoid LINQ allocation on hot path — called on every projection-host poll
		var result = new List<ProjectionRegistration>();
		foreach (var registration in _registrations.Values)
		{
			if (registration.Mode == mode)
			{
				result.Add(registration);
			}
		}

		return result;
	}

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">
	/// The same projection type is already registered in a different <see cref="ProjectionMode"/>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>A cross-mode duplicate is refused rather than applied.</b> This used to be a bare indexer
	/// assignment, so registering one projection type both inline and async silently displaced the first
	/// registration and the consumer got one of the two modes it asked for. The displacement was not
	/// symmetric either: registering an inline projection also writes container descriptors that cannot
	/// be withdrawn, so the inline coordinator stayed wired for a mode the registry no longer held.
	/// Neither call order was refused. Refusing here makes that composition inexpressible rather than
	/// silently resolved, and this is the one choke point every registration path reaches.
	/// </para>
	/// <para>
	/// A re-registration in the SAME mode still replaces, which keeps a projection reconfigurable in
	/// place and is what the mode cannot be incoherent about.
	/// </para>
	/// </remarks>
	public void Register(ProjectionRegistration registration)
	{
		ArgumentNullException.ThrowIfNull(registration);

		if (_registrations.TryGetValue(registration.ProjectionType, out var existing)
			&& existing.Mode != registration.Mode)
		{
			throw new InvalidOperationException(
				$"Projection type '{registration.ProjectionType.Name}' is already registered in " +
				$"{existing.Mode} mode and cannot also be registered in {registration.Mode} mode. " +
				"One projection type folds one stream of events into one destination, so registering it " +
				"in two modes would either fold every event twice or contradict the single cursor the " +
				$"type is tracked by. Register '{registration.ProjectionType.Name}' in one mode; to " +
				"project the same events a second way, declare a second projection type for it.");
		}

		_registrations[registration.ProjectionType] = registration;
	}
}
