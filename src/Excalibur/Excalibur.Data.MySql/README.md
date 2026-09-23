# Excalibur.Data.MySql

MySQL/MariaDB database provider implementation for the Excalibur data access layer.

## Features

- `MySqlPersistenceProvider` implementing `IPersistenceProvider`, `IPersistenceProviderHealth`, `IPersistenceProviderTransaction`, `IPersistenceProviderConnection`, `IDataRequestExecutor`
- Transient error retry policy with exponential backoff (error codes 1040, 1205, 1213, 2002, 2003, 2006, 2013)
- Connection pooling via MySqlConnector
- Health check and metrics support
- Transaction scope support

## Usage

```csharp
services.AddExcaliburMySql(options =>
{
    options.ConnectionString = "Server=localhost;Database=mydb;User=root;Password=secret;";
    options.CommandTimeout = 30;
    options.MaxRetryCount = 3;
});
```

## Connection string settings this package applies for you

The provider builds its connections from the connection string you supply, and overrides a small number
of settings. Most are ordinary configuration — pool sizes, timeouts, application name. **One deserves an
explicit note, because it disables a driver safety check.**

### `IgnoreCommandTransaction=true`

MySqlConnector requires, by default, that every `MySqlCommand.Transaction` equals the connection's active
transaction, and rejects a command whose `Transaction` is null while one is pending. That check exists to
catch a real class of bug: passing the wrong transaction, or a disposed one.

This provider turns it off, and the reason is structural rather than convenient. Data access here goes
through request objects that build their own commands from a **connection** — the resolver signature has
no transaction parameter — so there is no seam through which the provider could set the property the
check demands. In this design the check can only ever fire as a false alarm: it guards against supplying
the *wrong* transaction, and supplying *any* transaction is not expressible. Without the override, no
request that runs SQL inside a batch can execute at all.

It also makes MySQL behave the way the SQL Server and PostgreSQL providers already do. Their drivers
associate a command with the connection's pending transaction on their own, which is why the same code
works on those providers and not on this one.

**What this means for you.** Transactional correctness is unaffected — a batch still commits or rolls
back as one unit, and `ExecuteBatchInTransactionAsync` still enlists in the scope you pass it. What you
lose is the driver's ability to *tell you* that a command was built with the wrong transaction, and that
diagnostic was never reachable through this API in the first place. If you use `MySqlConnection` directly,
outside this provider, your own connections are unaffected: this setting applies only to connections the
provider builds.

## Dependencies

- [MySqlConnector](https://mysqlconnector.net/) — async MySQL/MariaDB driver
- [Dapper](https://github.com/DapperLib/Dapper) — micro-ORM
- [Polly](https://github.com/App-vNext/Polly) — resilience and transient fault handling

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
