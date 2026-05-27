// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
		// Avoid LINQ allocation on hot path — called every SaveAsync for inline projections
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
	public void Register(ProjectionRegistration registration)
	{
		ArgumentNullException.ThrowIfNull(registration);
		_registrations[registration.ProjectionType] = registration;
	}
}
