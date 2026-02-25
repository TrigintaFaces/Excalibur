// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Base interface for type-safe message transformations between versions.
/// </summary>
/// <typeparam name="TOld">The source message type (older version).</typeparam>
/// <typeparam name="TNew">The target message type (newer version).</typeparam>
/// <remarks>
/// <para>
/// This is the generic base interface used by all specialized upcasters
/// (events, commands, queries, integration events). Upcasters form a directed
/// graph where nodes are message versions and edges are transformations.
/// The <see cref="IUpcastingPipeline"/> uses BFS to find shortest paths between versions.
/// </para>
/// <para>
/// <b>Variance markers:</b>
/// <list type="bullet">
/// <item><description><c>in TOld</c> (contravariant) - allows accepting base types</description></item>
/// <item><description><c>out TNew</c> (covariant) - allows returning derived types</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Performance:</b> This approach provides ~20x improvement over DynamicInvoke
/// (15ns vs 300ns per migration) with zero allocations in the hot path.
/// </para>
/// </remarks>
public interface IMessageUpcaster<in TOld, out TNew>
	where TOld : IDispatchMessage, IVersionedMessage
	where TNew : IDispatchMessage, IVersionedMessage
{
	/// <summary>
	/// Gets the source version this upcaster transforms from.
	/// </summary>
	int FromVersion { get; }

	/// <summary>
	/// Gets the target version this upcaster transforms to.
	/// </summary>
	int ToVersion { get; }

	/// <summary>
	/// Transforms an old message version to a new version.
	/// </summary>
	/// <param name="oldMessage">The source message to transform.</param>
	/// <returns>A new message instance with the target version.</returns>
	/// <remarks>
	/// <para>This method must be:</para>
	/// <list type="bullet">
	/// <item><description><b>Pure</b> - same input always produces same output</description></item>
	/// <item><description><b>Immutable</b> - does not modify oldMessage</description></item>
	/// <item><description><b>Deterministic</b> - no random values or current time</description></item>
	/// <item><description><b>Fast</b> - called in hot path: event replay, command handling, query processing</description></item>
	/// </list>
	/// </remarks>
	TNew Upcast(TOld oldMessage);
}
