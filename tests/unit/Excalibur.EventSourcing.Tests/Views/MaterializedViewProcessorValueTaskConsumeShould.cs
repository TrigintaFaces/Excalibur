// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Sources;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Views;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Views;

/// <summary>
/// Regression lock for the <c>vexx3n</c> keystone: <see cref="MaterializedViewProcessor"/> must consume
/// each <see cref="ValueTask"/> returned by <see cref="IMaterializedViewStore"/> <b>exactly once</b>.
/// </summary>
/// <remarks>
/// The pre-fix processor awaited the store's generic <c>GetAsync</c>/<c>SaveAsync</c> via reflection
/// machinery that consumed the same <see cref="ValueTask"/> twice (<c>AsTask()</c> AND
/// <c>GetAwaiter().GetResult()</c>). A consumer-supplied <see cref="IValueTaskSource"/>-backed store
/// (the idiomatic allocation-free pattern) is single-consumption: a second consume throws / tears.
/// This lock backs the store's returns with single-consume <see cref="IValueTaskSource"/>s that throw
/// on a second <c>GetResult</c>, and asserts each is consumed exactly once — RED on the double-consume
/// impl, GREEN on the per-view-type accessor (plain single <c>await</c>).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
public sealed class MaterializedViewProcessorValueTaskConsumeShould
{
	private readonly IOptions<MaterializedViewOptions> _options =
		Options.Create(new MaterializedViewOptions { BatchSize = 100, BatchDelay = TimeSpan.Zero });

	private readonly ILogger<MaterializedViewProcessor> _logger =
		Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
			.CreateLogger<MaterializedViewProcessor>();

	[Fact]
	public async Task ConsumeTheStoreGetAndSaveValueTasksExactlyOnce()
	{
		// Arrange — a store whose GetAsync/SaveAsync return IValueTaskSource-backed ValueTasks that
		// throw on a second GetResult (the tear a real IValueTaskSource-backed store would exhibit).
		var store = new SingleConsumeViewStore();
		var registrations = new List<MaterializedViewBuilderRegistration>
		{
			new(typeof(CountingView), typeof(CountingViewBuilder), new CountingViewBuilder(), new ViewStoreAccessor<CountingView>(), ViewDeliverySemantics.ExactlyOnce),
		};

		var processor = new MaterializedViewProcessor(
			store,
			A.Fake<IGlobalStreamQuery>(),
			A.Fake<IEventSerializer>(),
			registrations,
			_options,
			_logger);

		// Act — one event routes to one builder → one GetAsync (load) + one SaveAsync (persist).
		await Should.NotThrowAsync(() =>
			processor.ProcessEventAsync(new TickEvent("agg-1"), 1, CancellationToken.None));

		// Assert — each store ValueTask was consumed EXACTLY once (double-consume would throw above
		// and/or push these counts past one).
		store.GetResultCount.ShouldBe(1);
		store.SaveResultCount.ShouldBe(1);
	}

	// --- Test view / builder / event ---

	private sealed class CountingView
	{
		public int Applied { get; set; }
	}

	private sealed class CountingViewBuilder : IMaterializedViewBuilder<CountingView>
	{
		public string ViewName => "Counting";

		public IReadOnlyList<Type> HandledEventTypes { get; } = [typeof(TickEvent)];

		public string? GetViewId(IDomainEvent @event) => "view-1";

		public CountingView Apply(CountingView view, IDomainEvent @event)
		{
			view.Applied++;
			return view;
		}

		public CountingView CreateNew() => new();
	}

