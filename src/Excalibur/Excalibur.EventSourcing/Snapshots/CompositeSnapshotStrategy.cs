// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing.Snapshots;

/// <summary>
/// Composite snapshot strategy that combines multiple strategies.
/// </summary>
/// <remarks>
/// <para>
/// Allows combining multiple snapshot strategies using either Any (OR) or All (AND) logic.
/// Useful for complex snapshotting rules that depend on multiple conditions.
/// </para>
/// </remarks>
public sealed class CompositeSnapshotStrategy : ISnapshotStrategy
{
	private static readonly CompositeFormat UnknownCompositeModeFormat =
			CompositeFormat.Parse(Resources.CompositeSnapshotStrategy_UnknownCompositeModeFormat);

	private readonly IList<ISnapshotStrategy> _strategies;
	private readonly CompositeMode _mode;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompositeSnapshotStrategy"/> class.
	/// </summary>
	/// <param name="mode">The composite mode.</param>
	/// <param name="strategies">The strategies to combine.</param>
	/// <exception cref="ArgumentException">Thrown when no strategies are provided.</exception>
	public CompositeSnapshotStrategy(CompositeMode mode, params ISnapshotStrategy[] strategies)
	{
		if (strategies == null || strategies.Length == 0)
		{
			throw new ArgumentException(
					Resources.CompositeSnapshotStrategy_AtLeastOneStrategyMustBeProvided,
					nameof(strategies));
		}

		_mode = mode;
		_strategies = [.. strategies];
	}

	/// <summary>
	/// Defines how multiple strategies are combined.
	/// </summary>
	public enum CompositeMode
	{
		/// <summary>
		/// Create snapshot if ANY strategy returns true.
		/// </summary>
		Any = 0,

		/// <summary>
		/// Create snapshot only if ALL strategies return true.
		/// </summary>
		All = 1,
	}

	/// <summary>
	/// Gets the number of strategies in the composite.
	/// </summary>
	/// <value>The number of strategies in the composite.</value>
	public int StrategyCount => _strategies.Count;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Snapshot strategy evaluation may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Snapshot strategy evaluation may require dynamic code generation which is not compatible with AOT compilation.")]
	public bool ShouldCreateSnapshot(IAggregateRoot aggregate)
	{
		ArgumentNullException.ThrowIfNull(aggregate);

		return _mode switch
		{
			CompositeMode.Any => _strategies.Any(s => s.ShouldCreateSnapshot(aggregate)),
			CompositeMode.All => _strategies.All(s => s.ShouldCreateSnapshot(aggregate)),
			_ => throw new InvalidOperationException(
					string.Format(
							CultureInfo.CurrentCulture,
							UnknownCompositeModeFormat,
							_mode)),
		};
	}

	/// <summary>
	/// Adds a strategy to the composite.
	/// </summary>
	/// <param name="strategy">The strategy to add.</param>
	/// <exception cref="ArgumentNullException">Thrown when strategy is null.</exception>
	public void AddStrategy(ISnapshotStrategy strategy)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		_strategies.Add(strategy);
	}

	/// <summary>
	/// Removes a strategy from the composite.
	/// </summary>
	/// <param name="strategy">The strategy to remove.</param>
	/// <returns>True if the strategy was removed, otherwise false.</returns>
	public bool RemoveStrategy(ISnapshotStrategy strategy) => _strategies.Remove(strategy);
}
