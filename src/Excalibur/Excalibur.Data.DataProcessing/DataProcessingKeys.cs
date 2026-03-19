// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Well-known keyed service keys for DataProcessing DI registrations.
/// </summary>
/// <remarks>
/// <para>
/// Use these keys with <c>[FromKeyedServices]</c> to resolve specific DataProcessing
/// service registrations when multiple <see cref="System.Func{T}"/> factories
/// for <see cref="System.Data.IDbConnection"/> are registered.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class MyProcessor(
///     [FromKeyedServices(DataProcessingKeys.OrchestrationConnection)]
///     Func&lt;IDbConnection&gt; orchestrationFactory)
/// {
///     // orchestrationFactory resolves to the DataProcessing orchestration database
/// }
/// </code>
/// </example>
public static class DataProcessingKeys
{
	/// <summary>
	/// The keyed service key for the orchestration database connection factory.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Use with <c>[FromKeyedServices]</c> to resolve the orchestration
	/// <see cref="System.Func{T}"/> for <see cref="System.Data.IDbConnection"/>.
	/// This connection is used by <see cref="DataOrchestrationManager"/> to manage
	/// data task requests and processing state.
	/// </para>
	/// </remarks>
	public const string OrchestrationConnection = "Excalibur.DataProcessing.Orchestration";
}