	private sealed record TickEvent(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType => nameof(TickEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	// --- Single-consume store ---

	/// <summary>
	/// A view store that returns <see cref="IValueTaskSource"/>-backed <see cref="ValueTask"/>s which
	/// throw on a second <c>GetResult</c>. Position calls use plain completed tasks (not under test).
	/// </summary>
	private sealed class SingleConsumeViewStore : IAtomicMaterializedViewStore
	{
		public bool SupportsAtomicWrites => true;

		/// <summary>
		/// Delegates to <see cref="SaveAsync"/> then <see cref="SavePositionAsync"/>. This double exists to
		/// prove each returned <see cref="ValueTask"/> is awaited exactly once, not to model atomicity — so
		/// the two-call shape is deliberate: it is what puts the save's single-consume source on the path.
		/// </summary>
		public async ValueTask SaveViewAndPositionAsync<TView>(
			string viewName, string viewId, TView view, long position, CancellationToken cancellationToken)
			where TView : class
		{
			await SaveAsync(viewName, viewId, view, cancellationToken).ConfigureAwait(false);
			await SavePositionAsync(viewName, position, cancellationToken).ConfigureAwait(false);
		}

		private readonly Dictionary<string, object> _views = new(StringComparer.Ordinal);
		private SingleConsumeSource<object?>? _lastGet;
		private SingleConsumeVoidSource? _lastSave;

		public int GetResultCount => _lastGet?.ResultCount ?? 0;

		public int SaveResultCount => _lastSave?.ResultCount ?? 0;

		public ValueTask<TView?> GetAsync<TView>(string viewName, string viewId, CancellationToken cancellationToken)
			where TView : class
		{
			var key = $"{viewName}:{viewId}";
			_ = _views.TryGetValue(key, out var view);
			var source = new SingleConsumeSource<object?>(view);
			_lastGet = source;
			return new ValueTask<TView?>(new ProjectingSource<TView>(source), token: 0);
		}

		public ValueTask SaveAsync<TView>(string viewName, string viewId, TView view, CancellationToken cancellationToken)
			where TView : class
		{
			_views[$"{viewName}:{viewId}"] = view;
			var source = new SingleConsumeVoidSource();
			_lastSave = source;
			return new ValueTask(source, token: 0);
		}

		public ValueTask DeleteAsync(string viewName, string viewId, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask<long?> GetPositionAsync(string viewName, CancellationToken cancellationToken)
			=> new((long?)null);

		public ValueTask SavePositionAsync(string viewName, long position, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	/// <summary>Synchronously-completed source that throws if its result is consumed more than once.</summary>
	private sealed class SingleConsumeSource<T> : IValueTaskSource<T>
	{
		private readonly T _result;
		private int _resultCount;

		public SingleConsumeSource(T result) => _result = result;

		public int ResultCount => _resultCount;

		public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;

		public void OnCompleted(Action<object?> continuation, object? state, short token,
			ValueTaskSourceOnCompletedFlags flags) => continuation(state);

		public T GetResult(short token)
		{
			if (Interlocked.Increment(ref _resultCount) > 1)
			{
				throw new InvalidOperationException(
					"ValueTask<T> consumed more than once — IValueTaskSource is single-consumption.");
			}

			return _result;
		}
	}

	/// <summary>Projects a <see cref="SingleConsumeSource{T}"/> of <c>object?</c> onto <c>TView?</c>.</summary>
	private sealed class ProjectingSource<TView> : IValueTaskSource<TView?>
		where TView : class
	{
		private readonly SingleConsumeSource<object?> _inner;

		public ProjectingSource(SingleConsumeSource<object?> inner) => _inner = inner;

		public ValueTaskSourceStatus GetStatus(short token) => _inner.GetStatus(token);

		public void OnCompleted(Action<object?> continuation, object? state, short token,
			ValueTaskSourceOnCompletedFlags flags) => _inner.OnCompleted(continuation, state, token, flags);

		public TView? GetResult(short token) => (TView?)_inner.GetResult(token);
	}

	/// <summary>Void source that throws if consumed more than once.</summary>
	private sealed class SingleConsumeVoidSource : IValueTaskSource
	{
		private int _resultCount;

		public int ResultCount => _resultCount;

		public ValueTaskSourceStatus GetStatus(short token) => ValueTaskSourceStatus.Succeeded;

		public void OnCompleted(Action<object?> continuation, object? state, short token,
			ValueTaskSourceOnCompletedFlags flags) => continuation(state);

		public void GetResult(short token)
		{
			if (Interlocked.Increment(ref _resultCount) > 1)
			{
				throw new InvalidOperationException(
					"ValueTask consumed more than once — IValueTaskSource is single-consumption.");
			}
		}
	}
}
