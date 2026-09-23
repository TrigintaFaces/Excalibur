// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring message processing pipelines.
/// </summary>
public sealed partial class PipelineBuilder : IPipelineBuilder
{
	private readonly IServiceProvider _serviceProvider;
	private readonly List<MiddlewareRegistration> _middlewares = [];
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;
	private MessageKinds? _messageKinds;
	private IReadOnlyList<IDispatchMiddleware> _resolvedMiddleware = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineBuilder"/> class.
	/// Creates a new pipeline builder.
	/// </summary>
	public PipelineBuilder(
		string name,
		IServiceProvider serviceProvider,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(serviceProvider);

		Name = name;
		_serviceProvider = serviceProvider;
		_applicabilityStrategy = applicabilityStrategy;
	}

	/// <inheritdoc />
	public string Name { get; }

	internal bool HasMiddlewareRegistered => _middlewares.Count > 0;

	internal IReadOnlyList<Type> ConfiguredMiddlewareTypes =>
		_middlewares
			.Select(static registration => registration.Type)
			.Where(static type => type is not null)
			.Cast<Type>()
			.ToArray();

	/// <summary>
	/// Gets the middleware instances resolved by the most recent <see cref="Build"/> call.
	/// This is the canonical resolve-safe materialization (unregistered/unconstructable
	/// profile middleware are skipped), reused by the dispatcher's invoker so there is a
	/// single resolution path.
	/// </summary>
	internal IReadOnlyList<IDispatchMiddleware> ResolvedMiddleware => _resolvedMiddleware;

