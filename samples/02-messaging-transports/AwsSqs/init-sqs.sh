#!/bin/bash
# Initialize SQS queues for the sample

echo "Creating SQS queues..."

# Create the main orders queue
awslocal sqs create-queue --queue-name dispatch-orders

# Create a dead-letter queue for failed messages
awslocal sqs create-queue --queue-name dispatch-orders-dlq

# Configure dead-letter queue on main queue
awslocal sqs set-queue-attributes \
    --queue-url http://localhost:4566/000000000000/dispatch-orders \
    --attributes '{
        "RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:000000000000:dispatch-orders-dlq\",\"maxReceiveCount\":\"3\"}"
    }'

echo "SQS queues created successfully!"
awslocal sqs list-queues
