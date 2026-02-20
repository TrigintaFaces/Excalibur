// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Saga;

/// <summary>
/// OpenTelemetry activity source for saga operations, providing distributed tracing capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This activity source is used by saga infrastructure to create spans for:
/// </para>
/// <list type="bullet">
/// <item><description>Timeout scheduling operations</description></item>
/// <item><description>Timeout delivery service execution</description></item>
/// <item><description>Individual timeout deliveries</description></item>
/// <item><description>SqlServer store operations</description></item>
/// </list>
/// <para>
/// <b>Tag Conventions:</b>
/// <list type="bullet">
/// <item><description><c>saga.id</c> - Saga instance identifier</description></item>
/// <item><description><c>saga.type</c> - Saga type name</description></item>
/// <item><description><c>timeout.id</c> - Timeout identifier</description></item>
/// <item><description><c>timeout.type</c> - Timeout message type</description></item>
/// <item><description><c>timeout.due_at</c> - When timeout was due (ISO 8601)</description></item>
/// </list>
/// </para>
/// </remarks>
public static class SagaActivitySource
{
	/// <summary>
	/// The name of the activity source: "Excalibur.Dispatch.Sagas".
	/// </summary>
	public const string SourceName = "Excalibur.Dispatch.Sagas";

	/// <summary>
	/// The version of the activity source.
	/// </summary>
	public const string SourceVersion = "1.0.0";

	private static readonly ActivitySource Source = new(SourceName, SourceVersion);

	/// <summary>
	/// Gets the underlying activity source for advanced scenarios.
	/// </summary>
	public static ActivitySource Instance => Source;

	/// <summary>
	/// Starts a new activity with the specified name.
	/// </summary>
	/// <param name="name">The name of the activity.</param>
	/// <param name="kind">The kind of activity. Default is <see cref="ActivityKind.Internal"/>.</param>
	/// <returns>The started activity, or null if tracing is not enabled.</returns>
	public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) =>
		Source.StartActivity(name, kind);
}
