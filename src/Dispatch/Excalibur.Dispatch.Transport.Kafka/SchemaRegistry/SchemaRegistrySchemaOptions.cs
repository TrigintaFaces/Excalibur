// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Schema management configuration options for the Confluent Schema Registry.
/// </summary>
/// <remarks>
/// This sub-options class is part of the <see cref="ConfluentSchemaRegistryOptions"/> ISP split
/// to keep each class within the 10-property gate.
/// </remarks>
public sealed class SchemaRegistrySchemaOptions
{
	/// <summary>
	/// Gets or sets whether to auto-register schemas on first use.
	/// </summary>
	/// <value><see langword="true"/> to auto-register; otherwise, <see langword="false"/>. Default is true.</value>
	public bool AutoRegisterSchemas { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to validate schemas locally before registration.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to validate schema structure before sending to the registry;
	/// otherwise, <see langword="false"/>. Default is <see langword="true"/>.
	/// </value>
	public bool ValidateBeforeRegister { get; set; } = true;

	/// <summary>
	/// Gets or sets the default compatibility mode for new subjects.
	/// </summary>
	/// <value>The compatibility mode. Default is <see cref="CompatibilityMode.Backward"/>.</value>
	public CompatibilityMode DefaultCompatibility { get; set; } = CompatibilityMode.Backward;

	/// <summary>
	/// Gets or sets the subject naming strategy.
	/// </summary>
	/// <value>The subject name strategy. Default is <see cref="SubjectNameStrategy.TopicName"/>.</value>
	public SubjectNameStrategy SubjectNameStrategy { get; set; } = SubjectNameStrategy.TopicName;

	/// <summary>
	/// Gets or sets the custom subject name strategy type, if using a custom implementation.
	/// </summary>
	/// <value>The custom strategy type, or <see langword="null"/> to use the enum-based strategy.</value>
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	public Type? CustomSubjectNameStrategyType { get; set; }
}
