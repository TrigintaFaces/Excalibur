// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.SourceGenerators.Validation;

/// <summary>
/// Marks an Options class for AOT-compatible validation source generation.
/// When applied to a class, the <see cref="AotValidationGenerator"/> will generate
/// a compile-time <c>IValidateOptions&lt;T&gt;</c> implementation that validates
/// <see cref="System.ComponentModel.DataAnnotations"/> attributes and cross-property
/// constraints without runtime reflection.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is consumed by the Roslyn source generator at compile time.
/// It does not affect runtime behavior directly — the generated validator does the work.
/// </para>
/// <para>
/// Usage:
/// <code>
/// [AotValidatable]
/// public class MyOptions
/// {
///     [Required]
///     public string Name { get; set; } = "";
///
///     [Range(1, 100)]
///     public int MaxRetries { get; set; } = 3;
/// }
/// </code>
/// This generates <c>MyOptionsValidator : IValidateOptions&lt;MyOptions&gt;</c> at compile time.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AotValidatableAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether to generate cross-property validation.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to include cross-property validation rules;
	/// otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.
	/// </value>
	public bool IncludeCrossPropertyValidation { get; set; } = true;

	/// <summary>
	/// Gets or sets the name of the generated validator class.
	/// When <see langword="null"/>, defaults to <c>{ClassName}Validator</c>.
	/// </summary>
	/// <value>The custom validator class name, or <see langword="null"/> for the default.</value>
	public string? ValidatorClassName { get; set; }
}
