// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch;

/// <summary>
/// Fails host start when a domain event that could be written declares no message name, instead of
/// letting the first append discover it.
/// </summary>
/// <remarks>
/// <para>
/// Writing an event needs its declared name, so an event type without one throws inside
/// <c>AppendAsync</c> -- mid-transaction, at the deepest point of the call stack, quite possibly on a
/// type that persisted successfully in an earlier version. Registration already refuses an undeclared
/// type, which is the right moment, but only catches the types the consumer remembered to register.
/// </para>
/// <para>
/// The types that were registered say which assemblies the consumer keeps events in. This guard reads
/// those assemblies and reports any event they contain that declares no name -- the sibling left out of
/// the registration list, which is how the gap actually arises. Nothing outside those assemblies is
/// examined, so an unrelated dependency that happens to define an event cannot fail an unrelated host.
/// </para>
/// <para>
/// Trimming can remove a type before this runs, so a report is proof of a problem while a clean run is
/// not proof of its absence. It narrows the window; it does not close it. The check is startup-only and
/// touches no dispatch path.
/// </para>
/// </remarks>
internal sealed class UndeclaredEventNameStartupValidator : IStartupPrerequisiteValidator
{
	private readonly IEventTypeRegistry _registry;

	public UndeclaredEventNameStartupValidator(IEventTypeRegistry registry) =>
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "Startup-only diagnostic. Enumerating a trimmed assembly yields fewer types, which can only make this guard report less; it never fabricates a failure, and no dispatch path depends on it.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:UnrecognizedReflectionPattern",
		Justification = "As above: the scan is advisory at startup and degrades to reporting nothing under trimming.")]
	public void Validate()
	{
		if (_registry is not EventTypeRegistry registry || registry.IsEmpty)
		{
			// Nothing registered means no assemblies to infer, and an empty allow-list is already
			// refused by its own guard. Reporting here would duplicate that with a worse message.
			return;
		}

		var undeclared = new List<Type>();

		foreach (var assembly in registry.RegisteredTypes.Select(static t => t.Assembly).Distinct())
		{
			Type[] types;

			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				// A partially loadable assembly still answers for the types it could load. Examining
				// those is strictly better than abandoning the assembly.
				types = Array.ConvertAll(
					Array.FindAll(ex.Types, static t => t is not null),
					static t => t!);
			}

			foreach (var type in types)
			{
				if (type.IsAbstract
					|| type.IsInterface
					|| type.ContainsGenericParameters
					|| !typeof(IDomainEvent).IsAssignableFrom(type))
				{
					continue;
				}

				if (MessageNameHelper.GetDeclaredName(type) is null)
				{
					undeclared.Add(type);
				}
			}
		}

		if (undeclared.Count == 0)
		{
			return;
		}

		var names = string.Join(
			Environment.NewLine,
			undeclared.Select(static t => "  " + (t.FullName ?? t.Name)).Order(StringComparer.Ordinal));

		throw new InvalidOperationException(
			$"{undeclared.Count} domain event type(s) declare no message name:{Environment.NewLine}{names}{Environment.NewLine}"
			+ "An event is stored under the name it declares, so writing one of these would fail inside "
			+ "AppendAsync rather than here. Add [MessageName(\"...\")] to each type. A type that is not an "
			+ "event and cannot reach an event store does not need one and should not implement IDomainEvent.");
	}
}
