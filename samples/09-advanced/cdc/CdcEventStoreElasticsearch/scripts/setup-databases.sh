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
SQL_FILE="$SCRIPT_DIR/setup-databases.sql"

# The schema lives in setup-databases.sql -- this script APPLIES it, it does not restate it.
# Keeping one copy is the point: the two drifted apart precisely because the shell script
# carried its own inline copy of Section 1 and simply omitted Section 2.
#
# The file is not runnable end-to-end against one instance: Section 1 targets the CDC source
# (port 1433, which also holds this sample's CDC processing-state table) and Section 2 targets
# the event store (port 1434). Each section is applied to its own server below.
SECTION2_BANNER="^-- SECTION 2: Run on SQL Server #2"

if [ ! -f "$SQL_FILE" ]; then
    echo "ERROR: cannot find $SQL_FILE -- this script applies that file and has nothing to run without it."
    exit 1
fi

if ! grep -qE "$SECTION2_BANNER" "$SQL_FILE"; then
    echo "ERROR: $SQL_FILE has no Section 2 banner, so it cannot be split per server."
    echo "       Refusing to apply it rather than sending event-store DDL to the CDC source."
    exit 1
fi

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

# Setup SQL Server #1 - CDC Source (Section 1 of setup-databases.sql)
echo "============================================================================"
echo "Setting up SQL Server #1 (CDC Source)..."
echo "============================================================================"
echo "Applying Section 1 (LegacyDb, CDC, LegacyCustomers, Cdc.CdcProcessingState)..."

awk "/$SECTION2_BANNER/ { exit } { print }" "$SQL_FILE" \
    | docker exec -i excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" -C -b -i /dev/stdin

echo "SQL Server #1 setup complete."
echo ""

# Setup SQL Server #2 - Event Store (Section 2 of setup-databases.sql)
echo "============================================================================"
echo "Setting up SQL Server #2 (Event Store)..."
echo "============================================================================"
echo "Applying Section 2 (EventStore database and tables)..."

# The framework does NOT create these tables. Excalibur.EventSourcing.SqlServer ships DDL under
# its Scripts/ folder and executes none of it at runtime, so if this step is skipped the
# application starts and then fails on its first append with "Invalid object name".
awk "/$SECTION2_BANNER/ { f = 1 } f { print }" "$SQL_FILE" \
    | docker exec -i excalibur-sqlserver-eventstore /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" -C -b -i /dev/stdin

echo "SQL Server #2 setup complete."
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
