#!/bin/bash
# Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: Apache-2.0

# Google Cloud Functions Deployment Script
# =========================================
# This script deploys the sample functions to Google Cloud Functions.
#
# Prerequisites:
# 1. Google Cloud SDK installed: https://cloud.google.com/sdk/docs/install
# 2. Authenticated: gcloud auth login
# 3. Project set: gcloud config set project YOUR_PROJECT_ID
# 4. APIs enabled:
#    - Cloud Functions API
#    - Cloud Build API
#    - Cloud Pub/Sub API
#    - Cloud Scheduler API

set -euo pipefail

# Configuration
PROJECT_ID="${GCP_PROJECT_ID:-}"
REGION="${GCP_REGION:-us-central1}"
RUNTIME="dotnet8"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v gcloud &> /dev/null; then
        log_error "gcloud CLI not found. Please install: https://cloud.google.com/sdk/docs/install"
        exit 1
    fi

    if [ -z "$PROJECT_ID" ]; then
        PROJECT_ID=$(gcloud config get-value project 2>/dev/null || echo "")
        if [ -z "$PROJECT_ID" ]; then
            log_error "No project ID set. Use: gcloud config set project YOUR_PROJECT_ID"
            log_error "Or set GCP_PROJECT_ID environment variable"
            exit 1
        fi
    fi

    log_info "Using project: $PROJECT_ID"
    log_info "Using region: $REGION"
}

# Enable required APIs
enable_apis() {
    log_info "Enabling required APIs..."

    gcloud services enable cloudfunctions.googleapis.com --project="$PROJECT_ID" || true
    gcloud services enable cloudbuild.googleapis.com --project="$PROJECT_ID" || true
    gcloud services enable pubsub.googleapis.com --project="$PROJECT_ID" || true
    gcloud services enable cloudscheduler.googleapis.com --project="$PROJECT_ID" || true
}

# Create Pub/Sub topics
create_topics() {
    log_info "Creating Pub/Sub topics..."

    gcloud pubsub topics create orders --project="$PROJECT_ID" 2>/dev/null || log_warn "Topic 'orders' already exists"
    gcloud pubsub topics create scheduled-tasks --project="$PROJECT_ID" 2>/dev/null || log_warn "Topic 'scheduled-tasks' already exists"
}

# Deploy HTTP function
deploy_http_function() {
    log_info "Deploying HTTP function..."

    gcloud functions deploy http-order-function \
        --gen2 \
        --runtime="$RUNTIME" \
        --region="$REGION" \
        --source=. \
        --entry-point=GoogleCloudFunctionsSample.Functions.HttpFunction \
        --trigger-http \
        --allow-unauthenticated \
        --memory=256MB \
        --timeout=60s \
        --min-instances=0 \
        --max-instances=10 \
        --project="$PROJECT_ID"

    # Get the function URL
    HTTP_URL=$(gcloud functions describe http-order-function \
        --gen2 \
        --region="$REGION" \
        --project="$PROJECT_ID" \
        --format='value(serviceConfig.uri)')

    log_info "HTTP function deployed: $HTTP_URL"
}

# Deploy Pub/Sub function
deploy_pubsub_function() {
    log_info "Deploying Pub/Sub function..."

    gcloud functions deploy pubsub-order-processor \
        --gen2 \
        --runtime="$RUNTIME" \
        --region="$REGION" \
        --source=. \
        --entry-point=GoogleCloudFunctionsSample.Functions.PubSubFunction \
        --trigger-topic=orders \
        --memory=256MB \
        --timeout=60s \
        --min-instances=0 \
        --max-instances=10 \
        --project="$PROJECT_ID"

    log_info "Pub/Sub function deployed"
}

# Deploy scheduled function
deploy_scheduled_function() {
    log_info "Deploying scheduled function..."

    gcloud functions deploy scheduled-task-function \
        --gen2 \
        --runtime="$RUNTIME" \
        --region="$REGION" \
        --source=. \
        --entry-point=GoogleCloudFunctionsSample.Functions.ScheduledFunction \
        --trigger-topic=scheduled-tasks \
        --memory=256MB \
        --timeout=300s \
        --min-instances=0 \
        --max-instances=5 \
        --project="$PROJECT_ID"

    log_info "Scheduled function deployed"
}

