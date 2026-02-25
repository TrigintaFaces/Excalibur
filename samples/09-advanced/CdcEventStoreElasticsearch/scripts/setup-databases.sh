#!/bin/bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: Apache-2.0

# ============================================================================
# CDC + Event Store + Elasticsearch Sample - Database Setup Script
# ============================================================================
#
# This script initializes the databases for the sample:
#   1. Creates LegacyDb on SQL Server #1 (port 1433)
#   2. Enables CDC on LegacyDb
#   3. Creates LegacyCustomers table with CDC enabled
#   4. Creates EventStore database on SQL Server #2 (port 1434)
#
# Prerequisites:
#   - Docker containers must be running: docker-compose up -d
#   - Wait for containers to be healthy before running this script
#
# Usage:
#   ./setup-databases.sh
#
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SA_PASSWORD="YourStrong@Passw0rd"

echo "============================================================================"
echo "CDC + Event Store + Elasticsearch Sample - Database Setup"
echo "============================================================================"
echo ""

# Check if containers are running
echo "Checking Docker containers..."
if ! docker ps | grep -q excalibur-sqlserver-cdc; then
    echo "ERROR: SQL Server #1 (CDC Source) container is not running."
    echo "Please run: docker-compose up -d"
    exit 1
fi

if ! docker ps | grep -q excalibur-sqlserver-eventstore; then
    echo "ERROR: SQL Server #2 (Event Store) container is not running."
    echo "Please run: docker-compose up -d"
    exit 1
fi

echo "All containers are running."
echo ""

# Wait for SQL Server #1 to be ready
echo "Waiting for SQL Server #1 (CDC Source) to be ready..."
until docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; do
    echo "  Waiting..."
    sleep 2
done
echo "SQL Server #1 is ready."

# Wait for SQL Server #2 to be ready
echo "Waiting for SQL Server #2 (Event Store) to be ready..."
until docker exec excalibur-sqlserver-eventstore /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; do
    echo "  Waiting..."
    sleep 2
done
echo "SQL Server #2 is ready."
echo ""

# Setup SQL Server #1 - CDC Source
echo "============================================================================"
echo "Setting up SQL Server #1 (CDC Source)..."
echo "============================================================================"

# Create LegacyDb database
echo "Creating LegacyDb database..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C \
    -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'LegacyDb') CREATE DATABASE LegacyDb"

# Enable CDC on the database
echo "Enabling CDC on LegacyDb..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'LegacyDb' AND is_cdc_enabled = 1) EXEC sys.sp_cdc_enable_db"

# Create LegacyCustomers table
echo "Creating LegacyCustomers table..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LegacyCustomers')
        CREATE TABLE dbo.LegacyCustomers (
            CustId NVARCHAR(50) NOT NULL PRIMARY KEY,
            CustomerName NVARCHAR(200) NOT NULL,
            Email NVARCHAR(200) NULL,
            Phone NVARCHAR(50) NULL,
            Address NVARCHAR(500) NULL,
            City NVARCHAR(100) NULL,
            Country NVARCHAR(100) NULL,
            CreatedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            ModifiedDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            IsActive BIT NOT NULL DEFAULT 1,
            ExternalId AS CustId PERSISTED,
            Name AS CustomerName PERSISTED
        )"

# Enable CDC on LegacyCustomers table
echo "Enabling CDC on LegacyCustomers table..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "IF NOT EXISTS (
            SELECT 1 FROM sys.tables t
            JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
            WHERE t.name = 'LegacyCustomers'
        )
        EXEC sys.sp_cdc_enable_table
            @source_schema = N'dbo',
            @source_name = N'LegacyCustomers',
            @role_name = NULL,
            @capture_instance = N'dbo_LegacyCustomers',
            @supports_net_changes = 1"

echo "SQL Server #1 setup complete."
echo ""

# Setup SQL Server #2 - Event Store
echo "============================================================================"
echo "Setting up SQL Server #2 (Event Store)..."
echo "============================================================================"

# Create EventStore database
echo "Creating EventStore database..."
docker exec excalibur-sqlserver-eventstore /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C \
    -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'EventStore') CREATE DATABASE EventStore"

echo "SQL Server #2 setup complete."
echo "(Event store schema will be created automatically by the framework)"
echo ""

# Verify setup
echo "============================================================================"
echo "Verifying setup..."
echo "============================================================================"

echo ""
echo "CDC-enabled tables on SQL Server #1:"
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "SELECT t.name AS TableName, ct.capture_instance AS CaptureInstance
        FROM sys.tables t
        JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id" \
    -W

echo ""
echo "============================================================================"
echo "Setup complete!"
echo "============================================================================"
echo ""
echo "You can now run the sample application:"
echo "  cd .."
echo "  dotnet run"
echo ""
echo "To insert test data, run:"
echo "  ./insert-test-data.sh"
echo ""
