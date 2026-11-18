// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Runtime.ExceptionServices;

namespace Excalibur.Data;

internal static partial class StaticDbCommand
{
	private const string CleanupFailuresKey = "Excalibur.Data.StaticDbCommand.CleanupFailures";
	private const string AlternateCleanupFailuresKey = "Excalibur.Data.StaticDbCommand.CleanupFailures.Secondary";

	private static async ValueTask<Exception?> CleanupReaderAndConnectionAsync(DbDataReader? reader,
		IDbConnection connection, bool wasClosed, Exception? primary)
	{
		try
		{
			if (reader is not null)
			{
				await reader.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception error)
		{
			primary = CaptureFailure(primary, error, "ReaderDispose");
		}
		finally
		{
			primary = CloseOwnedConnection(connection, wasClosed, primary);
		}

		return primary;
	}

	private static Exception? CloseOwnedConnection(IDbConnection connection, bool wasClosed, Exception? primary)
	{
		if (wasClosed)
		{
			try
			{
				connection.Close();
			}
			catch (Exception error)
			{
				primary = CaptureFailure(primary, error, "ConnectionClose");
			}
		}

		return primary;
	}

	private static Exception CaptureFailure(Exception? primary, Exception error, string phase)
	{
		if (primary is null)
		{
			return error;
		}

		TryAttachCleanupFailure(primary, error, phase);

		return primary;
	}

	private static void ThrowPrimary(Exception? primary)
	{
		if (primary is not null)
		{
			ExceptionDispatchInfo.Capture(primary).Throw();
		}
	}

	private static void TryAttachCleanupFailure(Exception primary, Exception error, string phase)
	{
		try
		{
			var data = primary.Data;
			if (data[CleanupFailuresKey] is CleanupFailureCollection owned)
			{
				owned.Add(new KeyValuePair<string, Exception>(phase, error));
				return;
			}

			var key = data.Contains(CleanupFailuresKey) ? AlternateCleanupFailuresKey : CleanupFailuresKey;
			if (data[key] is CleanupFailureCollection alternate)
			{
				alternate.Add(new KeyValuePair<string, Exception>(phase, error));
			}
			else if (!data.Contains(key))
			{
				data.Add(key, new CleanupFailureCollection { new(phase, error) });
			}
			// Never replace caller-owned data at either scoped key.
		}
		catch (Exception)
		{
			// Diagnostic attachment is best effort: a custom throwing/read-only Data implementation
			// cannot replace the primary exception or prevent subsequent resource cleanup attempts.
		}
	}

	private sealed class CleanupFailureCollection : List<KeyValuePair<string, Exception>>
	{
	}
}
