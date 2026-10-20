// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Checks one projection type for a resolvable store.
/// </summary>
/// <remarks>
/// Closed over the projection type at registration, so the check needs no reflection and stays
/// AOT-safe. A non-generic interface lets the validator enumerate every registered projection without
/// knowing any of their types.
/// </remarks>
internal interface IProjectionStoreProbe
{
	/// <summary>The projection type this probe covers, for the failure message.</summary>
	string ProjectionTypeName { get; }

	/// <summary>
	/// Whether this projection needs a store and cannot reach one.
	/// </summary>
	/// <param name="services">The root provider; the probe opens its own scope.</param>
	/// <returns><see langword="true"/> when a store is required and absent.</returns>
	bool IsMissingARequiredStore(IServiceProvider services);
}

/// <inheritdoc />
/// <remarks>
/// The mode is supplied at construction rather than read from the registry, and that is load-bearing:
/// <c>AddProjection&lt;T&gt;</c> defers the registry write to start-up, so at validation time the
/// registry does not yet know this projection. The mode IS known at registration, because the configure
/// delegate has already run -- so the fact is captured where it exists instead of being looked up where
/// it does not.
/// </remarks>
internal sealed class ProjectionStoreProbe<TProjection> : IProjectionStoreProbe
	where TProjection : class
{
	private readonly bool _isEphemeral;

	public ProjectionStoreProbe(ProjectionMode mode) => _isEphemeral = mode == ProjectionMode.Ephemeral;

	/// <inheritdoc />
	public string ProjectionTypeName => typeof(TProjection).Name;

	/// <inheritdoc />
	public bool IsMissingARequiredStore(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// An ephemeral projection is rebuilt on demand and never stored, so storelessness is correct for it.
		if (_isEphemeral)
		{
			return false;
		}

		// ASK THE CONTAINER, DO NOT RESOLVE. IServiceProviderIsService is the first-party API for exactly
		// this question, and using it avoids two hazards that a resolve walks straight into: projection
		// stores are registered SCOPED, so resolving one from the root provider throws once scope
		// validation is on (which ASP.NET Core turns on in Development); and a store that implements only
		// IAsyncDisposable -- the Elasticsearch one does -- makes the synchronous disposal of the probe's
		// own scope throw. Either would mean this check crashed the host it exists to protect. It also
		// means no store is CONSTRUCTED at start-up merely to ask whether one exists.
        var registrations = services.GetService<IServiceProviderIsService>();

		// A container that cannot answer is not interrogated further: refusing to start on a question
		// nobody answered would be worse than the defect. Every container built by
		// ServiceCollection.BuildServiceProvider answers.
		return registrations is not null && !registrations.IsService(typeof(IProjectionStore<TProjection>));
	}
}

/// <summary>
/// Checks one projection event handler for constructor dependencies the container cannot supply.
/// </summary>
/// <remarks>
/// Closed over the handler type at registration, for the same reason as
/// <see cref="IProjectionStoreProbe"/>: the non-generic interface lets the validator enumerate every
/// registered handler without knowing any of their types.
/// </remarks>
internal interface IProjectionHandlerProbe
{
	/// <summary>The handler type this probe covers, for the failure message.</summary>
	string HandlerTypeName { get; }

	/// <summary>The projection this handler folds into, for the failure message.</summary>
	string ProjectionTypeName { get; }

	/// <summary>
	/// The dependencies this handler needs and the container cannot supply.
	/// </summary>
	/// <param name="services">The root provider; nothing is resolved from it.</param>
	/// <returns>Empty when the handler can be constructed.</returns>
	IReadOnlyList<string> UnsuppliableDependencies(IServiceProvider services);
}

