// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Microsoft.Extensions.Logging;

namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.FirestoreTriggers
{
 /// <summary>
 /// Example Firestore function that handles user profile changes.
 /// </summary>
 public class UserProfileFunction : FirestoreFunction<UserProfile>
 {
 private readonly ILogger<UserProfileFunction> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="UserProfileFunction"/> class.
 /// </summary>
 public UserProfileFunction()
 {
 _logger = GetLogger().AsGeneric<UserProfileFunction>();
 }

 /// <summary>
 /// Configures the trigger to only handle user profile documents.
 /// </summary>
 protected override FirestoreTriggerOptions ConfigureTriggerOptions()
 {
 return new FirestoreTriggerOptions
 {
 // Only process documents in the users collection
 AllowedCollections = new() { "users" },

 // Track field changes for updates
 TrackFieldChanges = true,

 // Set path template to extract user ID
 PathTemplate = "users/{userId}",

 // Validate all events
 ValidateEvent = true
 };
 }

 /// <summary>
 /// Processes typed user profile changes.
 /// </summary>
 protected override async Task ProcessTypedChangeAsync(
 UserProfile? before,
 UserProfile? after,
 FirestoreChangeType changeType,
 string documentId,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 switch (changeType)
 {
 case FirestoreChangeType.Create:
 await HandleUserCreated(after!, documentId, context, cancellationToken);
 break;

 case FirestoreChangeType.Update:
 await HandleUserUpdated(before!, after!, documentId, context, cancellationToken);
 break;

 case FirestoreChangeType.Delete:
 await HandleUserDeleted(before!, documentId, context, cancellationToken);
 break;
 }
 }

 private async Task HandleUserCreated(
 UserProfile user,
 string userId,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogInformation("New user created: {UserId}, Name: {Name}, Email: {Email}",
 userId, user.DisplayName, user.Email);

 context.TrackMetric("users.created", 1);
 context.TrackMetric("user.plan", user.SubscriptionPlan == "premium" ? 1 : 0);

 // Simulate sending welcome email
 await SendWelcomeEmail(user, cancellationToken);

 // Initialize user preferences
 await InitializeUserPreferences(userId, cancellationToken);

 // Track signup source
 if (!string.IsNullOrEmpty(user.SignupSource))
 {
 context.TrackMetric($"signup.source.{user.SignupSource}", 1);
 }
 }

 private async Task HandleUserUpdated(
 UserProfile before,
 UserProfile after,
 string userId,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 context.TrackMetric("users.updated", 1);

 // Check for email change
 if (before.Email != after.Email)
 {
 _logger.LogInformation("User {UserId} changed email from {OldEmail} to {NewEmail}",
 userId, before.Email, after.Email);

 await SendEmailChangeNotification(before.Email, after.Email, cancellationToken);
 context.TrackMetric("users.email.changed", 1);
 }

 // Check for subscription upgrade
 if (before.SubscriptionPlan != after.SubscriptionPlan)
 {
 _logger.LogInformation("User {UserId} changed subscription from {OldPlan} to {NewPlan}",
 userId, before.SubscriptionPlan, after.SubscriptionPlan);

 if (after.SubscriptionPlan == "premium" && before.SubscriptionPlan != "premium")
 {
 await GrantPremiumFeatures(userId, cancellationToken);
 context.TrackMetric("users.upgraded.premium", 1);
 }
 }

 // Check for profile completion
 if (!before.IsProfileComplete && after.IsProfileComplete)
 {
 await SendProfileCompletionReward(userId, cancellationToken);
 context.TrackMetric("users.profile.completed", 1);
 }
 }

 private async Task HandleUserDeleted(
 UserProfile user,
 string userId,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogInformation("User deleted: {UserId}, Name: {Name}",
 userId, user.DisplayName);

 context.TrackMetric("users.deleted", 1);

 // Clean up user data
 await CleanupUserData(userId, cancellationToken);

 // Send deletion confirmation
 await SendAccountDeletionConfirmation(user.Email, cancellationToken);

 // Archive user data for compliance
 await ArchiveUserData(userId, user, cancellationToken);
 }

 // Helper methods (simulated operations)
 private Task SendWelcomeEmail(UserProfile user, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Sending welcome email to {Email}", user.Email);
 return Task.Delay(100, cancellationToken); // Simulate async operation
 }

 private Task InitializeUserPreferences(string userId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Initializing preferences for user {UserId}", userId);
 return Task.Delay(50, cancellationToken);
 }

 private Task SendEmailChangeNotification(string oldEmail, string newEmail, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Notifying email change from {OldEmail} to {NewEmail}", oldEmail, newEmail);
 return Task.Delay(100, cancellationToken);
 }

 private Task GrantPremiumFeatures(string userId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Granting premium features to user {UserId}", userId);
 return Task.Delay(150, cancellationToken);
 }

 private Task SendProfileCompletionReward(string userId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Sending profile completion reward to user {UserId}", userId);
 return Task.Delay(100, cancellationToken);
 }

 private Task CleanupUserData(string userId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Cleaning up data for user {UserId}", userId);
 return Task.Delay(200, cancellationToken);
 }

 private Task SendAccountDeletionConfirmation(string email, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Sending deletion confirmation to {Email}", email);
 return Task.Delay(100, cancellationToken);
 }

 private Task ArchiveUserData(string userId, UserProfile user, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Archiving data for user {UserId}", userId);
 return Task.Delay(150, cancellationToken);
 }
 }

 /// <summary>
 /// User profile model.
 /// </summary>
 public class UserProfile {
 /// <summary>
 /// Gets or sets the user ID.
 /// </summary>
 public string? Id { get; set; }

 /// <summary>
 /// Gets or sets the display name.
 /// </summary>
 public string DisplayName { get; set; } = null!;

 /// <summary>
 /// Gets or sets the email address.
 /// </summary>
 public string Email { get; set; } = null!;

 /// <summary>
 /// Gets or sets the avatar URL.
 /// </summary>
 public string? AvatarUrl { get; set; }

 /// <summary>
 /// Gets or sets the subscription plan.
 /// </summary>
 public string SubscriptionPlan { get; set; } = "free";

 /// <summary>
 /// Gets or sets the signup source.
 /// </summary>
 public string? SignupSource { get; set; }

 /// <summary>
 /// Gets or sets the created timestamp.
 /// </summary>
 public DateTime CreatedAt { get; set; }

 /// <summary>
 /// Gets or sets the last login timestamp.
 /// </summary>
 public DateTime? LastLoginAt { get; set; }

 /// <summary>
 /// Gets or sets a value indicating whether the profile is complete.
 /// </summary>
 public bool IsProfileComplete =>
 !string.IsNullOrEmpty(DisplayName) &&
 !string.IsNullOrEmpty(AvatarUrl);
 }
}