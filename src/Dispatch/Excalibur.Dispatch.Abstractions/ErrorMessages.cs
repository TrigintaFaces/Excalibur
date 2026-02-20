// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides centralized error message constants used throughout the Excalibur framework.
/// </summary>
public static class ErrorConstants
{
	/// <summary>
	/// Error message: Access denied for message type {MessageType}.
	/// </summary>
	public const string AccessDeniedForMessageType = "Access denied for message type {MessageType}.";

	/// <summary>
	/// Error message: Authentication failed for message type {MessageType}.
	/// </summary>
	public const string AuthenticationFailedForMessageType = "Authentication failed for message type {MessageType}.";

	/// <summary>
	/// Error message: Authentication required but no token found for message type {MessageType}.
	/// </summary>
	public const string
		AuthenticationRequiredButNoTokenFound = "Authentication required but no token found for message type {MessageType}.";

	/// <summary>
	/// Error message: Authorization failed for message type {MessageType}.
	/// </summary>
	public const string AuthorizationFailedForMessage = "Authorization failed for message type {MessageType}.";

	/// <summary>
	/// Error message: Authorization succeeded for message type {MessageType}.
	/// </summary>
	public const string AuthorizationSucceededForMessage = "Authorization succeeded for message type {MessageType}.";

	/// <summary>
	/// Error message: Evaluating authorization for message type {MessageType}.
	/// </summary>
	public const string EvaluatingAuthorizationForMessage = "Evaluating authorization for message type {MessageType}.";

	/// <summary>
	/// Error message: Exception occurred during authorization evaluation for message type {MessageType}.
	/// </summary>
	public const string
		ExceptionDuringAuthorizationEvaluation = "Exception occurred during authorization evaluation for message type {MessageType}.";

	/// <summary>
	/// Error message: Message type {MessageType} allows anonymous access.
	/// </summary>
	public const string MessageTypeAllowsAnonymousAccess = "Message type {MessageType} allows anonymous access.";

	/// <summary>
	/// Error message: No authenticated subject found for message type {MessageType}.
	/// </summary>
	public const string NoAuthenticatedSubjectFound = "No authenticated subject found for message type {MessageType}.";

	/// <summary>
	/// Error message: No authentication token found for message type {MessageType}; allowing anonymous access.
	/// </summary>
	public const string
		NoAuthenticationTokenFoundAllowingAnonymousAccess =
			"No authentication token found for message type {MessageType}; allowing anonymous access.";

	/// <summary>
	/// Error message: No client certificate found in context.
	/// </summary>
	public const string NoClientCertificateFoundInContext = "No client certificate found in context.";

	/// <summary>
	/// Error message: Successfully authenticated principal {Principal} for message type {MessageType}.
	/// </summary>
	public const string
		SuccessfullyAuthenticatedPrincipalForMessage =
			"Successfully authenticated principal {Principal} for message type {MessageType}.";

	/// <summary>
	/// Error message: Unexpected error occurred during authentication for message type {MessageType}.
	/// </summary>
	public const string
		UnexpectedErrorDuringAuthentication = "Unexpected error occurred during authentication for message type {MessageType}.";

	/// <summary>
	/// Error message: Using cached authentication for token.
	/// </summary>
	public const string UsingCachedAuthenticationForToken = "Using cached authentication for token.";

	/// <summary>
	/// Error message: Acquisition timeout must be positive.
	/// </summary>
	public const string AcquisitionTimeoutMustBePositive = "Acquisition timeout must be positive.";

	/// <summary>
	/// Error message: Argument must be positive.
	/// </summary>
	public const string ArgumentMustBePositive = "Argument must be positive.";

	/// <summary>
	/// Error message: At least one branch must be specified.
	/// </summary>
	public const string AtLeastOneBranchMustBeSpecified = "At least one branch must be specified.";

	/// <summary>
	/// Error message: At least one bucket boundary is required.
	/// </summary>
	public const string
		AtLeastOneBucketBoundaryIsRequired = "At least one bucket boundary is required.";

	/// <summary>
	/// Error message: At least one destination must be specified.
	/// </summary>
	public const string AtLeastOneDestinationMustBeSpecified = "At least one destination must be specified.";

	/// <summary>
	/// Error message: At least one handler is required.
	/// </summary>
	public const string AtLeastOneHandlerIsRequired = "At least one handler is required.";

	/// <summary>
	/// Error message: Batch timeout must be positive.
	/// </summary>
	public const string BatchTimeoutMustBePositive = "Batch timeout must be positive.";

	/// <summary>
	/// Error message: Batch timeout should be less than cleanup interval.
	/// </summary>
	public const string
		BatchTimeoutShouldBeLessThanCleanupInterval =
			"Batch timeout should be less than cleanup interval.";

	/// <summary>
	/// Error message: Binding name is required.
	/// </summary>
	public const string BindingNameIsRequired = "Binding name is required.";

	/// <summary>
	/// Error message: Branch evaluator must be specified.
	/// </summary>
	public const string BranchEvaluatorMustBeSpecified = "Branch evaluator must be specified.";

	/// <summary>
	/// Error message: Branch key cannot be null or whitespace.
	/// </summary>
	public const string BranchKeyCannotBeNullOrWhitespace = "Branch key cannot be null or whitespace.";

	/// <summary>
	/// Error message: Cache refresh interval must be positive.
	/// </summary>
	public const string
		CacheRefreshIntervalMustBePositive = "Cache refresh interval must be positive.";

	/// <summary>
	/// Error message: Cannot access value of faulted result.
	/// </summary>
	public const string CannotAccessValueOfFaultedResult = "Cannot access value of faulted result.";

	/// <summary>
	/// Error message: Cannot convert null to Money.
	/// </summary>
	public const string CannotConvertNullToMoney = "Cannot convert null to Money.";

	/// <summary>
	/// Error message: Cannot convert type to Money.
	/// </summary>
	public const string CannotConvertTypeToMoney = "Cannot convert type to Money.";

	/// <summary>
	/// Error message: Cannot deserialize content without serializer.
	/// </summary>
	public const string
		CannotDeserializeContentWithoutSerializer = "Cannot deserialize content without serializer.";

	/// <summary>
	/// Error message: Cannot process message without identifier; skipping inbox processing.
	/// </summary>
	public const string CannotProcessMessageWithoutIdentifierSkippingInboxProcessing =
		"Cannot process message without identifier; skipping inbox processing.";

	/// <summary>
	/// Error message: Cannot replay message: not a Dispatch message.
	/// </summary>
	public const string
		CannotReplayMessageNotDispatchMessage = "Cannot replay message: not a Dispatch message.";

	/// <summary>
	/// Error message: Cannot replay message: type not found.
	/// </summary>
	public const string CannotReplayMessageTypeNotFound = "Cannot replay message: type not found.";

	/// <summary>
	/// Error message: Cannot schedule message in the past.
	/// </summary>
	public const string CannotScheduleMessageInPast = "Cannot schedule message in the past.";