/// <inheritdoc />
/// <remarks>
/// <para>
/// The store probe asks whether a projection has somewhere to be stored. This asks the narrower
/// question one level in: whether the handlers that fold into it can be CONSTRUCTED. A handler
/// depending on another projection's store -- the shape produced by a handler that reads several
/// projections to build the one it writes -- satisfies the store check and fails on the first event.
/// </para>
/// <para>
/// The handler type is known at registration, where it is also registered in the container, so no
/// reflection over the assembly is needed and the constructors are already declared as reachable by the
/// annotation on the type parameter.
/// </para>
/// </remarks>
internal sealed class ProjectionHandlerProbe<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>
	: IProjectionHandlerProbe
{
	private readonly string _projectionTypeName;

	public ProjectionHandlerProbe(string projectionTypeName) => _projectionTypeName = projectionTypeName;

	/// <inheritdoc />
	public string HandlerTypeName => typeof(THandler).Name;

	/// <inheritdoc />
	public string ProjectionTypeName => _projectionTypeName;

	/// <inheritdoc />
	public IReadOnlyList<string> UnsuppliableDependencies(IServiceProvider services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ASK THE CONTAINER, DO NOT CONSTRUCT. IServiceProviderIsService is the first-party API for this
		// question, and it already accounts for the dependencies the container manufactures without a
		// registration -- IServiceProvider, IServiceScopeFactory, and IEnumerable<T> of anything. Asking
		// it is what keeps this check from needing an exemption list that would drift from the
		// container's own rules. Nothing is instantiated, so no handler runs at start-up.
		var registrations = services.GetService<IServiceProviderIsService>();

		// A container that cannot answer is not interrogated further: refusing to start on a question
		// nobody answered would be worse than the defect.
		if (registrations is null)
		{
			return [];
		}

		var constructors = typeof(THandler).GetConstructors();

		// No public constructor is a different defect, and the container's own message for it names the
		// type and the rule. Reporting it here would only restate that less clearly.
		if (constructors.Length == 0)
		{
			return [];
		}

		// The container picks a constructor it can satisfy, so the handler is constructible when ANY of
		// them is. Reporting only when none is satisfiable makes a false refusal impossible -- which
		// matters more here than precision, because this check stops the host.
		IReadOnlyList<string>? widestShortfall = null;

		foreach (var constructor in constructors.OrderByDescending(c => c.GetParameters().Length))
		{
			var missing = Unsuppliable(constructor, registrations);

			if (missing.Count == 0)
			{
				return [];
			}

			// The widest constructor is the one the consumer wrote, so its shortfall is the one to report.
			widestShortfall ??= missing;
		}

		return widestShortfall ?? [];
	}

	private static List<string> Unsuppliable(
		ConstructorInfo constructor,
		IServiceProviderIsService registrations)
	{
		var missing = new List<string>();

		foreach (var parameter in constructor.GetParameters())
		{
			// An omitted optional parameter is a supported shape: the container leaves it at its default
			// and the handler still constructs.
			if (parameter.HasDefaultValue || registrations.IsService(parameter.ParameterType))
			{
				continue;
			}

			missing.Add($"{Describe(parameter.ParameterType)} {parameter.Name}");
		}

		return missing;
	}

	/// <summary>
	/// Renders a type the way the consumer wrote it. The whole value of the message is that it names the
	/// registration to add, and <c>IProjectionStore`1</c> does not.
	/// </summary>
	private static string Describe(Type type)
	{
		if (!type.IsConstructedGenericType)
		{
			return type.Name;
		}

		var name = type.Name;
		var arity = name.IndexOf('`', StringComparison.Ordinal);
		var bare = arity < 0 ? name : name[..arity];
		var arguments = string.Join(", ", type.GenericTypeArguments.Select(Describe));

		return $"{bare}<{arguments}>";
	}
}

