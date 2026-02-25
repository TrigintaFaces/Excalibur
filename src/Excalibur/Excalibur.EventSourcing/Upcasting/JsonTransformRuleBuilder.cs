// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Upcasting;

/// <summary>
/// Fluent builder for constructing a list of <see cref="JsonTransformRule"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Usage example:
/// <code>
/// var rules = new JsonTransformRuleBuilder()
///     .Rename("OldProperty", "NewProperty")
///     .Remove("DeprecatedField")
///     .AddDefault("NewField", 42)
///     .Move("Nested.Value", "TopLevel")
///     .Build();
/// </code>
/// </para>
/// </remarks>
public sealed class JsonTransformRuleBuilder
{
	private readonly List<JsonTransformRule> _rules = [];

	/// <summary>
	/// Adds a rename rule that renames a property from <paramref name="sourcePath"/> to <paramref name="targetPath"/>.
	/// </summary>
	/// <param name="sourcePath">The current property name.</param>
	/// <param name="targetPath">The new property name.</param>
	/// <returns>The builder for method chaining.</returns>
	public JsonTransformRuleBuilder Rename(string sourcePath, string targetPath)
	{
		ArgumentException.ThrowIfNullOrEmpty(sourcePath);
		ArgumentException.ThrowIfNullOrEmpty(targetPath);

		_rules.Add(new JsonTransformRule(JsonTransformOperation.Rename, sourcePath, targetPath));
		return this;
	}

	/// <summary>
	/// Adds a remove rule that deletes the property at <paramref name="path"/>.
	/// </summary>
	/// <param name="path">The property name to remove.</param>
	/// <returns>The builder for method chaining.</returns>
	public JsonTransformRuleBuilder Remove(string path)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);

		_rules.Add(new JsonTransformRule(JsonTransformOperation.Remove, path));
		return this;
	}

	/// <summary>
	/// Adds an add-default rule that sets the property at <paramref name="path"/> to <paramref name="defaultValue"/>
	/// if it does not already exist.
	/// </summary>
	/// <param name="path">The property name to add.</param>
	/// <param name="defaultValue">The default value.</param>
	/// <returns>The builder for method chaining.</returns>
	public JsonTransformRuleBuilder AddDefault(string path, object? defaultValue)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);

		_rules.Add(new JsonTransformRule(JsonTransformOperation.AddDefault, path, DefaultValue: defaultValue));
		return this;
	}

	/// <summary>
	/// Adds a move rule that moves a property from <paramref name="sourcePath"/> to <paramref name="targetPath"/>.
	/// </summary>
	/// <param name="sourcePath">The source property name.</param>
	/// <param name="targetPath">The target property name.</param>
	/// <returns>The builder for method chaining.</returns>
	public JsonTransformRuleBuilder Move(string sourcePath, string targetPath)
	{
		ArgumentException.ThrowIfNullOrEmpty(sourcePath);
		ArgumentException.ThrowIfNullOrEmpty(targetPath);

		_rules.Add(new JsonTransformRule(JsonTransformOperation.Move, sourcePath, targetPath));
		return this;
	}

	/// <summary>
	/// Builds the list of transformation rules.
	/// </summary>
	/// <returns>An immutable list of transformation rules.</returns>
	public IReadOnlyList<JsonTransformRule> Build() => _rules.AsReadOnly();
}