	/// <summary>
	/// Error message: Cannot schedule message in the past.
	/// </summary>
	public const string CannotScheduleMessageInThePast = "Cannot schedule message in the past.";

	/// <summary>
	/// Error message: Cannot schedule messages too far in the future.
	/// </summary>
	public const string CannotScheduleMessagesTooFarInFuture = "Cannot schedule messages too far in the future.";

	/// <summary>
	/// Error message: Cannot start processing: no valid minimum LSN found.
	/// </summary>
	public const string CannotStartProcessingNoValidMinimumLsnFound = "Cannot start processing: no valid minimum LSN found.";

	/// <summary>
	/// Error message: Capacity must be at least one.
	/// </summary>
	public const string CapacityMustBeAtLeastOne = "Capacity must be at least one.";

	/// <summary>
	/// Error message: Capacity must be at least two.
	/// </summary>
	public const string CapacityMustBeAtLeastTwo = "Capacity must be at least two.";

	/// <summary>
	/// Error message: Cleanup interval must be positive.
	/// </summary>
	public const string CleanupIntervalMustBePositive = "Cleanup interval must be positive.";

	/// <summary>
	/// Error message: Command timeout must be greater than zero.
	/// </summary>
	public const string CommandTimeoutMustBeGreaterThanZero = "Command timeout must be greater than zero.";

	/// <summary>
	/// Error message: Completed schedule retention must be positive.
	/// </summary>
	public const string CompletedScheduleRetentionMustBePositive = "Completed schedule retention must be positive.";

	/// <summary>
	/// Error message: Condition must be specified.
	/// </summary>
	public const string ConditionMustBeSpecified = "Condition must be specified.";

	/// <summary>
	/// Error message: Connection pool not available.
	/// </summary>
	public const string ConnectionPoolNotAvailable = "Connection pool not available.";

	/// <summary>
	/// Error message: Connection string required.
	/// </summary>
	public const string ConnectionStringRequired = "Connection string required.";

	/// <summary>
	/// Error message: Connection string required for SQL Server.
	/// </summary>
	public const string ConnectionStringRequiredForSqlServer = "Connection string required for SQL Server.";

	/// <summary>
	/// Error message: Connection timeout must be greater than zero.
	/// </summary>
	public const string ConnectionTimeoutMustBeGreaterThanZero = "Connection timeout must be greater than zero.";

	/// <summary>
	/// Error message: Connect retry count must be between 0 and 255.
	/// </summary>
	public const string ConnectRetryCountMustBeBetween0And255 = "Connect retry count must be between 0 and 255.";

	/// <summary>
	/// Error message: Connect retry interval must be between 1 and 60 seconds.
	/// </summary>
	public const string ConnectRetryIntervalMustBeBetween1And60Seconds = "Connect retry interval must be between 1 and 60 seconds.";

	/// <summary>
	/// Error message: Content deduplication window must be positive.
	/// </summary>
	public const string ContentDeduplicationWindowMustBePositive = "Content deduplication window must be positive.";

	/// <summary>
	/// Error message: Contention threshold must be positive.
	/// </summary>
	public const string ContentionThresholdMustBePositive = "Contention threshold must be positive.";

	/// <summary>
	/// Error message: Context is required.
	/// </summary>
	public const string ContextIsRequired = "Context is required.";

	/// <summary>
	/// Error message: Context not found or invalid.
	/// </summary>
	public const string ContextNotFoundOrInvalid = "Context not found or invalid.";

	/// <summary>
	/// Error message: Counter can only increase.
	/// </summary>
	public const string CounterCanOnlyIncrease = "Counter can only increase.";

	/// <summary>
	/// Error message: Count must be positive.
	/// </summary>
	public const string CountMustBePositive = "Count must be positive.";

	/// <summary>
	/// Error message: Custom validation failed.
	/// </summary>
	public const string CustomValidationFailed = "Custom validation failed.";

	/// <summary>
	/// Error message: Validation failed.
	/// </summary>
	public const string ValidationFailed = "Validation failed.";

	/// <summary>
	/// Error message: Validation failed for message type.
	/// </summary>
	public const string ValidationFailedForMessageType = "Validation failed for message type.";

	/// <summary>
	/// Error message: Database name required.
	/// </summary>
	public const string DatabaseNameRequired = "Database name required.";

	/// <summary>
	/// Error message: Delay must be positive.
	/// </summary>
	public const string DelayMustBePositive = "Delay must be positive.";

	/// <summary>
	/// Error message: Delivery count cannot be negative.
	/// </summary>
	public const string DeliveryCountCannotBeNegative = "Delivery count cannot be negative.";

	/// <summary>
	/// Error message: Deserialization timeout should be less than dispatch timeout.
	/// </summary>
	public const string DeserializationTimeoutShouldBeLessThanDispatchTimeout =
		"Deserialization timeout should be less than dispatch timeout.";

	/// <summary>
	/// Error message: Deserialized message cannot be null.
	/// </summary>
	public const string DeserializedMessageCannotBeNull = "Deserialized message cannot be null.";

	/// <summary>
	/// Error message: Destination buffer too small.
	/// </summary>
	public const string DestinationBufferTooSmall = "Destination buffer too small.";

	/// <summary>
	/// Error message: Dispatch builder does not support pipeline profiles configuration.
	/// </summary>
	public const string DispatchBuilderDoesNotSupportPipelineProfilesConfiguration =
		"Dispatch builder does not support pipeline profiles configuration.";

	/// <summary>
	/// Error message: Dispatch timeout should be less than max scheduling timeout.
	/// </summary>
	public const string DispatchTimeoutShouldBeLessThanMaxSchedulingTimeout =
		"Dispatch timeout should be less than max scheduling timeout.";

	/// <summary>
	/// Error message: Duplicate detection window must be positive.
	/// </summary>
	public const string DuplicateDetectionWindowMustBePositive = "Duplicate detection window must be positive.";

	/// <summary>
	/// Error message: Endpoint pattern is required.
	/// </summary>
	public const string EndpointPatternIsRequired = "Endpoint pattern is required.";

	/// <summary>
	/// Error message: Event version cannot be negative.
	/// </summary>
	public const string EventVersionCannotBeNegative = "Event version cannot be negative.";

	/// <summary>
	/// Error message: Event version is incompatible.
	/// </summary>
	public const string EventVersionIsIncompatible = "Event version is incompatible.";

	/// <summary>
	/// Error message: Event version not specified and explicit versions required.
	/// </summary>
	public const string EventVersionNotSpecifiedAndExplicitVersionsRequired = "Event version not specified and explicit versions required.";

	/// <summary>
	/// Error message: Execution window buffer cannot be negative.
	/// </summary>
	public const string ExecutionWindowBufferCannotBeNegative = "Execution window buffer cannot be negative.";

	/// <summary>
	/// Error message: Export timeout must be positive.
	/// </summary>
	public const string ExportTimeoutMustBePositive = "Export timeout must be positive.";

