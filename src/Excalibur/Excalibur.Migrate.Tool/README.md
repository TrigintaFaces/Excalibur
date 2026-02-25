# Excalibur.Migrate.Tool

CLI tool for managing Excalibur event sourcing database schema migrations.

## Installation

```bash
dotnet tool install --global Excalibur.Migrate.Tool
```

## Usage

### Apply all pending migrations

```bash
excalibur-migrate up --provider sqlserver --connection "Server=.;Database=MyDb;..."
excalibur-migrate up --provider postgres --connection "Host=localhost;Database=mydb;..."
```

### Rollback to a specific version

```bash
excalibur-migrate down --to 20260101120000 --provider sqlserver --connection "..."
```

### Show migration status

```bash
excalibur-migrate status --provider sqlserver --connection "..."
```

### Generate SQL script (without applying)

```bash
excalibur-migrate script --output migrations.sql --provider sqlserver --connection "..."
```

## Options

| Option | Description |
|--------|-------------|
| `--provider`, `-p` | Database provider: `sqlserver` or `postgres` |
| `--connection`, `-c` | Connection string |
| `--assembly`, `-a` | Assembly containing migration scripts |
| `--namespace`, `-n` | Namespace prefix for migration resources |
| `--verbose`, `-v` | Enable verbose output |

## Configuration File

You can also use an `excalibur-migrate.json` configuration file:

```json
{
  "provider": "sqlserver",
  "connectionString": "Server=.;Database=MyDb;Trusted_Connection=True;",
  "migrationAssembly": "MyApp.Migrations.dll",
  "migrationNamespace": "MyApp.Migrations"
}
```
