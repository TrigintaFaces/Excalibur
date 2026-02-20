#!/bin/bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: Apache-2.0

# ============================================================================
# CDC + Event Store + Elasticsearch Sample - Insert Test Data
# ============================================================================
#
# This script inserts sample customer data into the LegacyCustomers table
# to generate CDC changes that will be processed by the sample application.
#
# Prerequisites:
#   - Run setup-databases.sh first
#   - Sample application should be running to observe CDC processing
#
# Usage:
#   ./insert-test-data.sh
#
# ============================================================================

set -e

SA_PASSWORD="YourStrong@Passw0rd"

echo "============================================================================"
echo "Inserting test data into LegacyCustomers..."
echo "============================================================================"
echo ""

# Insert sample customers
echo "Inserting 5 sample customers..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "INSERT INTO dbo.LegacyCustomers (CustId, CustomerName, Email, Phone, City, Country)
        VALUES
            ('CUST-001', 'Alice Johnson', 'alice@example.com', '+1-555-0101', 'New York', 'USA'),
            ('CUST-002', 'Bob Smith', 'bob@example.com', '+1-555-0102', 'Los Angeles', 'USA'),
            ('CUST-003', 'Carol Davis', 'carol@example.com', '+1-555-0103', 'Chicago', 'USA'),
            ('CUST-004', 'David Lee', 'david@example.com', '+1-555-0104', 'Houston', 'USA'),
            ('CUST-005', 'Eve Wilson', 'eve@example.com', '+1-555-0105', 'Phoenix', 'USA')"

echo "  Inserted: Alice Johnson (CUST-001)"
echo "  Inserted: Bob Smith (CUST-002)"
echo "  Inserted: Carol Davis (CUST-003)"
echo "  Inserted: David Lee (CUST-004)"
echo "  Inserted: Eve Wilson (CUST-005)"
echo ""

# Wait a moment for CDC to capture the inserts
echo "Waiting for CDC to capture inserts..."
sleep 3

# Update a customer
echo "Updating Alice Johnson..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "UPDATE dbo.LegacyCustomers
        SET CustomerName = 'Alice M. Johnson', Email = 'alice.johnson@example.com', ModifiedDate = SYSUTCDATETIME()
        WHERE CustId = 'CUST-001'"
echo "  Updated: Alice M. Johnson (CUST-001)"
echo ""

# Wait a moment
echo "Waiting for CDC to capture update..."
sleep 3

# Update Bob
echo "Updating Bob Smith..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "UPDATE dbo.LegacyCustomers
        SET CustomerName = 'Robert Smith', Email = 'bob.smith@example.com', ModifiedDate = SYSUTCDATETIME()
        WHERE CustId = 'CUST-002'"
echo "  Updated: Robert Smith (CUST-002)"
echo ""

# Wait a moment
echo "Waiting for CDC to capture update..."
sleep 3

# Delete Eve (soft delete scenario - the ACL will handle this as deactivation)
echo "Deleting Eve Wilson..."
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "DELETE FROM dbo.LegacyCustomers WHERE CustId = 'CUST-005'"
echo "  Deleted: Eve Wilson (CUST-005)"
echo ""

echo "============================================================================"
echo "Test data insertion complete!"
echo "============================================================================"
echo ""
echo "The sample application should now process these CDC changes:"
echo "  - 5 INSERT events (customer creation)"
echo "  - 2 UPDATE events (Alice and Bob modified)"
echo "  - 1 DELETE event (Eve deleted)"
echo ""
echo "Check the application logs and Elasticsearch for results."
echo ""

# Show current state
echo "Current customers in database:"
docker exec excalibur-sqlserver-cdc /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -C -d LegacyDb \
    -Q "SELECT CustId, CustomerName, Email, IsActive FROM dbo.LegacyCustomers" \
    -W