	/// <summary>
	/// Error message: Factor must be greater than one.
	/// </summary>
	public const string FactorMustBeGreaterThanOne = "Factor must be greater than one.";

	/// <summary>
	/// Error message: Failed schedule retention must be positive.
	/// </summary>
	public const string FailedScheduleRetentionMustBePositive = "Failed schedule retention must be positive.";

	/// <summary>
	/// Error message: Global position cannot be negative.
	/// </summary>
	public const string GlobalPositionCannotBeNegative = "Global position cannot be negative.";

	/// <summary>
	/// Error message: Handler type does not specify generic type.
	/// </summary>
	public const string HandlerTypeDoesNotSpecifyGenericType = "Handler type does not specify generic type.";

	/// <summary>
	/// Error message: Health check interval must be positive.
	/// </summary>
	public const string HealthCheckIntervalMustBePositive = "Health check interval must be positive.";

	/// <summary>
	/// Error message: Idle timeout must be positive.
	/// </summary>
	public const string IdleTimeoutMustBePositive = "Idle timeout must be positive.";

	/// <summary>
	/// Error message: Initial retry delay must be positive.
	/// </summary>
	public const string InitialRetryDelayMustBePositive = "Initial retry delay must be positive.";

	/// <summary>
	/// Error message: Insufficient data for batch header.
	/// </summary>
	public const string InsufficientDataForBatchHeader = "Insufficient data for batch header.";

	/// <summary>
	/// Error message: Invalid API key format.
	/// </summary>
	public const string InvalidApiKeyFormat = "Invalid API key format.";

	/// <summary>
	/// Error message: Invalid document request for MongoDB provider.
	/// </summary>
	public const string InvalidDocumentRequestForMongoDbProvider = "Invalid document request for MongoDB provider.";

	/// <summary>
	/// Error message: Job does not implement interface.
	/// </summary>
	public const string JobDoesNotImplementInterface = "Job does not implement interface.";

	/// <summary>
	/// Error message: Job type not found or invalid.
	/// </summary>
	public const string JobTypeNotFoundOrInvalid = "Job type not found or invalid.";

	/// <summary>
	/// Error message: Lambda context must implement ILambdaContext.
	/// </summary>
	public const string LambdaContextMustImplementILambdaContext = "Lambda context must implement ILambdaContext.";

	/// <summary>
	/// Error message: Max delivery count must be positive.
	/// </summary>
	public const string MaxDeliveryCountMustBePositive = "Max delivery count must be positive.";

	/// <summary>
	/// Error message: Maximum connection lifetime must be positive.
	/// </summary>
	public const string MaximumConnectionLifetimeMustBePositive = "Maximum connection lifetime must be positive.";

	/// <summary>
	/// Error message: Maximum connections must be positive.
	/// </summary>
	public const string MaximumConnectionsMustBePositive = "Maximum connections must be positive.";

	/// <summary>
	/// Error message: Maximum future schedule age must be positive.
	/// </summary>
	public const string MaximumFutureScheduleAgeMustBePositive = "Maximum future schedule age must be positive.";

	/// <summary>
	/// Error message: Maximum retry attempts cannot be negative.
	/// </summary>
	public const string MaximumRetryAttemptsCannotBeNegative = "Maximum retry attempts cannot be negative.";

	/// <summary>
	/// Error message: Maximum retry delay must be greater than initial retry delay.
	/// </summary>
	public const string MaximumRetryDelayMustBeGreaterThanInitialRetryDelay =
		"Maximum retry delay must be greater than initial retry delay.";

	/// <summary>
	/// Error message: Max pool size must be greater than zero.
	/// </summary>
	public const string MaxPoolSizeMustBeGreaterThanZero = "Max pool size must be greater than zero.";

	/// <summary>
	/// Error message: Max retry attempts cannot be negative.
	/// </summary>
	public const string MaxRetryAttemptsCannotBeNegative = "Max retry attempts cannot be negative.";

	/// <summary>
	/// Error message: Max spin count must be positive.
	/// </summary>
	public const string MaxSpinCountMustBePositive = "Max spin count must be positive.";

	/// <summary>
	/// Error message: Message bus already registered.
	/// </summary>
	public const string MessageBusAlreadyRegistered = "Message bus already registered.";

	/// <summary>
	/// Error message: Message bus is not connected.
	/// </summary>
	public const string MessageBusIsNotConnected = "Message bus is not connected.";

	/// <summary>
	/// Error message: Message cannot be null.
	/// </summary>
	public const string MessageCannotBeNull = "Message cannot be null.";

	/// <summary>
	/// Error message: Message ID is required.
	/// </summary>
	public const string MessageIdIsRequired = "Message ID is required.";

	/// <summary>
	/// Error message: Message is required.
	/// </summary>
	public const string MessageIsRequired = "Message is required.";

	/// <summary>
	/// Error message: Messages and contexts must have same count.
	/// </summary>
	public const string MessagesAndContextsMustHaveSameCount = "Messages and contexts must have same count.";

	/// <summary>
	/// Error message: Message type is required.
	/// </summary>
	public const string MessageTypeIsRequired = "Message type is required.";

	/// <summary>
	/// Error message: Message type must implement IDispatchMessage.
	/// </summary>
	public const string MessageTypeMustImplementIDispatchMessage = "Message type must implement IDispatchMessage.";

	/// <summary>
	/// Error message: Message type timeout cannot exceed max scheduling timeout.
	/// </summary>
	public const string MessageTypeTimeoutCannotExceedMaxSchedulingTimeout = "Message type timeout cannot exceed max scheduling timeout.";

	/// <summary>
	/// Error message: Message type timeout must be positive.
	/// </summary>
	public const string MessageTypeTimeoutMustBePositive = "Message type timeout must be positive.";

	/// <summary>
	/// Error message: Metadata must be for counter type.
	/// </summary>
	public const string MetadataMustBeForCounterType = "Metadata must be for counter type.";

	/// <summary>
	/// Error message: Minimum connections cannot be negative.
	/// </summary>
	public const string MinimumConnectionsCannotBeNegative = "Minimum connections cannot be negative.";

	/// <summary>
	/// Error message: Minimum connections cannot exceed maximum.
	/// </summary>
	public const string MinimumConnectionsCannotExceedMaximum = "Minimum connections cannot exceed maximum.";

	/// <summary>
	/// Error message: Minimum length cannot be negative.
	/// </summary>
	public const string MinimumLengthCannotBeNegative = "Minimum length cannot be negative.";

	/// <summary>
	/// Error message: Min pool size cannot be greater than max pool size.
	/// </summary>
	public const string MinPoolSizeCannotBeGreaterThanMaxPoolSize = "Min pool size cannot be greater than max pool size.";

	/// <summary>
	/// Error message: Min pool size cannot be negative.
	/// </summary>
	public const string MinPoolSizeCannotBeNegative = "Min pool size cannot be negative.";

	/// <summary>
	/// Error message: Missing required fields.
	/// </summary>
	public const string MissingRequiredFields = "Missing required fields.";

