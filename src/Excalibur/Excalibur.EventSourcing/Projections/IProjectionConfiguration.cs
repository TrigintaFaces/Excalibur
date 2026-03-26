// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Marker interface for convention-based projection registration via assembly scanning.
/// Implement this interface to register projections when using
/// <c>AddProjectionsFromAssembly</c>.
/// </summary>
/// <typeparam name="TProjection">The projection state type.</typeparam>
/// <remarks>
/// <para>
/// This enables convention-based registration instead of individual
/// <c>AddProjection&lt;T&gt;</c> calls:
/// </para>
/// <code>
/// // Registration
/// builder.AddProjectionsFromAssembly(typeof(OrderSummary).Assembly);
///
/// // Configuration class (discovered automatically)
/// public class OrderSummaryProjectionConfig : IProjectionConfiguration&lt;OrderSummary&gt;
/// {
///     public void Configure(IProjectionBuilder&lt;OrderSummary&gt; builder)
///     {
///         builder.Inline()
///             .When&lt;OrderPlaced&gt;((proj, e) =&gt; { proj.Total = e.Amount; })
///             .When&lt;OrderShipped&gt;((proj, e) =&gt; { proj.ShippedAt = e.ShippedAt; });
///     }
/// }
/// </code>
/// </remarks>
public interface IProjectionConfiguration<TProjection>
	where TProjection : class, new()
{
	/// <summary>
	/// Configures the projection's mode, event handlers, and options.
	/// </summary>
	/// <param name="builder">The projection builder to configure.</param>
	void Configure(IProjectionBuilder<TProjection> builder);
}
