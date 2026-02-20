// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Versioning;

/// <summary>
/// Marks a saga state migrator with the source and target version numbers
/// for the migration it performs.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to <see cref="ISagaStateMigrator{TFrom, TTo}"/> implementations
/// to declare the version transition they handle. The infrastructure uses these
/// attributes to discover and chain migrators for multi-version upgrades.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SagaVersion(1, 2)]
/// public class OrderSagaMigratorV1ToV2 : ISagaStateMigrator&lt;OrderStateV1, OrderStateV2&gt;
/// {
///     // ...
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SagaVersionAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SagaVersionAttribute"/> class.
	/// </summary>
	/// <param name="fromVersion">The source version number.</param>
	/// <param name="toVersion">The target version number.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="fromVersion"/> is less than 1 or
	/// <paramref name="toVersion"/> is less than or equal to <paramref name="fromVersion"/>.
	/// </exception>
	public SagaVersionAttribute(int fromVersion, int toVersion)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(fromVersion, 1);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(toVersion, fromVersion);

		FromVersion = fromVersion;
		ToVersion = toVersion;
	}

	/// <summary>
	/// Gets the source version number that this migrator upgrades from.
	/// </summary>
	/// <value>The source version number.</value>
	public int FromVersion { get; }

	/// <summary>
	/// Gets the target version number that this migrator upgrades to.
	/// </summary>
	/// <value>The target version number.</value>
	public int ToVersion { get; }
}
