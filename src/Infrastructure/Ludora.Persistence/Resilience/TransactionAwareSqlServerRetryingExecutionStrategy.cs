using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ludora.Persistence.Resilience;

/// <summary>
/// Retries transient SQL Server failures for normal commands while preserving
/// existing user-managed transactions. Commands already running inside an
/// explicit or ambient transaction execute once; the transaction owner keeps
/// control of commit and rollback semantics.
/// </summary>
public sealed class TransactionAwareSqlServerRetryingExecutionStrategy
    : SqlServerRetryingExecutionStrategy
{
    public TransactionAwareSqlServerRetryingExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        int maxRetryCount,
        TimeSpan maxRetryDelay,
        IEnumerable<int>? errorNumbersToAdd = null)
        : base(
            dependencies,
            maxRetryCount,
            maxRetryDelay,
            errorNumbersToAdd)
    {
    }

    protected override void OnFirstExecution()
    {
        if (HasActiveTransaction())
        {
            ExceptionsEncountered.Clear();
            return;
        }

        base.OnFirstExecution();
    }

    protected override bool ShouldRetryOn(Exception exception) =>
        !HasActiveTransaction() && base.ShouldRetryOn(exception);

    private bool HasActiveTransaction()
    {
        var database = Dependencies.CurrentContext.Context.Database;
        return database.CurrentTransaction is not null ||
               database.GetEnlistedTransaction() is not null ||
               Transaction.Current is not null;
    }
}
