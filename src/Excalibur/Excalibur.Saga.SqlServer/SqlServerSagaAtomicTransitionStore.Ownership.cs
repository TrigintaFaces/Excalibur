// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    private async Task<T> ReadOwnedAsync<T>(Func<SqlConnection,SqlTransaction,CancellationToken,Task<T>> read,CancellationToken token)
    {
        SqlTransaction? transaction=null;Exception? primary=null;
        var acknowledged=false;T result=default!;
        try
        {
            await using var connection = new SqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                transaction=(SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                result=await read(connection,transaction,token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);acknowledged=true;
            }
            catch(Exception error){primary=error;}
            finally
            {
                primary = await CleanupOwnedAsync(null, transaction, acknowledged, primary).ConfigureAwait(false);
            }
        }
        catch(Exception error){Attach(ref primary,error,"ConnectionDispose");}

        if(primary is not null){ExceptionDispatchInfo.Capture(primary).Throw();}
        return result;
    }

    private static async Task<Exception?> CleanupOwnedAsync(SqlConnection? connection,SqlTransaction? transaction,
        bool commitAcknowledged,Exception? primary)
    {
        if(transaction is not null)
        {
            if(!commitAcknowledged)
            {
                try
                {
                    using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await transaction.RollbackAsync(budget.Token).ConfigureAwait(false);
                }
                catch(Exception error){Attach(ref primary,error,"Rollback");}
            }
            try{await transaction.DisposeAsync().ConfigureAwait(false);}
            catch(Exception error){Attach(ref primary,error,"TransactionDispose");}
        }
        if(connection is not null)
        {
            try{await connection.DisposeAsync().ConfigureAwait(false);}
            catch(Exception error){Attach(ref primary,error,"ConnectionDispose");}
        }

        return primary;
    }
}