	/// <summary>
	/// Error message: MongoDB client not initialized.
	/// </summary>
	public const string MongoDbClientNotInitialized = "MongoDB client not initialized.";

	/// <summary>
	/// Error message: MongoDB database not initialized.
	/// </summary>
	public const string MongoDbDatabaseNotInitialized = "MongoDB database not initialized.";

	/// <summary>
	/// Error message: MongoDB provider not initialized.
	/// </summary>
	public const string MongoDbProviderNotInitialized = "MongoDB provider not initialized.";

	/// <summary>
	/// Error message: Nested transaction scopes not directly supported.
	/// </summary>
	public const string NestedTransactionScopesNotDirectlySupported = "Nested transaction scopes not directly supported.";

	/// <summary>
	/// Error message: New maximum size must be positive.
	/// </summary>
	public const string NewMaximumSizeMustBePositive = "New maximum size must be positive.";

	/// <summary>
	/// Error message: No default adapter configured.
	/// </summary>
	public const string NoDefaultAdapterConfigured = "No default adapter configured.";

	/// <summary>
	/// Error message: No health checker configured.
	/// </summary>
	public const string NoHealthCheckerConfigured = "No health checker configured.";

	/// <summary>
	/// Error message: No transport adapter configured.
	/// </summary>
	public const string NoTransportAdapterConfigured = "No transport adapter configured.";

	/// <summary>
	/// Error message: Packet size must be between 512 and 32768.
	/// </summary>
	public const string PacketSizeMustBeBetween512And32768 = "Packet size must be between 512 and 32768.";

	/// <summary>
	/// Error message: Parallelism strategy not supported.
	/// </summary>
	public const string ParallelismStrategyNotSupported = "Parallelism strategy '{0}' not supported.";

	/// <summary>
	/// Error message: Percentile must be between 0 and 100.
	/// </summary>
	public const string PercentileMustBeBetween0And100 = "Percentile must be between 0 and 100.";

	/// <summary>
	/// Error message: Pipeline profile registry not registered.
	/// </summary>
	public const string PipelineProfileRegistryNotRegistered = "Pipeline profile registry not registered.";

	/// <summary>
	/// Error message: Poll interval should be less than schedule retrieval timeout.
	/// </summary>
	public const string PollIntervalShouldBeLessThanScheduleRetrievalTimeout =
		"Poll interval should be less than schedule retrieval timeout.";

	/// <summary>
	/// Error message: Pool name cannot be null or empty.
	/// </summary>
	public const string PoolNameCannotBeNullOrEmpty = "Pool name cannot be null or empty.";

	/// <summary>
	/// Error message: Priority cannot be negative.
	/// </summary>
	public const string PriorityCannotBeNegative = "Priority cannot be negative.";

	/// <summary>
	/// Error message: Processed message retention must be positive.
	/// </summary>
	public const string ProcessedMessageRetentionMustBePositive = "Processed message retention must be positive.";

	/// <summary>
	/// Error message: Queue capacity cannot be less than producer batch size.
	/// </summary>
	public const string QueueCapacityCannotBeLessThanProducerBatchSize = "Queue capacity cannot be less than producer batch size.";

	/// <summary>
	/// Error message: Remote message bus already registered.
	/// </summary>
	public const string RemoteMessageBusAlreadyRegistered = "Remote message bus already registered.";

	/// <summary>
	/// Error message: Request services not initialized.
	/// </summary>
	public const string RequestServicesNotInitialized = "Request services not initialized.";

	/// <summary>
	/// Error message: Resource cannot be null or whitespace.
	/// </summary>
	public const string ResourceCannotBeNullOrWhitespace = "Resource cannot be null or whitespace.";

	/// <summary>
	/// Error message: Result has no value.
	/// </summary>
	public const string ResultHasNoValue = "Result has no value.";

	/// <summary>
	/// Error message: Retry delay cannot be negative.
	/// </summary>
	public const string RetryDelayCannotBeNegative = "Retry delay cannot be negative.";

	/// <summary>
	/// Error message: Retry delay milliseconds cannot be negative.
	/// </summary>
	public const string RetryDelayMillisecondsCannotBeNegative = "Retry delay milliseconds cannot be negative.";

	/// <summary>
	/// Error message: Sampling ratio must be between 0 and 1.
	/// </summary>
	public const string SamplingRatioMustBeBetween0And1 = "Sampling ratio must be between 0 and 1.";

	/// <summary>
	/// Error message: Schedule retrieval timeout should be less than max scheduling timeout.
	/// </summary>
	public const string ScheduleRetrievalTimeoutShouldBeLessThanMaxSchedulingTimeout =
		"Schedule retrieval timeout should be less than max scheduling timeout.";

	/// <summary>
	/// Error message: Schedule update timeout should be less than max scheduling timeout.
	/// </summary>
	public const string ScheduleUpdateTimeoutShouldBeLessThanMaxSchedulingTimeout =
		"Schedule update timeout should be less than max scheduling timeout.";

	/// <summary>
	/// Error message: Service name cannot be null or empty.
	/// </summary>
	public const string ServiceNameCannotBeNullOrEmpty = "Service name cannot be null or empty.";

	/// <summary>
	/// Error message: Service version cannot be null or empty.
	/// </summary>
	public const string ServiceVersionCannotBeNullOrEmpty = "Service version cannot be null or empty.";

	/// <summary>
	/// Error message: Slow operation threshold must be positive.
	/// </summary>
	public const string SlowOperationThresholdMustBePositive = "Slow operation threshold must be positive.";

	/// <summary>
	/// Error message: Source and destination must have same length.
	/// </summary>
	public const string SourceAndDestinationMustHaveSameLength = "Source and destination must have same length.";

	/// <summary>
	/// Error message: Spin count must be positive.
	/// </summary>
	public const string SpinCountMustBePositive = "Spin count must be positive.";

	/// <summary>
	/// Error message: SQL Server does not support document-based data requests.
	/// </summary>
	public const string SqlServerDoesNotSupportDocumentBasedDataRequests = "SQL Server does not support document-based data requests.";

	/// <summary>
	/// Error message: Start must be positive.
	/// </summary>
	public const string StartMustBePositive = "Start must be positive.";

	/// <summary>
	/// Error message: Start timestamp cannot be negative.
	/// </summary>
	public const string StartTimestampCannotBeNegative = "Start timestamp cannot be negative.";

	/// <summary>
	/// Error message: Table name cannot be null or empty.
	/// </summary>
	public const string TableNameCannotBeNullOrEmpty = "Table name cannot be null or empty.";

	/// <summary>
	/// Error message: Timeout escalation multiplier must be greater than one.
	/// </summary>
	public const string TimeoutEscalationMultiplierMustBeGreaterThanOne = "Timeout escalation multiplier must be greater than one.";

	/// <summary>
	/// Error message: Timeout must be positive.
	/// </summary>
	public const string TimeoutMustBePositive = "Timeout must be positive.";

