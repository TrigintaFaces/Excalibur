// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Framework;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Microsoft.Extensions.Logging;

namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.FirestoreTriggers
{
 /// <summary>
 /// Example Firestore function that handles real-time chat messages.
 /// </summary>
 public class ChatMessageFunction : FirestoreFunction
 {
 private readonly ILogger<ChatMessageFunction> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="ChatMessageFunction"/> class.
 /// </summary>
 public ChatMessageFunction()
 {
 _logger = GetLogger().AsGeneric<ChatMessageFunction>();
 }

 /// <summary>
 /// Configures the trigger for chat messages.
 /// </summary>
 protected override FirestoreTriggerOptions ConfigureTriggerOptions()
 {
 return new FirestoreTriggerOptions
 {
 // Set path template to extract room and message IDs
 PathTemplate = "chatRooms/{roomId}/messages/{messageId}",

 // Only process new messages and edits
 AllowedChangeTypes = new()
 {
 FirestoreChangeType.Create,
 FirestoreChangeType.Update
 },

 // Track field changes for message edits
 TrackFieldChanges = true,

 // Validate all events
 ValidateEvent = true
 };
 }

 /// <summary>
 /// Processes chat message changes.
 /// </summary>
 protected override async Task ProcessDocumentChangeAsync(
 FirestoreDocumentChange change,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 // Extract room ID from path parameters
 var roomId = change.PathParameters.GetValueOrDefault("roomId", "unknown");
 var messageId = change.DocumentId;

 context.Items["ChatRoomId"] = roomId;
 context.Items["MessageId"] = messageId;

 // Deserialize message
 var message = DeserializeChatMessage(change.NewValue);
 if (message == null)
 {
 _logger.LogWarning("Failed to deserialize message {MessageId} in room {RoomId}",
 messageId, roomId);
 return;
 }

 switch (change.ChangeType)
 {
 case FirestoreChangeType.Create:
 await HandleNewMessage(message, roomId, messageId, context, cancellationToken);
 break;

 case FirestoreChangeType.Update:
 var oldMessage = DeserializeChatMessage(change.OldValue);
 await HandleMessageEdit(oldMessage!, message, roomId, messageId, change.ChangedFields, context, cancellationToken);
 break;
 }
 }

 private async Task HandleNewMessage(
 ChatMessage message,
 string roomId,
 string messageId,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogInformation("New message in room {RoomId}: {MessageId} from {SenderId}",
 roomId, messageId, message.SenderId);

 context.TrackMetric("chat.messages.created", 1);
 context.TrackMetric($"chat.messages.type.{message.Type}", 1);

 // Check for mentions
 if (message.Mentions?.Count > 0)
 {
 await SendMentionNotifications(message.Mentions, message, roomId, cancellationToken);
 context.TrackMetric("chat.mentions.count", message.Mentions.Count);
 }

 // Moderate content
 var moderationResult = await ModerateContent(message.Content, cancellationToken);
 if (moderationResult.RequiresModeration)
 {
 await HandleModerationRequired(messageId, roomId, moderationResult, cancellationToken);
 context.TrackMetric("chat.moderation.flagged", 1);
 }

 // Process attachments
 if (message.Attachments?.Count > 0)
 {
 await ProcessAttachments(message.Attachments, messageId, cancellationToken);
 context.TrackMetric("chat.attachments.count", message.Attachments.Count);
 }

 // Update room statistics
 await UpdateRoomStatistics(roomId, cancellationToken);

 // Send push notifications to room members
 await SendNewMessageNotifications(roomId, message, cancellationToken);

 // Process special message types
 switch (message.Type)
 {
 case MessageType.Command:
 await ProcessCommand(message.Content, roomId, message.SenderId, cancellationToken);
 break;

 case MessageType.Poll:
 await CreatePoll(message.PollData!, roomId, messageId, cancellationToken);
 break;

 case MessageType.Announcement:
 await BroadcastAnnouncement(message, roomId, cancellationToken);
 break;
 }
 }

 private async Task HandleMessageEdit(
 ChatMessage oldMessage,
 ChatMessage newMessage,
 string roomId,
 string messageId,
 List<string> changedFields,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogInformation("Message edited in room {RoomId}: {MessageId}, Fields changed: {Fields}",
 roomId, messageId, string.Join(", ", changedFields));

 context.TrackMetric("chat.messages.edited", 1);

 // Check if content was changed
 if (changedFields.Contains("content"))
 {
 // Re-moderate edited content
 var moderationResult = await ModerateContent(newMessage.Content, cancellationToken);
 if (moderationResult.RequiresModeration)
 {
 await HandleModerationRequired(messageId, roomId, moderationResult, cancellationToken);
 }

 // Track edit history
 await SaveEditHistory(messageId, oldMessage.Content, newMessage.Content, cancellationToken);

 // Notify users about the edit
 await NotifyMessageEdit(roomId, messageId, newMessage.SenderId, cancellationToken);
 }

 // Check for new mentions
 var newMentions = GetNewMentions(oldMessage.Mentions, newMessage.Mentions);
 if (newMentions.Count > 0)
 {
 await SendMentionNotifications(newMentions, newMessage, roomId, cancellationToken);
 }
 }

 private ChatMessage? DeserializeChatMessage(FirestoreValue? value)
 {
 if (value?.Fields == null)
 return null;

 try
 {
 return new ChatMessage
 {
 SenderId = value.Fields.GetValueOrDefault("senderId")?.ToString() ?? "",
 Content = value.Fields.GetValueOrDefault("content")?.ToString() ?? "",
 Type = Enum.Parse<MessageType>(value.Fields.GetValueOrDefault("type")?.ToString() ?? "Text"),
 Timestamp = value.UpdateTime ?? DateTime.UtcNow,
 IsEdited = bool.Parse(value.Fields.GetValueOrDefault("isEdited")?.ToString() ?? "false"),
 Mentions = ParseList(value.Fields.GetValueOrDefault("mentions")),
 Attachments = ParseAttachments(value.Fields.GetValueOrDefault("attachments")),
 PollData = ParsePollData(value.Fields.GetValueOrDefault("pollData"))
 };
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to deserialize chat message");
 return null;
 }
 }

 private List<string> ParseList(object? value)
 {
 // Simplified parsing - in production, handle Firestore array values properly
 return new List<string>();
 }

 private List<MessageAttachment> ParseAttachments(object? value)
 {
 // Simplified parsing
 return new List<MessageAttachment>();
 }

 private PollData? ParsePollData(object? value)
 {
 // Simplified parsing
 return null;
 }

 private List<string> GetNewMentions(List<string>? oldMentions, List<string>? newMentions)
 {
 if (newMentions == null) return new List<string>();
 if (oldMentions == null) return newMentions;
 return newMentions.Except(oldMentions).ToList();
 }

 // Helper methods (simulated operations)
 private Task SendMentionNotifications(List<string> mentions, ChatMessage message, string roomId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Sending mention notifications to {Count} users", mentions.Count);
 return Task.Delay(100, cancellationToken);
 }

 private Task<ModerationResult> ModerateContent(string content, CancellationToken cancellationToken)
 {
 // Simulate content moderation
 var result = new ModerationResult
 {
 RequiresModeration = content.Contains("spam", StringComparison.OrdinalIgnoreCase),
 Reason = "Spam detected"
 };
 return Task.FromResult(result);
 }

 private Task HandleModerationRequired(string messageId, string roomId, ModerationResult result, CancellationToken cancellationToken)
 {
 _logger.LogWarning("Message {MessageId} in room {RoomId} flagged: {Reason}",
 messageId, roomId, result.Reason);
 return Task.Delay(50, cancellationToken);
 }

 private Task ProcessAttachments(List<MessageAttachment> attachments, string messageId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Processing {Count} attachments for message {MessageId}",
 attachments.Count, messageId);
 return Task.Delay(150, cancellationToken);
 }

 private Task UpdateRoomStatistics(string roomId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Updating statistics for room {RoomId}", roomId);
 return Task.Delay(50, cancellationToken);
 }

 private Task SendNewMessageNotifications(string roomId, ChatMessage message, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Sending notifications for new message in room {RoomId}", roomId);
 return Task.Delay(100, cancellationToken);
 }

 private Task ProcessCommand(string command, string roomId, string senderId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Processing command '{Command}' in room {RoomId} from {SenderId}",
 command, roomId, senderId);
 return Task.Delay(100, cancellationToken);
 }

 private Task CreatePoll(PollData pollData, string roomId, string messageId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Creating poll in room {RoomId} for message {MessageId}", roomId, messageId);
 return Task.Delay(100, cancellationToken);
 }

 private Task BroadcastAnnouncement(ChatMessage message, string roomId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Broadcasting announcement in room {RoomId}", roomId);
 return Task.Delay(150, cancellationToken);
 }

 private Task SaveEditHistory(string messageId, string oldContent, string newContent, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Saving edit history for message {MessageId}", messageId);
 return Task.Delay(50, cancellationToken);
 }

 private Task NotifyMessageEdit(string roomId, string messageId, string editorId, CancellationToken cancellationToken)
 {
 _logger.LogDebug("Notifying room {RoomId} about edit to message {MessageId}", roomId, messageId);
 return Task.Delay(100, cancellationToken);
 }
 }

 /// <summary>
 /// Chat message model.
 /// </summary>
 public class ChatMessage {
 /// <summary>
 /// Gets or sets the sender ID.
 /// </summary>
 public string SenderId { get; set; } = null!;

 /// <summary>
 /// Gets or sets the message content.
 /// </summary>
 public string Content { get; set; } = null!;

 /// <summary>
 /// Gets or sets the message type.
 /// </summary>
 public MessageType Type { get; set; } = MessageType.Text;

 /// <summary>
 /// Gets or sets the timestamp.
 /// </summary>
 public DateTime Timestamp { get; set; }

 /// <summary>
 /// Gets or sets a value indicating whether the message was edited.
 /// </summary>
 public bool IsEdited { get; set; }

 /// <summary>
 /// Gets or sets the mentioned user IDs.
 /// </summary>
 public List<string>? Mentions { get; set; }

 /// <summary>
 /// Gets or sets the attachments.
 /// </summary>
 public List<MessageAttachment>? Attachments { get; set; }

 /// <summary>
 /// Gets or sets the poll data (if message type is Poll).
 /// </summary>
 public PollData? PollData { get; set; }
 }

 /// <summary>
 /// Message type enumeration.
 /// </summary>
 public enum MessageType
 {
 /// <summary>
 /// Regular text message.
 /// </summary>
 Text,

 /// <summary>
 /// Image message.
 /// </summary>
 Image,

 /// <summary>
 /// File attachment.
 /// </summary>
 File,

 /// <summary>
 /// Command message.
 /// </summary>
 Command,

 /// <summary>
 /// Poll message.
 /// </summary>
 Poll,

 /// <summary>
 /// Announcement message.
 /// </summary>
 Announcement
 }

 /// <summary>
 /// Message attachment model.
 /// </summary>
 public class MessageAttachment {
 /// <summary>
 /// Gets or sets the attachment ID.
 /// </summary>
 public string Id { get; set; } = null!;

 /// <summary>
 /// Gets or sets the file name.
 /// </summary>
 public string FileName { get; set; } = null!;

 /// <summary>
 /// Gets or sets the file size.
 /// </summary>
 public long FileSize { get; set; }

 /// <summary>
 /// Gets or sets the content type.
 /// </summary>
 public string ContentType { get; set; } = null!;

 /// <summary>
 /// Gets or sets the download URL.
 /// </summary>
 public string DownloadUrl { get; set; } = null!;
 }

 /// <summary>
 /// Poll data model.
 /// </summary>
 public class PollData {
 /// <summary>
 /// Gets or sets the poll question.
 /// </summary>
 public string Question { get; set; } = null!;

 /// <summary>
 /// Gets or sets the poll options.
 /// </summary>
 public List<string> Options { get; set; } = new();

 /// <summary>
 /// Gets or sets a value indicating whether multiple selections are allowed.
 /// </summary>
 public bool AllowMultiple { get; set; }

 /// <summary>
 /// Gets or sets the poll expiration time.
 /// </summary>
 public DateTime? ExpiresAt { get; set; }
 }

 /// <summary>
 /// Moderation result model.
 /// </summary>
 internal class ModerationResult {
 /// <summary>
 /// Gets or sets a value indicating whether moderation is required.
 /// </summary>
 public bool RequiresModeration { get; set; }

 /// <summary>
 /// Gets or sets the reason for moderation.
 /// </summary>
 public string? Reason { get; set; }
 }
}