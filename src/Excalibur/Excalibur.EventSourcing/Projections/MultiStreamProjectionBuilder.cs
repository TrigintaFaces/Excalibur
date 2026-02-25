// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Default implementation of <see cref="IMultiStreamProjectionBuilder{TProjection}"/>
/// that provides a fluent API for building multi-stream projections.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// This builder accumulates stream sources, categories, and event handlers,
/// then produces a <see cref="MultiStreamProjection{TProjection}"/> via <see cref="Build"/>.
/// </para>
/// </remarks>
public sealed class MultiStreamProjectionBuilder<TProjection> : IMultiStreamProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	private readonly MultiStreamProjection<TProjection> _projection = new();

	/// <inheritdoc />
	public IMultiStreamProjectionBuilder<TProjection> FromStream(string streamId)
	{
		ArgumentException.ThrowIfNullOrEmpty(streamId);
		_projection.AddStream(streamId);
		return this;
	}

	/// <inheritdoc />
	public IMultiStreamProjectionBuilder<TProjection> FromCategory(string category)
	{
		ArgumentException.ThrowIfNullOrEmpty(category);
		_projection.AddCategory(category);
		return this;
	}

	/// <inheritdoc />
	public IMultiStreamProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);
		_projection.AddHandler(handler);
		return this;
	}

	/// <inheritdoc />
	public MultiStreamProjection<TProjection> Build() => _projection;
}
