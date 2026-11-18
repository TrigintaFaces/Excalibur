using PrimarySqlAtomicSaga;

var connection = Environment.GetEnvironmentVariable("SAGA_SQL_CONNECTION")
    ?? throw new InvalidOperationException("Set SAGA_SQL_CONNECTION to an already migrated dedicated SQL database.");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var recoveryDemo = args.Contains("--timeout-recovery-demo", StringComparer.Ordinal);
await using var provider = SampleConfiguration.Create(connection, recoveryDemo);
if (recoveryDemo)
{
    await TimeoutRecoveryDemo.RunAsync(provider, deadline.Token);
}
else
{
    await SampleWorkflow.RunAsync(provider, deadline.Token);
}
