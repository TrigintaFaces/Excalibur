# Consumer Conformance Example

A runnable, worked example of the **Excalibur consumer testing toolkit**. It shows the two things you do
when you write your own implementation of an Excalibur contract and want to prove it is correct:

1. **Run a framework conformance kit against your own implementation** — no Docker required.
2. **Spin up a real backend with an opt-in TestContainers fixture** and run a kit against it.

Both parts use only supported public APIs from the shipped packages:

| Package | Used for |
|---------|----------|
| `Excalibur.Testing.Conformance` | the abstract `{Contract}ConformanceTestKit` base classes |
| `Excalibur.Testing.Containers` | opt-in TestContainers fixtures (`SqlServerContainerFixture`, …) |

## Run it

```bash
dotnet run                     # part (a) — runs the conformance kit against a custom retry policy
dotnet run -- --with-container # also runs part (b) — starts a real SQL Server (needs Docker)
```

## (a) Conformance against your own implementation

`SampleRetryPolicy` is a stand-in for *your* production `IDataRequestRetryPolicy`. You bind it to the
framework kit by implementing three factory hooks:

```csharp
internal sealed class MyRetryPolicyConformance : RetryPolicyConformanceTestKit
{
    protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts) => new MyRetryPolicy(maxRetryAttempts);
    protected override Exception CreateRetryableException() => new TimeoutException();
    protected override Exception CreateNonRetryableException() => new ArgumentException();
}
```

In a real test project you would add `[Fact]` methods that call the kit's checks. This sample invokes
them directly so it can run as a plain console program and print pass/fail. A failing check throws
`TestFixtureAssertionException` with a message describing the contract violation.

## (b) Conformance against a real backend

`SqlServerContainerFixture` starts a real SQL Server in Docker and exposes a `ConnectionString`. Hand
that to your store implementation and run the matching store conformance kit against it — the same kit,
now proving your implementation against the real engine:

```csharp
var fixture = new SqlServerContainerFixture();
await fixture.InitializeAsync();
var store = new MyOutboxStore(fixture.ConnectionString);
// run OutboxStoreConformanceTestKit against `store`
await fixture.DisposeAsync();
```

The container dependency lives only in `Excalibur.Testing.Containers`, so consumers who test purely
in-memory never pull a Docker dependency.
