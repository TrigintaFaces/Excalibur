// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Postgres;

/// <summary>Preserves commit certainty across the complete append operation, including cleanup.</summary>
internal sealed class AppendCommitBoundary
{
	internal bool Dispatched { get; set; }
	internal AppendResult? Acknowledged { get; set; }

	internal static async ValueTask<AppendResult> ExecuteAsync(
		Func<AppendCommitBoundary, ValueTask<AppendResult>> operation)
	{
		if (System.Transactions.Transaction.Current is not null)
		{
			throw new InvalidOperationException("Event store appends require a store-owned transaction and cannot run inside an ambient transaction.");
		}

		var state = new AppendCommitBoundary();
		try
		{
			return await operation(state).ConfigureAwait(false);
		}
		catch (Exception) when (state.Acknowledged is not null)
		{
			// This boundary includes logging, telemetry and asynchronous resource disposal.
			// An acknowledged durable commit cannot become a rejected append during cleanup.
			return state.Acknowledged;
		}
		catch (OperationCanceledException)
		{
			// Cancellation after dispatch is intentionally not a statement of rollback.
			throw;
		}
		catch (Exception) when (state.Dispatched)
		{
			return AppendResult.CreateUnknown("Commit was requested but its outcome could not be established. Reconcile the original operation.");
		}
	}
	/// <summary>
	/// Gets the full exception message chain for better error diagnostics.
	/// </summary>
	internal static string DescribeFailure(Exception ex)
	{
		// Performance optimization: - use StringBuilder to avoid List allocation
		// Most exception chains are short (1-3 levels), so this is efficient
		var current = ex;
		if (current.InnerException == null)
		{
			return current.Message;
		}

		var sb = new System.Text.StringBuilder(current.Message);
		current = current.InnerException;
		while (current != null)
		{
			_ = sb.Append(" -> ");
			_ = sb.Append(current.Message);
			current = current.InnerException;
		}

		return sb.ToString();
	}

}
