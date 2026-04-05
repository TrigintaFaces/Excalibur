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
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromHours(1),
		});
		var deduplicator = new InMemoryDeduplicator(
			options,
			NullLogger<InMemoryDeduplicator>.Instance);

		// Get the timer field via reflection
		var timerField = typeof(InMemoryDeduplicator)
			.GetField("_cleanupTimer", BindingFlags.NonPublic | BindingFlags.Instance);
		timerField.ShouldNotBeNull("Expected _cleanupTimer field on InMemoryDeduplicator");

		var timer = timerField.GetValue(deduplicator) as Timer;
		timer.ShouldNotBeNull("Timer should be initialized");

		// Act
		deduplicator.Dispose();

		// Assert -- after dispose, the timer should be disposed (Change returns false on disposed timer)
		timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).ShouldBeFalse(
			"Timer.Change should return false after disposal");
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
				.Where(f => f.FieldType == typeof(Timer) || f.FieldType == typeof(Timer))
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
				.Any(f => f.FieldType == typeof(Timer)))
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
				.Any(f => f.FieldType == typeof(Timer)))
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
}