/// <summary>
/// Startup guard: refuses to start when a projection is registered with no store to put it in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists, and why it is not opt-in.</b> A stored projection takes two registrations —
/// <c>AddProjection&lt;T&gt;</c> says what it folds, and a provider's <c>Add&lt;T&gt;</c> says where it
/// is stored. Nothing connected them, so omitting the second produced a projection that folds into
/// nothing. For an inline projection that surfaced as a resolution failure on the first save, after the
/// events were already durable, wrapped in advice about rebuilding from the event stream — the wrong
/// remedy when the fix is one registration line. For an async projection it surfaced inside a background
/// host, where nobody was looking. A projection with no store is wrong on every host, not only on hosts
/// that asked for something extra, so the check runs for all of them.
/// </para>
/// <para>
/// <b>It probes the RESOLVED store, in a scope, not the registration.</b> Resolving the way the apply
/// path resolves is the only check that sees what the writer will see — a decorator chain included.
/// </para>
/// </remarks>
internal sealed class ProjectionStoreValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;

	public ProjectionStoreValidator(IServiceProvider services) =>
		_services = services ?? throw new ArgumentNullException(nameof(services));

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	public void Validate()
	{
		RefuseStorelessProjections();
		RefuseUnconstructibleHandlers();
	}

	private void RefuseStorelessProjections()
	{
		var storeless = new List<string>();

		foreach (var probe in _services.GetServices<IProjectionStoreProbe>())
		{
			if (probe.IsMissingARequiredStore(_services))
			{
				storeless.Add(probe.ProjectionTypeName);
			}
		}

		if (storeless.Count == 0)
		{
			return;
		}

		var names = string.Join(", ", storeless);
		var subject = storeless.Count == 1 ? "projection" : "projections";
		var verb = storeless.Count == 1 ? "is" : "are";

		throw new InvalidOperationException(
			$"The {subject} {names} {verb} registered but {verb} not stored anywhere, so nothing folded "
			+ "into them will be saved and every read of them will come back empty.\n\n"
			+ "AddProjection<T>() says WHAT a projection folds. A second registration says WHERE it is "
			+ "stored, and that one is missing. Add the provider call for your store, for example:\n\n"
			+ "    services.AddElasticSearchProjections(nodeUri, p => p.Add<T>());\n"
			+ "    services.AddSqlServerProjections(connectionString, p => p.Add<T>());\n\n"
			+ "If a projection is meant to be rebuilt on demand and never stored, call .Ephemeral() on it "
			+ "instead and this check will skip it.");
	}

	/// <summary>
	/// Refuses to start when a registered handler needs a dependency the container cannot supply.
	/// </summary>
	/// <remarks>
	/// Runs after the storeless check, because a projection with no store at all is the coarser mistake
	/// and reporting both at once would bury it.
	/// </remarks>
	private void RefuseUnconstructibleHandlers()
	{
		var unconstructible = new List<string>();

		foreach (var probe in _services.GetServices<IProjectionHandlerProbe>())
		{
			var missing = probe.UnsuppliableDependencies(_services);

			if (missing.Count > 0)
			{
				unconstructible.Add(
					$"  {probe.HandlerTypeName} (folds {probe.ProjectionTypeName}) needs "
					+ string.Join(", ", missing));
			}
		}

		if (unconstructible.Count == 0)
		{
			return;
		}

		var subject = unconstructible.Count == 1 ? "handler" : "handlers";

		throw new InvalidOperationException(
			$"The following projection event {subject} cannot be constructed, because nothing registers "
			+ "the services they ask for:\n\n"
			+ string.Join("\n", unconstructible)
			+ "\n\nEach would have resolved on the first event it was given -- for an async projection "
			+ "that happens inside a background host, after the events are already durable and with no "
			+ "request to fail. Register what each handler needs. A handler reading another "
			+ "projection's data needs that projection's store registered too, for example:\n\n"
			+ "    services.AddElasticSearchProjections(nodeUri, p => p.Add<TOtherProjection>());\n\n"
			+ "If a dependency is genuinely optional, give the parameter a default value and this check "
			+ "will skip it.");
	}
}