	/// <summary>
	/// Error message: Transaction scope must be MongoDbTransactionScope.
	/// </summary>
	public const string TransactionScopeMustBeMongoDbTransactionScope = "Transaction scope must be MongoDbTransactionScope.";

	/// <summary>
	/// Error message: Transaction scope must be MongoDbTransactionScope&lt;T&gt;.
	/// </summary>
	public const string TransactionScopeMustBeMongoDbTransactionScopeGeneric = "Transaction scope must be MongoDbTransactionScope<T>.";

	/// <summary>
	/// Error message: Transport name is required.
	/// </summary>
	public const string TransportNameIsRequired = "Transport name is required.";

	/// <summary>
	/// Error message: Type does not implement interface.
	/// </summary>
	public const string TypeDoesNotImplementInterface = "Type does not implement interface.";

	/// <summary>
	/// Error message: Type must implement interface.
	/// </summary>
	public const string TypeMustImplementInterface = "Type must implement interface.";

	/// <summary>
	/// Error message: Type not found in registry.
	/// </summary>
	public const string TypeNotFoundInRegistry = "Type not found in registry.";

	/// <summary>
	/// Error message: Unsupported channel mode.
	/// </summary>
	public const string UnsupportedChannelMode = "Unsupported channel mode.";

	/// <summary>
	/// Error message: Use single parameter constructor for specific keys.
	/// </summary>
	public const string UseSingleParameterConstructorForSpecificKeys = "Use single parameter constructor for specific keys.";

	/// <summary>
	/// Error message: Use struct ValidationResult for failed results.
	/// </summary>
	public const string UseStructValidationResultForFailedResults = "Use struct ValidationResult for failed results.";

	/// <summary>
	/// Error message: ValueStopwatch not started.
	/// </summary>
	public const string ValueStopwatchNotStarted = "ValueStopwatch not started.";

	/// <summary>
	/// Error message: View already registered.
	/// </summary>
	public const string ViewAlreadyRegistered = "View '{0}' already registered.";

	/// <summary>
	/// Error message: View not registered.
	/// </summary>
	public const string ViewNotRegistered = "View '{0}' not registered.";

	/// <summary>
	/// Error message: Width must be positive.
	/// </summary>
	public const string WidthMustBePositive = "Width must be positive.";

	// Serverless platform support

	/// <summary>
	/// Error message: AWS Lambda support not available in current build.
	/// </summary>
	public const string AwsLambdaSupportNotAvailable = "AWS Lambda support not available in current build.";

	/// <summary>
	/// Error message: Azure Functions support not available in current build.
	/// </summary>
	public const string AzureFunctionsSupportNotAvailable = "Azure Functions support not available in current build.";

	/// <summary>
	/// Error message: Function context must be FunctionContext.
	/// </summary>
	public const string FunctionContextMustBeFunctionContext = "Function context must be FunctionContext.";

	/// <summary>
	/// Error message: Function execution timed out.
	/// </summary>
	public const string FunctionExecutionTimedOut = "Function execution timed out.";

	/// <summary>
	/// Error message: Lambda execution timed out.
	/// </summary>
	public const string LambdaExecutionTimedOut = "Lambda execution timed out.";

	/// <summary>
	/// Error message: Batch duration (ms).
	/// </summary>
	public const string BatchDurationMs = "Batch duration (ms).";

	/// <summary>
	/// Error message: Batching added.
	/// </summary>
	public const string BatchingAdded = "Batching added.";

	/// <summary>
	/// Error message: Batching batch size.
	/// </summary>
	public const string BatchingBatchSize = "Batching batch size.";

	/// <summary>
	/// Error message: Batching enabled.
	/// </summary>
	public const string BatchingEnabled = "Batching enabled.";

	/// <summary>
	/// Error message: Batching key.
	/// </summary>
	public const string BatchingKey = "Batching key.";

	/// <summary>
	/// Error message: Batching middleware invoke.
	/// </summary>
	public const string BatchingMiddlewareInvoke = "Batching middleware invoke.";

	/// <summary>
	/// Error message: Batching middleware process batch.
	/// </summary>
	public const string BatchingMiddlewareProcessBatch = "Batching middleware process batch.";

	/// <summary>
	/// Error message: Batching trigger.
	/// </summary>
	public const string BatchingTrigger = "Batching trigger.";

	/// <summary>
	/// Error message: Batch key.
	/// </summary>
	public const string BatchKey = "Batch key.";

	/// <summary>
	/// Error message: Batch processing error.
	/// </summary>
	public const string BatchProcessingError = "Batch processing error.";

	/// <summary>
	/// Error message: Batch processing failed.
	/// </summary>
	public const string BatchProcessingFailed = "Batch processing failed.";

	/// <summary>
	/// Error message: Batch size.
	/// </summary>
	public const string BatchSize = "Batch size.";

	/// <summary>
	/// Error message: Batch size limit exceeded at event.
	/// </summary>
	public const string BatchSizeLimitExceededAtEvent = "Batch size limit exceeded at event.";

	/// <summary>
	/// Error message: Batch success.
	/// </summary>
	public const string BatchSuccess = "Batch success.";

	/// <summary>
	/// Error message: Excalibur.Dispatch batching middleware.
	/// </summary>
	public const string DispatchCoreBatchingMiddleware = "Excalibur.Dispatch.Core batching middleware.";

	/// <summary>
	/// Error message: Error processing batch of items.
	/// </summary>
	public const string ErrorProcessingBatchOfItems = "Error processing batch of items.";

	/// <summary>
	/// Error message: Failed to begin batch transaction.
	/// </summary>
	public const string FailedToBeginBatchTransaction = "Failed to begin batch transaction.";

	/// <summary>
	/// Error message: Flushing expired batch.
	/// </summary>
	public const string FlushingExpiredBatch = "Flushing expired batch.";

	/// <summary>
	/// Error message: Size threshold.
	/// </summary>
	public const string SizeThreshold = "Size threshold.";

	/// <summary>
	/// Error message: CDC processor is already running.
	/// </summary>
	public const string CdcProcessorIsAlreadyRunning = "CDC processor is already running.";

	/// <summary>
	/// Error message: Corrupted fields detected.
	/// </summary>
	public const string CorruptedFieldsDetected = "Corrupted fields detected.";

	/// <summary>
	/// Error message: Circuit breaker consecutive failures.
	/// </summary>
	public const string CircuitBreakerConsecutiveFailures = "Circuit breaker consecutive failures.";

	/// <summary>
	/// Error message: Circuit breaker manual reset.
	/// </summary>
	public const string CircuitBreakerManualReset = "Circuit breaker manual reset.";

	/// <summary>
	/// Error message: Circuit breaker recovery confirmed.
	/// </summary>
	public const string CircuitBreakerRecoveryConfirmed = "Circuit breaker recovery confirmed.";

	/// <summary>
	/// Error message: Circuit breaker testing recovery.
	/// </summary>
	public const string CircuitBreakerTestingRecovery = "Circuit breaker testing recovery.";

