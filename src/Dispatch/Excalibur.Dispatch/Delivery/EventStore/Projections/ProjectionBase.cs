// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Base class for projections that provides common functionality.
/// </summary>
/// <typeparam name="TKey"> The type of the projection key. </typeparam>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
public abstract class ProjectionBase<TKey> : IProjection<TKey>
	where TKey : notnull
{
	/// <summary>
	/// Gets the unique key that identifies this projection.
	/// </summary>
	/// <value>The current <see cref="ProjectionKey"/> value.</value>
	public abstract TKey ProjectionKey { get; }

	/// <summary>
	/// Gets the unique identifier for this projection.
	/// </summary>
	/// <value>The current <see cref="Id"/> value.</value>
	public virtual TKey Id => ProjectionKey;

	/// <summary>
	/// Gets or sets the version of this projection.
	/// </summary>
	/// <value>The current <see cref="Version"/> value.</value>
	public abstract long Version { get; protected set; }

	/// <summary>
	/// Gets or sets the timestamp when this projection was last updated.
	/// </summary>
	/// <value>The current <see cref="LastModified"/> value.</value>
	public abstract DateTimeOffset LastModified { get; protected set; }
}
