# Excalibur.Data.MySql

MySQL/MariaDB database provider implementation for the Excalibur data access layer.

## Features

- `MySqlPersistenceProvider` implementing `IPersistenceProvider`, `IPersistenceProviderHealth`, `IPersistenceProviderTransaction`
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

## Dependencies

- [MySqlConnector](https://mysqlconnector.net/) — async MySQL/MariaDB driver
- [Dapper](https://github.com/DapperLib/Dapper) — micro-ORM
- [Polly](https://github.com/App-vNext/Polly) — resilience and transient fault handling