	/// <summary>
	/// Error message: Cleaned up dead letter messages.
	/// </summary>
	public const string CleanedUpDeadLetterMessages = "Cleaned up dead letter messages.";

	/// <summary>
	/// Error message: Cleaned up expired entries from deduplicator.
	/// </summary>
	public const string CleanedUpExpiredEntriesFromDeduplicator = "Cleaned up expired entries from deduplicator.";

	/// <summary>
	/// Error message: Cleaned up old dead letter messages.
	/// </summary>
	public const string CleanedUpOldDeadLetterMessages = "Cleaned up old dead letter messages.";

	/// <summary>
	/// Error message: Cleaned up old dead letter messages (retention days).
	/// </summary>
	public const string CleanedUpOldDeadLetterMessagesRetentionDays = "Cleaned up old dead letter messages (retention days).";

	/// <summary>
	/// Error message: Cleaned up processed inbox entries.
	/// </summary>
	public const string CleanedUpProcessedInboxEntries = "Cleaned up processed inbox entries.";

	/// <summary>
	/// Error message: Error during automatic deduplicator cleanup.
	/// </summary>
	public const string ErrorDuringAutomaticDeduplicatorCleanup = "Error during automatic deduplicator cleanup.";

	/// <summary>
	/// Error message: Error during automatic inbox cleanup.
	/// </summary>
	public const string ErrorDuringAutomaticInboxCleanup = "Error during automatic inbox cleanup.";

	/// <summary>
	/// Error message: Error during poison message cleanup.
	/// </summary>
	public const string ErrorDuringPoisonMessageCleanup = "Error during poison message cleanup.";

	/// <summary>
	/// Error message: Failed to cleanup old dead letter messages.
	/// </summary>
	public const string FailedToCleanupOldDeadLetterMessages = "Failed to cleanup old dead letter messages.";

	/// <summary>
	/// Error message: Poison message auto cleanup disabled.
	/// </summary>
	public const string PoisonMessageAutoCleanupDisabled = "Poison message auto cleanup disabled.";

	/// <summary>
	/// Error message: Poison message cleanup service started.
	/// </summary>
	public const string PoisonMessageCleanupServiceStarted = "Poison message cleanup service started.";

	/// <summary>
	/// Error message: Poison message cleanup service stopped.
	/// </summary>
	public const string PoisonMessageCleanupServiceStopped = "Poison message cleanup service stopped.";

	/// <summary>
	/// Error message: Trimmed oldest entries due to max limit.
	/// </summary>
	public const string TrimmedOldestEntriesDueToMaxLimit = "Trimmed oldest entries due to max limit.";

	/// <summary>
	/// Error message: Configuration error occurred.
	/// </summary>
	public const string ConfigurationErrorOccurred = "Configuration error occurred.";

	/// <summary>
	/// Error message: Conversion from string to TAggregateKey not implemented.
	/// </summary>
	public const string ConversionFromStringToTAggregateKeyNotImplemented = "Conversion from string to TAggregateKey not implemented.";

	/// <summary>
	/// Error message: Could not locate generic AddTypeHandler method.
	/// </summary>
	public const string CouldNotLocateGenericAddTypeHandlerMethod = "Could not locate generic AddTypeHandler method.";

	/// <summary>
	/// Error message: Could not resolve job type.
	/// </summary>
	public const string CouldNotResolveJobType = "Could not resolve job type.";

	/// <summary>
	/// Error message: EventStore dispatcher initialized.
	/// </summary>
	public const string EventStoreDispatcherInitialized = "EventStore dispatcher initialized.";

	/// <summary>
	/// Error message: Initialized in-memory deduplicator with automatic cleanup.
	/// </summary>
	public const string InitializedInMemoryDeduplicatorWithAutomaticCleanup = "Initialized in-memory deduplicator with automatic cleanup.";

	/// <summary>
	/// Error message: Initialized in-memory deduplicator without automatic cleanup.
	/// </summary>
	public const string InitializedInMemoryDeduplicatorWithoutAutomaticCleanup =
		"Initialized in-memory deduplicator without automatic cleanup.";

	/// <summary>
	/// Error message: Logger is not initialized.
	/// </summary>
	public const string LoggerIsNotInitialized = "Logger is not initialized.";

	/// <summary>
	/// Error message: Mapped message kind to profile.
	/// </summary>
	public const string MappedMessageKindToProfile = "Mapped message kind to profile.";

	/// <summary>
	/// Error message: No pipeline profiles registered; synthesizing.
	/// </summary>
	public const string NoPipelineProfilesRegisteredSynthesizing = "No pipeline profiles registered; synthesizing.";

	/// <summary>
	/// Error message: Pipeline synthesis failed with errors.
	/// </summary>
	public const string PipelineSynthesisFailedWithErrors = "Pipeline synthesis failed with errors.";

	/// <summary>
	/// Error message: Pipeline synthesis warning.
	/// </summary>
	public const string PipelineSynthesisWarning = "Pipeline synthesis warning.";

	/// <summary>
	/// Error message: Registered synthesized profile.
	/// </summary>
	public const string RegisteredSynthesizedProfile = "Registered synthesized profile.";

	/// <summary>
	/// Error message: Connection failed.
	/// </summary>
	public const string ConnectionFailed = "Connection failed.";

	/// <summary>
	/// Error message: Connection test failed.
	/// </summary>
	public const string ConnectionTestFailed = "Connection test failed.";

	/// <summary>
	/// Error message: Dead letter message not found for replay.
	/// </summary>
	public const string DeadLetterMessageNotFoundForReplay = "Dead letter message not found for replay.";

	/// <summary>
	/// Error message: Deleted dead letter message.
	/// </summary>
	public const string DeletedDeadLetterMessage = "Deleted dead letter message.";

	/// <summary>
	/// Error message: Error replaying dead letter message.
	/// </summary>
	public const string ErrorReplayingDeadLetterMessage = "Error replaying dead letter message.";

	/// <summary>
	/// Error message: Failed to replay dead letter message.
	/// </summary>
	public const string FailedToReplayDeadLetterMessage = "Failed to replay dead letter message.";

	/// <summary>
	/// Error message: Marked dead letter message as replayed.
	/// </summary>
	public const string MarkedDeadLetterMessageAsReplayed = "Marked dead letter message as replayed.";

	/// <summary>
	/// Error message: Max retries reached; moved to dead letter.
	/// </summary>
	public const string MaxRetriesReachedMovedToDeadLetter = "Max retries reached; moved to dead letter.";

	/// <summary>
	/// Error message: Message moved to dead letter queue; max retries exceeded.
	/// </summary>
	public const string MessageMovedToDeadLetterQueueMaxRetriesExceeded = "Message moved to dead letter queue; max retries exceeded.";

	/// <summary>
	/// Error message: Message moved to dead letter queue with reason.
	/// </summary>
	public const string MessageMovedToDeadLetterQueueWithReason = "Message moved to dead letter queue with reason.";

