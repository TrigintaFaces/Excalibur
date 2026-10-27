// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

using Microsoft.Data.SqlClient;

namespace Excalibur.Examples.TwoProcessProjections;

/// <summary>
/// Records every side effect a projection handler performs, so the effects can be counted afterwards.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the instrument the sample exists for.</b> A real handler's side effect — an email, a
/// payment call — leaves no evidence inside the system, so an effect that was skipped when it should
/// have fired, or fired twice when it should have fired once, is invisible. Writing each attempt to a
/// table converts both into something a query can count.
/// </para>
/// <para>
/// It records attempts rather than deduplicating them. A handler in production MUST be idempotent on
/// the event identifier, and the natural way to be idempotent is a unique constraint on exactly this
/// shape; recording duplicates instead is what lets the sample SHOW the duplicate window rather than
/// swallow it.
/// </para>
/// </remarks>
public sealed class EffectLog(string connectionString)
{
	/// <summary>Records that an effect was attempted for an event.</summary>
	/// <param name="eventId">The event the effect belongs to.</param>
	/// <param name="orderId">The order the event belongs to.</param>
	/// <param name="role">Which process performed it.</param>
	/// <param name="isReplay">What the framework said about this delivery.</param>
	/// <param name="suppressed">
	/// Whether the handler declined to perform its effect for this delivery. Recorded SEPARATELY from
	/// the classification so the two can disagree visibly: an effect suppressed on a delivery that was
	/// not a re-fold is the defect, and it is only countable if both facts are stored.
	/// </param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the write.</returns>
	public async Task RecordAsync(
		string eventId,
		string orderId,
		string role,
		bool isReplay,
		bool suppressed,
		CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText =
			"INSERT INTO SampleEffectLog (EventId, OrderId, Role, IsReplay, Suppressed, AttemptedAt) " +
			"VALUES (@EventId, @OrderId, @Role, @IsReplay, @Suppressed, SYSUTCDATETIME());";
		_ = command.Parameters.AddWithValue("@EventId", eventId);
		_ = command.Parameters.AddWithValue("@OrderId", orderId);
		_ = command.Parameters.AddWithValue("@Role", role);
		_ = command.Parameters.AddWithValue("@IsReplay", isReplay);
		_ = command.Parameters.AddWithValue("@Suppressed", suppressed);

		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Per event: how many deliveries were recorded, how many arrived classified as a re-fold, how many
	/// had their effect suppressed, and how many actually performed it.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>One row per event identifier.</returns>
	public async Task<IReadOnlyList<(string EventId, int Deliveries, int ReportedReplay, int Suppressed, int Performed)>> SummariseAsync(
		CancellationToken cancellationToken)
	{
		var rows = new List<(string, int, int, int, int)>();

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT EventId, COUNT(*) AS Deliveries, "
			+ "SUM(CASE WHEN IsReplay = 1 THEN 1 ELSE 0 END) AS Replays, "
			+ "SUM(CASE WHEN Suppressed = 1 THEN 1 ELSE 0 END) AS Suppressed, "
			+ "SUM(CASE WHEN Suppressed = 0 THEN 1 ELSE 0 END) AS Performed "
			+ "FROM SampleEffectLog GROUP BY EventId ORDER BY MIN(AttemptedAt);";

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			rows.Add((reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4)));
		}

		return rows;
	}
}

/// <summary>
/// A DI-resolved projection handler that both folds state AND performs an outside effect.
/// </summary>
/// <remarks>
/// <para>
/// Registered with <c>WhenHandledBy</c>, which is the only handler shape that can take dependencies —
/// and therefore the only one that can reach anything outside the projection. The two halves of its
/// body are deliberately different in kind:
/// </para>
/// <list type="bullet">
/// <item>
/// folding into <see cref="OrderSummary"/> is idempotent by construction, so it is unguarded and
/// correct however many times it runs;
/// </item>
/// <item>
/// the effect is NOT, so it is skipped on a deliberate re-fold of history and would need to be
/// idempotent on the event identifier in production, because delivery is at-least-once.
/// </item>
/// </list>
/// <para>
/// <b><c>IsReplay</c> is not what makes this correct.</b> It is <see langword="false"/> on a
/// redelivery after a crash, because such a delivery is not a re-fold of history. The guard below
/// stops the whole of history replaying its notifications during a rebuild; the unique constraint a
/// real handler would put on the effect table is what stops a duplicate.
/// </para>
/// </remarks>
public sealed class OrderPlacedNotificationHandler(EffectLog log, RoleName role)
	: IProjectionEventHandler<OrderSummary, OrderPlaced>
{
	/// <inheritdoc />
	public async Task HandleAsync(
		OrderSummary projection,
		OrderPlaced @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(@event);
		ArgumentNullException.ThrowIfNull(context);

		// Always correct, never guarded.
		projection.Id = context.AggregateId;
		projection.CustomerId = @event.CustomerId;
		projection.Total = @event.Amount;
		projection.EventCount++;

		// RECORD THE DELIVERY FIRST, THEN DECIDE. Order matters here, and getting it wrong is what made
		// this instrument useless: returning on IsReplay before recording meant the log could only ever
		// contain deliveries where IsReplay was false, so the reported replay count was necessarily
		// zero no matter what the framework did -- and a wrongly suppressed delivery vanished from the
		// record entirely rather than showing up as the suppression it was.
		//
		// A number that cannot be anything but zero is not evidence. Now every delivery is recorded with
		// the classification it arrived with, and whether the effect was performed is recorded beside
		// it, so "suppressed when it should not have been" is a row you can count.
		var suppressed = context.IsReplay;

		await log.RecordAsync(
			@event.EventId,
			context.AggregateId,
			role.Value,
			context.IsReplay,
			suppressed,
			cancellationToken).ConfigureAwait(false);

		if (suppressed)
		{
			// A rebuild is re-folding the whole log on purpose. Notifying every historical customer
			// again is certainly wrong, so this is where the flag earns its keep.
			return;
		}

		// The effect itself would go here. The sample has none beyond the row above, which is the point:
		// the row IS the observable effect, so a skipped or repeated one is countable.
	}
}

/// <summary>Which role the current process is running as, for the effect log.</summary>
/// <param name="Value">The role name.</param>
public sealed record RoleName(string Value);