# Create Cloud Scheduler job
create_scheduler_job() {
    log_info "Creating Cloud Scheduler job..."

    # Delete existing job if it exists
    gcloud scheduler jobs delete daily-report \
        --location="$REGION" \
        --project="$PROJECT_ID" \
        --quiet 2>/dev/null || true

    gcloud scheduler jobs create pubsub daily-report \
        --schedule="0 9 * * *" \
        --topic=scheduled-tasks \
        --message-body='{"taskName":"DailyReport"}' \
        --location="$REGION" \
        --project="$PROJECT_ID"

    log_info "Cloud Scheduler job created (runs daily at 9:00 AM)"
}

# Print deployment summary
print_summary() {
    log_info "=========================================="
    log_info "Deployment Complete!"
    log_info "=========================================="
    echo ""
    echo "HTTP Function:"
    echo "  POST $HTTP_URL/orders - Create an order"
    echo "  GET  $HTTP_URL/orders/{id} - Get order by ID"
    echo ""
    echo "Test with:"
    echo "  curl -X POST $HTTP_URL/orders \\"
    echo "    -H 'Content-Type: application/json' \\"
    echo "    -d '{\"orderId\":\"ORD-001\",\"customerId\":\"CUST-100\",\"totalAmount\":99.99}'"
    echo ""
    echo "Pub/Sub Function:"
    echo "  Triggered by messages to 'orders' topic"
    echo ""
    echo "  Test with:"
    echo "  gcloud pubsub topics publish orders --message='{\"orderId\":\"ORD-002\",\"customerId\":\"CUST-200\",\"totalAmount\":149.99}'"
    echo ""
    echo "Scheduled Function:"
    echo "  Runs daily at 9:00 AM via Cloud Scheduler"
    echo ""
    echo "  Test manually with:"
    echo "  gcloud scheduler jobs run daily-report --location=$REGION"
    echo ""
}

# Cleanup function
cleanup() {
    log_info "Cleaning up deployed resources..."

    gcloud functions delete http-order-function --gen2 --region="$REGION" --project="$PROJECT_ID" --quiet || true
    gcloud functions delete pubsub-order-processor --gen2 --region="$REGION" --project="$PROJECT_ID" --quiet || true
    gcloud functions delete scheduled-task-function --gen2 --region="$REGION" --project="$PROJECT_ID" --quiet || true
    gcloud scheduler jobs delete daily-report --location="$REGION" --project="$PROJECT_ID" --quiet || true
    gcloud pubsub topics delete orders --project="$PROJECT_ID" --quiet || true
    gcloud pubsub topics delete scheduled-tasks --project="$PROJECT_ID" --quiet || true

    log_info "Cleanup complete"
}

# Main execution
main() {
    case "${1:-deploy}" in
        deploy)
            check_prerequisites
            enable_apis
            create_topics
            deploy_http_function
            deploy_pubsub_function
            deploy_scheduled_function
            create_scheduler_job
            print_summary
            ;;
        cleanup)
            check_prerequisites
            cleanup
            ;;
        http)
            check_prerequisites
            deploy_http_function
            ;;
        pubsub)
            check_prerequisites
            deploy_pubsub_function
            ;;
        scheduled)
            check_prerequisites
            deploy_scheduled_function
            ;;
        *)
            echo "Usage: $0 {deploy|cleanup|http|pubsub|scheduled}"
            echo ""
            echo "Commands:"
            echo "  deploy    - Deploy all functions (default)"
            echo "  cleanup   - Remove all deployed resources"
            echo "  http      - Deploy HTTP function only"
            echo "  pubsub    - Deploy Pub/Sub function only"
            echo "  scheduled - Deploy scheduled function only"
            exit 1
            ;;
    esac
}

main "$@"