	/// <inheritdoc />
	public IPipelineBuilder Use<TMiddleware>()
		where TMiddleware : IDispatchMiddleware
	{
		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition: null,
			MiddlewareCriticality.Required));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder Use(Func<IServiceProvider, IDispatchMiddleware> middlewareFactory)
	{
		ArgumentNullException.ThrowIfNull(middlewareFactory);

		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			type: null,
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					middlewareFactory(sp), capturedKinds.Value)
				: middlewareFactory,
			stage: null,
			condition: null,
			MiddlewareCriticality.Required));
		return this;
	}

	/// <summary>
	/// Registers middleware from a factory while retaining the middleware's type.
	/// </summary>
	/// <param name="middlewareType"> The middleware type the factory resolves. </param>
	/// <param name="middlewareFactory"> The factory that resolves the middleware. </param>
	/// <returns> This builder. </returns>
	/// <remarks>
	/// The public factory overload cannot know what it will produce, so a middleware that fails to
	/// resolve can only be reported anonymously. Callers that already hold the type use this
	/// instead, so an unresolvable entry is named in the failure rather than described as
	/// factory-supplied — which tells a consumer nothing about what to register.
	/// </remarks>
	internal IPipelineBuilder Use(Type middlewareType, Func<IServiceProvider, IDispatchMiddleware> middlewareFactory)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);
		ArgumentNullException.ThrowIfNull(middlewareFactory);

		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			middlewareType,
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					middlewareFactory(sp), capturedKinds.Value)
				: middlewareFactory,
			stage: null,
			condition: null,
			MiddlewareCriticality.Required));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseAt<TMiddleware>(DispatchMiddlewareStage stage)
		where TMiddleware : IDispatchMiddleware
	{
		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage,
			condition: null,
			MiddlewareCriticality.Required));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseWhen<TMiddleware>(Func<IServiceProvider, bool> condition)
		where TMiddleware : IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(condition);

		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition,
			MiddlewareCriticality.Required));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder ForMessageKinds(MessageKinds messageKinds)
	{
		_messageKinds = messageKinds;
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

		// Look up profile from registry (would be injected)
		var registry = _serviceProvider.GetService<IPipelineProfileRegistry>() ?? throw new InvalidOperationException(
				Resources.PipelineBuilder_ProfileRegistryNotRegistered);

		var profile = registry.GetProfile(profileName) ??
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.PipelineBuilder_ProfileNotFoundFormat,
					profileName),
				nameof(profileName));

		return UseProfile(profile);
	}

	/// <inheritdoc />
	public IPipelineBuilder UseProfile(IPipelineProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);

		// Clear existing middleware and apply profile
		_middlewares.Clear();

		// The gap this comment used to describe is closed. Criticality is now carried on the
		// profile's own entries, so a profile declares at authoring time whether an entry may
		// be omitted, and the fail-closed path in Build() is reachable from a profile.
		//
		// Criticality is NOT inferred from the profile's strictness flag, and must not be:
		// IsStrict is also the registry's auto-selection priority key, so making it
		// load-bearing here would silently re-route message selection for every existing
		// host. That was the reason the obvious fix was rejected; carrying criticality on the
		// entry is what avoids it.
		// Entries arrive from a public interface, so they may have been produced without running
		// MiddlewareEntry's constructor - a default value, or an unfilled array slot. Those carry a
		// null type and MiddlewareCriticality.Unspecified, and admitting one would let an unstated
		// criticality decide silently whether a middleware may be skipped. Every entry is validated
		// here, at the single point where declared entries become registrations.
		var entries = profile.MiddlewareEntries;

		for (var index = 0; index < entries.Count; index++)
		{
			var entry = entries[index];

			MiddlewareEntryValidation.ValidateEntry(in entry, profile.Name, index);

			var middlewareType = entry.MiddlewareType;

			_middlewares.Add(new MiddlewareRegistration(
				middlewareType,
				sp => sp.GetService(middlewareType) as IDispatchMiddleware,
				stage: null,
				condition: null,
				entry.Criticality));
		}

		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder Clear()
	{
		_middlewares.Clear();
		return this;
	}

	/// <inheritdoc />
	/// <remarks>
	/// Composition happens inside a dependency-injection scope. Every middleware this framework seats is
	/// registered <see cref="ServiceLifetime.Scoped"/>, and the provider a pipeline is composed from is
	/// the root -- a singleton factory is handed the root by definition. Resolving a scoped service there
	/// is refused outright under <see cref="ServiceProviderOptions.ValidateScopes"/> and succeeds silently
	/// without it, which is the worse half: one middleware graph, and everything it was constructed with,
	/// shared by every dispatch for the life of the process.
	/// </remarks>
	public IDispatchPipeline Build()
	{
		// Unconditionally, because the root cannot be recognised by inspection: the provider handed to a
		// singleton factory is the root ENGINE scope, which implements IServiceScope exactly as a real
		// child scope does. A guard that skipped this when the provider "already is a scope" would
		// therefore skip it precisely on the path that needs it. A scope nested inside a genuine child
		// scope costs one allocation at start-up and resolves the same services.
		var owned = _serviceProvider.GetService<IServiceScopeFactory>()?.CreateScope();

		try
		{
			return BuildFrom(owned?.ServiceProvider ?? _serviceProvider);
		}
		finally
		{
			owned?.Dispose();
		}
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2072:'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
		Justification = "A middleware type is resolved from DI; its constructors are preserved by its registration. " +
			"The scope verdict is advisory -- a type the walk cannot inspect is treated as scope-requiring, which " +
			"is the safe direction -- and AOT consumers use the source-generated dispatcher.")]
	private IDispatchPipeline BuildFrom(IServiceProvider resolutionProvider)
	{
		// Resolve all middleware instances
		var resolvedMiddleware = new List<IDispatchMiddleware>();
		List<(Type? Type, string Reason)>? unresolvedRequired = null;

		// Decides, per middleware type, whether an instance resolved here may be reused by every dispatch
		// or must be resolved per dispatch. The same verdict function the scoped-handler path uses, and for
		// the same question: would holding this from the root be a captive dependency? Null when the
		// composition has no scoping infrastructure at all, in which case there is no scope to resolve from
		// and every entry is held as before.
		var scopeResolver = new HandlerScopeResolver(_serviceProvider);
		var canResolvePerDispatch = scopeResolver.CanCreateScope;

		foreach (var registration in _middlewares)
		{
			// Check condition if present
			if (registration.Condition != null && !registration.Condition(resolutionProvider))
			{
				continue;
			}

			// A middleware entry can fail to materialize in exactly two ways, and BOTH are
			// gated by the entry's criticality — never by which of the two occurred:
			//
			//   null result       the service is not registered at all
			//   activation throw  the service is registered but a required constructor
			//                     dependency is not (the container throws while activating
			//                     it, even where the C# parameter is nullable)
			//
			// Optional entries are skipped and logged, as before. Required entries are
			// collected and reported together after the loop, so a consumer sees every
			// missing registration at once instead of fixing them one build at a time.
			//
			// The catch stays scoped to the activation path: it only swallows for Optional
			// entries, and the only Optional entries are profile-sourced ones resolved via
			// GetService, which throws for no other reason.
			IDispatchMiddleware? middleware;
			try
			{
				middleware = registration.Factory(resolutionProvider);
			}
			catch (InvalidOperationException ex)
			{
				if (registration.Criticality == MiddlewareCriticality.Required)
				{
					(unresolvedRequired ??= []).Add((registration.Type, ex.Message));
					continue;
				}

				LogSkippedMiddleware(resolutionProvider, registration.Type);
				continue;
			}

			if (middleware is null)
			{
				if (registration.Criticality == MiddlewareCriticality.Required)
				{
					(unresolvedRequired ??= []).Add(
						(registration.Type, Resources.PipelineBuilder_MiddlewareNotRegistered));
					continue;
				}

				LogSkippedMiddleware(resolutionProvider, registration.Type);
				continue;
			}

			// The instance just resolved proves the registration can be materialized, and carries the
			// stage and applicable-kinds metadata ordering and filtering need before any dispatch exists.
			// Whether it may also SERVE every dispatch is a separate question, and the container answers
			// it: a middleware the container would hand out fresh per scope must not be held here, because
			// this pipeline is a singleton. Those entries become a per-dispatch resolver instead, and the
			// instance resolved above is released with the composition scope.
			IDispatchMiddleware entry;
			if (canResolvePerDispatch
				&& registration.Type is not null
				&& scopeResolver.RequiresScope(registration.Type))
			{
				entry = new ScopeResolvedMiddleware(
					registration.Type,
					registration.Factory,
					middleware.Stage,
					middleware.ApplicableMessageKinds,
					scopeResolver,
					_serviceProvider);
			}
			else if (canResolvePerDispatch && registration.Type is not null)
			{
				// Held by this singleton pipeline for its whole lifetime, so it must come from the ROOT
				// provider -- never from the composition scope, which Build() disposes on return. A transient
				// (or any disposable) middleware resolved there would be disposed and then invoked on every
				// dispatch. The walk above has proven the type root-safe, so resolving it from root cannot
				// capture a scoped dependency. This is how convention middleware lives in ASP.NET Core:
				// constructed once from the application's services, for the application's lifetime.
				entry = registration.Factory(_serviceProvider) ?? middleware;
			}
			else
			{
				entry = middleware;
			}

			// Apply a registration-time stage override (UseAt<T>(stage)) by decoration. It must not be
			// written onto the middleware instance: middleware is typically a DI singleton shared by every
			// pipeline, so assigning its stage would leak this registration's ordering into all of them.
			resolvedMiddleware.Add(
				registration.Stage.HasValue
					? new StageOverrideMiddleware(entry, registration.Stage.Value)
					: entry);
		}

		// Fail closed once, naming every Required middleware that could not be materialized
		// and the service each one needs. Reported before the pipeline is cached so a failed
		// Build() cannot leave a partially-resolved pipeline behind for the invoker to use.
		if (unresolvedRequired is not null)
		{
			throw new InvalidOperationException(BuildUnresolvedRequiredMessage(unresolvedRequired));
		}

		// Cache the resolved instances so the dispatcher's invoker can reuse this single
		// resolution path rather than re-resolving (which would re-trigger the throws).
		_resolvedMiddleware = resolvedMiddleware;

		// Create pipeline with resolved middleware
		return new DispatchPipeline(resolvedMiddleware, _applicabilityStrategy);
	}

	private string BuildUnresolvedRequiredMessage(
		IReadOnlyList<(Type? Type, string Reason)> unresolved)
	{
		var details = new StringBuilder();

		for (var i = 0; i < unresolved.Count; i++)
		{
			var (middlewareType, reason) = unresolved[i];

			_ = details
				.Append(Environment.NewLine)
				.AppendFormat(
					CultureInfo.CurrentCulture,
					Resources.PipelineBuilder_RequiredMiddlewareUnresolvedEntryFormat,
					middlewareType?.FullName
						?? middlewareType?.Name
						?? Resources.PipelineBuilder_FactoryProvidedMiddleware,
					reason);
		}

		_ = details.Append(Environment.NewLine);

		return string.Format(
			CultureInfo.CurrentCulture,
			Resources.PipelineBuilder_RequiredMiddlewareUnresolvedFormat,
			Name,
			unresolved.Count,
			details.ToString());
	}

	private static void LogSkippedMiddleware(IServiceProvider serviceProvider, Type? middlewareType)
	{
		if (middlewareType is null)
		{
			return;
		}

		var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<PipelineBuilder>();
		if (logger is not null)
		{
			LogPipelineMiddlewareSkipped(logger, middlewareType.FullName ?? middlewareType.Name);
		}
	}

	/// <summary>
	/// Internal registration for middleware with metadata.
	/// </summary>
	private sealed class MiddlewareRegistration(
		Type? type,
		Func<IServiceProvider, IDispatchMiddleware?> factory,
		DispatchMiddlewareStage? stage,
		Func<IServiceProvider, bool>? condition,
		MiddlewareCriticality criticality)
	{
		public Type? Type { get; } = type;

		public Func<IServiceProvider, IDispatchMiddleware?> Factory { get; } = factory;

		public DispatchMiddlewareStage? Stage { get; } = stage;

		public Func<IServiceProvider, bool>? Condition { get; } = condition;

		/// <summary>
		/// Gets a value indicating whether this entry may be omitted when it cannot be materialized.
		/// </summary>
		public MiddlewareCriticality Criticality { get; } = criticality;
	}

	// Warning, not Debug. A configured pipeline stage that is not present changes what the pipeline
	// does, and the omission is otherwise undetectable: the dispatch succeeds, nothing throws, and the
	// stage simply never runs. Debug is the one level at which that goes unseen in every normal
	// production configuration, which makes it the level at which this particular fact is useless.
	// A deliberate omission costs one suppressible warning; an accidental one is now visible.
	[LoggerMessage(CoreEventId.InvokerMiddlewareSkipped, LogLevel.Warning,
		"Skipping configured pipeline middleware {MiddlewareType}: not registered in the service provider. "
		+ "This stage will not run. Register it (for example via the matching Use* call) or remove it from the profile.")]
	private static partial void LogPipelineMiddlewareSkipped(ILogger logger, string middlewareType);
}
