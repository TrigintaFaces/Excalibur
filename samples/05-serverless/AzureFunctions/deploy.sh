#!/bin/bash
# Azure Functions Deployment Script
# Sprint 430 S430.1: Azure Functions Serverless Sample
#
# Prerequisites:
# - Azure CLI installed: https://docs.microsoft.com/cli/azure/install-azure-cli
# - Azure Functions Core Tools: npm install -g azure-functions-core-tools@4
# - Logged into Azure: az login

set -euo pipefail

# Configuration - Update these values for your deployment
RESOURCE_GROUP="${RESOURCE_GROUP:-dispatch-serverless-rg}"
LOCATION="${LOCATION:-eastus}"
STORAGE_ACCOUNT="${STORAGE_ACCOUNT:-dispatchfuncstore}"
FUNCTION_APP="${FUNCTION_APP:-dispatch-azure-functions-sample}"
APP_INSIGHTS="${APP_INSIGHTS:-dispatch-func-insights}"

echo "=== Azure Functions Deployment ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Function App: $FUNCTION_APP"
echo ""

# Check prerequisites
command -v az >/dev/null 2>&1 || { echo "Azure CLI required. Install from https://docs.microsoft.com/cli/azure/install-azure-cli"; exit 1; }
command -v func >/dev/null 2>&1 || { echo "Azure Functions Core Tools required. Run: npm install -g azure-functions-core-tools@4"; exit 1; }

# Login check
az account show >/dev/null 2>&1 || { echo "Please login to Azure: az login"; exit 1; }

echo "Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

echo "Creating storage account..."
az storage account create \
    --name "$STORAGE_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku Standard_LRS \
    --output none

echo "Creating Application Insights..."
az monitor app-insights component create \
    --app "$APP_INSIGHTS" \
    --location "$LOCATION" \
    --resource-group "$RESOURCE_GROUP" \
    --application-type web \
    --output none

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
    --app "$APP_INSIGHTS" \
    --resource-group "$RESOURCE_GROUP" \
    --query "connectionString" -o tsv)

echo "Creating function app..."
az functionapp create \
    --name "$FUNCTION_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --storage-account "$STORAGE_ACCOUNT" \
    --consumption-plan-location "$LOCATION" \
    --runtime dotnet-isolated \
    --runtime-version 8 \
    --functions-version 4 \
    --os-type Linux \
    --output none

echo "Configuring Application Insights..."
az functionapp config appsettings set \
    --name "$FUNCTION_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=$INSTRUMENTATION_KEY" \
    --output none

echo "Building project..."
dotnet build -c Release

echo "Publishing to Azure..."
func azure functionapp publish "$FUNCTION_APP"

echo ""
echo "=== Deployment Complete ==="
echo "Function App URL: https://$FUNCTION_APP.azurewebsites.net"
echo ""
echo "Test endpoints:"
echo "  POST https://$FUNCTION_APP.azurewebsites.net/api/orders"
echo "  GET  https://$FUNCTION_APP.azurewebsites.net/api/orders/{orderId}"
echo ""
echo "View logs: az functionapp logs tail --name $FUNCTION_APP --resource-group $RESOURCE_GROUP"