	/// <summary>
	/// Error message: Stored dead letter message.
	/// </summary>
	public const string StoredDeadLetterMessage = "Stored dead letter message.";

	/// <summary>
	/// Error message: Successfully replayed dead letter message.
	/// </summary>
	public const string SuccessfullyReplayedDeadLetterMessage = "Successfully replayed dead letter message.";

	/// <summary>
	/// Error message: Attempted to mark already processed message as processed.
	/// </summary>
	public const string AttemptedToMarkAlreadyProcessedMessageAsProcessed = "Attempted to mark already processed message as processed.";

	/// <summary>
	/// Error message: Attempted to mark non-existent message as failed.
	/// </summary>
	public const string AttemptedToMarkNonExistentMessageAsFailed = "Attempted to mark non-existent message as failed.";

	/// <summary>
	/// Error message: Attempted to mark non-existent message as processed.
	/// </summary>
	public const string AttemptedToMarkNonExistentMessageAsProcessed = "Attempted to mark non-existent message as processed.";

	/// <summary>
	/// Error message: Created inbox entry for message {MessageId} of type {MessageType} with correlation {CorrelationId}.
	/// </summary>
	public const string CreatedInboxEntryForMessage = "Created inbox entry for message {MessageId} of type {MessageType} with correlation {CorrelationId}.";

	/// <summary>
	/// Error message: Exception occurred during inbox processing for message.
	/// </summary>
	public const string ExceptionOccurredDuringInboxProcessingForMessage = "Exception occurred during inbox processing for message.";

	/// <summary>
	/// Error message: Inbox entry with message ID already exists.
	/// </summary>
	public const string InboxEntryWithMessageIdAlreadyExists = "Inbox entry with message ID already exists.";

	/// <summary>
	/// Error message: Inbox middleware enabled but neither store nor deduplicator registered.
	/// </summary>
	public const string InboxMiddlewareEnabledButNeitherStoreNorDeduplicatorRegistered =
		"Inbox middleware enabled but neither store nor deduplicator registered.";

	/// <summary>
	/// Error message: In-memory deduplicator disposed.
	/// </summary>
	public const string InMemoryDeduplicatorDisposed = "In-memory deduplicator disposed.";

	/// <summary>
	/// Error message: In-memory inbox store disposed.
	/// </summary>
	public const string InMemoryInboxStoreDisposed = "In-memory inbox store disposed.";

	/// <summary>
	/// Error message: Message has already been processed; skipping.
	/// </summary>
	public const string MessageHasAlreadyBeenProcessedSkipping = "Message has already been processed; skipping.";

	/// <summary>
	/// Error message: Message is already being processed; skipping.
	/// </summary>
	public const string MessageIsAlreadyBeingProcessedSkipping = "Message is already being processed; skipping.";

	/// <summary>
	/// Error message: Message is duplicate; processed at.
	/// </summary>
	public const string MessageIsDuplicateProcessedAt = "Message is duplicate; processed at.";

	/// <summary>
	/// Error message: Message is duplicate; skipping.
	/// </summary>
	public const string MessageIsDuplicateSkipping = "Message is duplicate; skipping.";

	/// <summary>
	/// Error message: Message is not duplicate.
	/// </summary>
	public const string MessageIsNotDuplicate = "Message is not duplicate.";

	/// <summary>
	/// Error message: Message is ready for processing.
	/// </summary>
	public const string MessageIsReadyForProcessing = "Message is ready for processing.";

	/// <summary>
	/// Error message: Message not found in inbox.
	/// </summary>
	public const string MessageNotFoundInInbox = "Message not found in inbox.";

	/// <summary>
	/// Error message: Message with ID already exists in inbox.
	/// </summary>
	public const string MessageWithIdAlreadyExistsInInbox = "Message with ID already exists in inbox.";

	/// <summary>
	/// Error message: Message with ID already marked as processed.
	/// </summary>
	public const string MessageWithIdAlreadyMarkedAsProcessed = "Message with ID already marked as processed.";

	/// <summary>
	/// Error message: Message with ID not found in inbox.
	/// </summary>
	public const string MessageWithIdNotFoundInInbox = "Message with ID not found in inbox.";

	/// <summary>
	/// Error message: No inbox entry found for message.
	/// </summary>
	public const string NoInboxEntryFoundForMessage = "No inbox entry found for message.";

	/// <summary>
	/// Error message: No inbox store or deduplicator available; processing without inbox semantics.
	/// </summary>
	public const string NoInboxStoreOrDeduplicatorAvailableProcessingWithoutInboxSemantics =
		"No inbox store or deduplicator available; processing without inbox semantics.";

	/// <summary>
	/// Error message: Processing message with inbox semantics.
	/// </summary>
	public const string ProcessingMessageWithInboxSemantics = "Processing message with inbox semantics.";

	/// <summary>
	/// Error message: Retrieved failed entries eligible for retry.
	/// </summary>
	public const string RetrievedFailedEntriesEligibleForRetry = "Retrieved failed entries eligible for retry.";

	/// <summary>
	/// Error message: Retrieved inbox entry for message.
	/// </summary>
	public const string RetrievedInboxEntryForMessage = "Retrieved inbox entry for message.";

	/// <summary>
	/// Error message: Could not deserialize as Dispatch message.
	/// </summary>
	public const string CouldNotDeserializeAsDispatchMessage = "Could not deserialize as Dispatch message.";

	/// <summary>
	/// Error message: Could not deserialize as integration event.
	/// </summary>
	public const string CouldNotDeserializeAsIntegrationEvent = "Could not deserialize as integration event.";

	/// <summary>
	/// Error message: Dispatch error.
	/// </summary>
	public const string DispatchError = "Dispatch error.";

	/// <summary>
	/// Error message: Dispatch error URI.
	/// </summary>
	public const string DispatchErrorUri = "Dispatch error URI.";

	/// <summary>
	/// Error message: Excalibur framework error.
	/// </summary>
	public const string DispatchFrameworkError = "Excalibur framework error.";

	/// <summary>
	/// Error message: Messaging error occurred.
	/// </summary>
	public const string MessagingErrorOccurred = "Messaging error occurred.";

	/// <summary>
	/// Error message: Pipe completed before message deserialized.
	/// </summary>
	public const string PipeCompletedBeforeMessageDeserialized = "Pipe completed before message deserialized.";

	/// <summary>
	/// Error message: Default pool.
	/// </summary>
	public const string DefaultPool = "Default pool.";

	/// <summary>
	/// Error message: Exception during light mode processing for message.
	/// </summary>
	public const string ExceptionDuringLightModeProcessingForMessage = "Exception during light mode processing for message.";

	/// <summary>
	/// Error message: Bulk operation failed.
	/// </summary>
	public const string BulkOperationFailed = "Bulk operation failed.";

	/// <summary>
	/// Error message: Bulk operation failed detail.
	/// </summary>
	public const string BulkOperationFailedDetail = "Bulk operation failed detail.";

