// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Disposal;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TimerDisposalShould
{
	[Fact]
	public void DisposeCleanupTimerInInMemoryDeduplicator()
	{
		// Arrange -- a TimeProvider that hands back a timer it can be asked about afterwards. Before
		// TimeProvider the only way to observe the timer at all was to reflect over the field and check
		// its concrete type; now the property the test actually cares about -- the cleanup timer is
		// disposed with the deduplicator -- is directly observable through the seam.
		var clock = new SpyTimerTimeProvider();
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromHours(1),
		});
		var deduplicator = new InMemoryDeduplicator(
			options,
			meterFactory: null,
			clock,
			NullLogger<InMemoryDeduplicator>.Instance);

		var timer = clock.Created;
		timer.ShouldNotBeNull("a cleanup timer must be created when automatic cleanup is enabled");
		timer.IsDisposed.ShouldBeFalse("the timer must still be running before disposal");

		// Act
		deduplicator.Dispose();

		// Assert
		timer.IsDisposed.ShouldBeTrue("disposing the deduplicator must dispose its cleanup timer");
	}

	[Fact]
	public void DisposeCleanupTimerWhenCalledMultipleTimes()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromHours(1),
		});
		var deduplicator = new InMemoryDeduplicator(
			options,
			NullLogger<InMemoryDeduplicator>.Instance);

		// Act -- dispose multiple times
		deduplicator.Dispose();

		// Assert -- second dispose should not throw
		Should.NotThrow(() => deduplicator.Dispose());
	}

	[Fact]
	public void HaveTimerFieldInAllTimerBasedClasses()
	{
		// Arrange -- all classes that should have Timer fields
		var timerClasses = new[]
		{
			typeof(InMemoryDeduplicator),
		};

		foreach (var type in timerClasses)
		{
			// Act
			var timerFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(f => f.FieldType == typeof(Timer) || f.FieldType == typeof(System.Threading.ITimer))
				.ToList();

			// Assert
			timerFields.ShouldNotBeEmpty(
				$"{type.Name} should have at least one Timer field for cleanup");
		}
	}

	[Fact]
	public void VerifyTimerBasedClassesImplementIDisposable()
	{
		// Arrange -- scan the core Dispatch assembly for classes with Timer fields
		var assembly = typeof(InMemoryDeduplicator).Assembly;
		var timerClasses = assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.Any(f => f.FieldType == typeof(Timer) || f.FieldType == typeof(System.Threading.ITimer)))
			.ToList();

		// Act & Assert -- all classes with Timer fields should implement IDisposable or IAsyncDisposable
		var nonDisposable = timerClasses
			.Where(t => !typeof(IDisposable).IsAssignableFrom(t) && !typeof(IAsyncDisposable).IsAssignableFrom(t))
			.Select(t => t.FullName)
			.ToList();

		nonDisposable.ShouldBeEmpty(
			$"Timer-based classes that don't implement IDisposable: {string.Join(", ", nonDisposable)}");
	}

	[Fact]
	public void VerifyTimerBasedClassesImplementDispose()
	{
		// Arrange -- scan for classes with Timer fields
		var assembly = typeof(InMemoryDeduplicator).Assembly;
		var timerClasses = assembly.GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.Any(f => f.FieldType == typeof(Timer) || f.FieldType == typeof(System.Threading.ITimer)))
			.ToList();

		// Act & Assert -- all timer classes should have a Dispose method
		var missingDispose = timerClasses
			.Where(t =>
			{
				var hasDispose = typeof(IDisposable).IsAssignableFrom(t) ||
				                 typeof(IAsyncDisposable).IsAssignableFrom(t);
				return !hasDispose;
			})
			.Select(t => t.FullName)
			.ToList();

		missingDispose.ShouldBeEmpty(
			$"Timer-based classes not implementing IDisposable/IAsyncDisposable: {string.Join(", ", missingDispose)}");
	}

	/// <summary>
	/// A <see cref="TimeProvider"/> whose timer records whether it was disposed, so a test can assert
	/// the disposal itself instead of inferring it from the field's declared type.
	/// </summary>
	private sealed class SpyTimerTimeProvider : TimeProvider
	{
		public SpyTimer? Created { get; private set; }

		public override System.Threading.ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
			Created = new SpyTimer();

		internal sealed class SpyTimer : System.Threading.ITimer
		{
			public bool IsDisposed { get; private set; }

			public bool Change(TimeSpan dueTime, TimeSpan period) => !IsDisposed;

			public void Dispose() => IsDisposed = true;

			public ValueTask DisposeAsync()
			{
				Dispose();
				return ValueTask.CompletedTask;
			}
		}
	}
}
