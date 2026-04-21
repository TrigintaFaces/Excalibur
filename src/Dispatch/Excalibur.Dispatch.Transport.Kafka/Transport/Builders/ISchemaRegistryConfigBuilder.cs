// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Defines schema configuration methods for a Schema Registry builder.
/// </summary>
public interface ISchemaRegistryConfigBuilder
{
	/// <summary>
	/// Sets the subject naming strategy using the built-in enum.
	/// </summary>
	/// <param name="strategy">The subject name strategy.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see cref="SubjectNameStrategy.TopicName"/> which uses <c>{topic}-value</c>
	/// as the subject name.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SubjectNameStrategy(SubjectNameStrategy strategy);

	/// <summary>
	/// Sets a custom subject naming strategy.
	/// </summary>
	/// <typeparam name="TStrategy">The custom strategy type implementing <see cref="ISubjectNameStrategy"/>.</typeparam>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this for advanced scenarios where the built-in strategies are insufficient.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder SubjectNameStrategy<TStrategy>()
		where TStrategy : class, ISubjectNameStrategy, new();

	/// <summary>
	/// Sets the schema compatibility mode.
	/// </summary>
	/// <param name="mode">The compatibility mode.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see cref="CompatibilityMode.Backward"/> which allows new schemas
	/// to read data written with the previous schema version.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder CompatibilityMode(CompatibilityMode mode);

	/// <summary>
	/// Enables or disables automatic schema registration on first use.
	/// </summary>
	/// <param name="enable">Whether to auto-register schemas. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, schemas are automatically registered when a message type is first
	/// published. Disable in production if you want explicit schema management.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder AutoRegisterSchemas(bool enable = true);

	/// <summary>
	/// Enables or disables local schema validation before registration.
	/// </summary>
	/// <param name="enable">Whether to validate schemas locally. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, schemas are validated locally before being sent to the registry.
	/// This catches schema errors earlier in the development cycle.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder ValidateBeforeRegister(bool enable = true);
}