	/// <summary>
	/// Error message: Failed to check alert threshold.
	/// </summary>
	public const string FailedToCheckAlertThreshold = "Failed to check alert threshold.";

	/// <summary>
	/// Error message: Failed to deserialize message metadata.
	/// </summary>
	public const string FailedToDeserializeMessageMetadata = "Failed to deserialize message metadata.";

	/// <summary>
	/// Error message: Failed to handle poison message (original reason).
	/// </summary>
	public const string FailedToHandlePoisonMessageOriginalReason = "Failed to handle poison message (original reason).";

	/// <summary>
	/// Error message: Failed to serialize message.
	/// </summary>
	public const string FailedToSerializeMessage = "Failed to serialize message.";

	/// <summary>
	/// Error message: Failed to serialize message payload for audit.
	/// </summary>
	public const string FailedToSerializeMessagePayloadForAudit = "Failed to serialize message payload for audit.";

	/// <summary>
	/// Error message: Marked message as failed due to exception.
	/// </summary>
	public const string MarkedMessageAsFailedDueToException = "Marked message as failed due to exception.";

	/// <summary>
	/// Error message: Marked message as failed with error.
	/// </summary>
	public const string MarkedMessageAsFailedWithError = "Marked message as failed with error.";

	/// <summary>
	/// Error message: Message dispatch failed.
	/// </summary>
	public const string MessageDispatchFailed = "Message dispatch failed.";

	/// <summary>
	/// Error message: Message previously failed; will retry.
	/// </summary>
	public const string MessagePreviouslyFailedWillRetry = "Message previously failed; will retry.";

	/// <summary>
	/// Error message: Message processing error.
	/// </summary>
	public const string MessageProcessingError = "Message processing error.";

	/// <summary>
	/// Error message: Message processing failed.
	/// </summary>
	public const string MessageProcessingFailed = "Message processing failed.";

	/// <summary>
	/// Error message: Message processing failed in light mode with error.
	/// </summary>
	public const string MessageProcessingFailedInLightModeWithError = "Message processing failed in light mode with error.";

	/// <summary>
	/// Error message: Operation failed.
	/// </summary>
	public const string OperationFailed = "Operation failed.";

	/// <summary>
	/// Error message: Operation failed detail.
	/// </summary>
	public const string OperationFailedDetail = "Operation failed detail.";

	/// <summary>
	/// Error message: Processing failed.
	/// </summary>
	public const string ProcessingFailed = "Processing failed.";

	/// <summary>
	/// Error message: Processing failed; retry attempt.
	/// </summary>
	public const string ProcessingFailedRetryAttempt = "Processing failed; retry attempt.";

	/// <summary>
	/// Error message: Unable to decrypt payload with any registered key.
	/// </summary>
	public const string UnableToDecryptPayloadWithAnyRegisteredKey = "Unable to decrypt payload with any registered key.";

	/// <summary>
	/// Error message: Unknown version status for event.
	/// </summary>
	public const string UnknownVersionStatusForEvent = "Unknown version status for event.";

	/// <summary>
	/// Error message: Message ID.
	/// </summary>
	public const string MessageId = "Message ID.";

	/// <summary>
	/// Error message: Message processed status.
	/// </summary>
	public const string MessageProcessedStatus = "Message processed status.";

	/// <summary>
	/// Error message: Message type.
	/// </summary>
	public const string MessageType = "Message type.";

	/// <summary>
	/// Error message: Version.
	/// </summary>
	public const string Version = "Version.";

	/// <summary>
	/// Error message: Audit message payload.
	/// </summary>
	public const string AuditMessagePayload = "Audit message payload.";

	/// <summary>
	/// Error message: Audit message payload too large.
	/// </summary>
	public const string AuditMessagePayloadTooLarge = "Audit message payload too large.";

	/// <summary>
	/// Error message: Error in poison detector for message.
	/// </summary>
	public const string ErrorInPoisonDetectorForMessage = "Error in poison detector for message.";

	/// <summary>
	/// Error message: Message detected as poison by detector.
	/// </summary>
	public const string MessageDetectedAsPoisonByDetector = "Message detected as poison by detector.";

	/// <summary>
	/// Error message: Poison message alert threshold exceeded.
	/// </summary>
	public const string PoisonMessageAlertThresholdExceeded = "Poison message alert threshold exceeded.";

	/// <summary>
	/// Error message: Potential object leak detected.
	/// </summary>
	public const string PotentialObjectLeakDetected = "Potential object leak detected.";

	/// <summary>
	/// Error message: Top poison message type.
	/// </summary>
	public const string TopPoisonMessageType = "Top poison message type.";

	/// <summary>
	/// Error message: Top poison reason.
	/// </summary>
	public const string TopPoisonReason = "Top poison reason.";

	/// <summary>
	/// Error message: Attempted to return object not rented from pool.
	/// </summary>
	public const string AttemptedToReturnObjectNotRentedFromPool = "Attempted to return object not rented from pool.";

	/// <summary>
	/// Error message: Attempted to run without calling Init.
	/// </summary>
	public const string AttemptedToRunWithoutCallingInit = "Attempted to run without calling Init.";

	/// <summary>
	/// Error message: Object leak on disposal; still rented by thread.
	/// </summary>
	public const string ObjectLeakOnDisposalStillRentedByThread = "Object leak on disposal; still rented by thread.";

	/// <summary>
	/// Error message: Pool disposed statistics.
	/// </summary>
	public const string PoolDisposedStatistics = "Pool disposed statistics.";

	/// <summary>
	/// Error message: Pool is disposed.
	/// </summary>
	public const string PoolIsDisposed = "Pool is disposed.";

	/// <summary>
	/// Error message: Retry attempt.
	/// </summary>
	public const string RetryAttempt = "Retry attempt.";

	/// <summary>
	/// Error message: Serialization error occurred.
	/// </summary>
	public const string SerializationErrorOccurred = "Serialization error occurred.";

	/// <summary>
	/// Error message: Message {MessageId} marked as processed with correlation {CorrelationId}.
	/// </summary>
	public const string MarkedMessageAsProcessed = "Message {MessageId} marked as processed with correlation {CorrelationId}.";

	/// <summary>
	/// Error message: Marked message as processed in light mode.
	/// </summary>
	public const string MarkedMessageAsProcessedInLightMode = "Marked message as processed in light mode.";

	/// <summary>
	/// Error message: Marked message as processed with expiry.
	/// </summary>
	public const string MarkedMessageAsProcessedWithExpiry = "Marked message as processed with expiry.";

	/// <summary>
	/// Error message: In-memory transaction scope only supports creating provider.
	/// </summary>
	public const string InMemoryTransactionScopeOnlySupportsCreatingProvider =
		"In-memory transaction scope only supports creating provider.";

	/// <summary>
	/// Error message: Redis transaction scope only supports creating provider.
	/// </summary>
	public const string RedisTransactionScopeOnlySupportsCreatingProvider = "Redis transaction scope only supports creating provider.";
}
