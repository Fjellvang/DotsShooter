// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using FirebaseAdmin.Messaging;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.TypeCodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.PushNotification
{
    [MetaMessage(MessageCodesCore.SendPushNotification, MessageDirection.ServerInternal)]
    public class SendPushNotification : MetaMessage
    {
        public EntityId                     SourceEntityId;
        public List<string>                 DeviceTokens;
        public string                       Title;
        public string                       Body;
        public Dictionary<string, string>   Data;
        // \todo [petri] add ExpireAt?

        SendPushNotification() { }
        public SendPushNotification(EntityId sourceEntityId, IEnumerable<string> deviceTokens, string title, string body, Dictionary<string, string> data)
        {
            MetaDebug.Assert(sourceEntityId.IsValid, "SourceEntityId {0} is invalid", sourceEntityId);
            SourceEntityId  = sourceEntityId;
            DeviceTokens    = deviceTokens.ToList();
            Title           = title;
            Body            = body;
            Data            = data;
        }
    }

    public class PendingNotification
    {
        public EntityId                     SourceEntityId;
        public string                       DeviceToken;
        public string                       Title;
        public string                       Body;
        public Dictionary<string, string>   Data;

        PendingNotification() { }
        public PendingNotification(EntityId sourceEntityId, string deviceToken, string title, string body, Dictionary<string, string> data)
        {
            SourceEntityId  = sourceEntityId;
            DeviceToken     = deviceToken;
            Title           = title;
            Body            = body;
            Data            = data;
        }
    }

    [EntityConfig]
    internal sealed class PushNotifierConfig : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.PushNotifier;
        public override Type                EntityActorType         => typeof(PushNotificationActor);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Logic;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateStaticService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Entity actor for sending out push notifications to mobile devices via FCM (Firebase Cloud Messaging).
    /// </summary>
    public class PushNotificationActor : EphemeralEntityActor
    {
        protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        List<PendingNotification>   _pendingNotifications   = new List<PendingNotification>();
        bool                        _isSending              = false;

        public PushNotificationActor(EntityId entityId) : base(entityId)
        {
        }

        //void PostLoad()
        //{
        //    // Initiate sending of notifications (in case there are pending ones)
        //    if (_pendingNotifications.Count > 0)
        //    {
        //        _log.Info("Resuming sending of push notifications ({0} pending)", _pendingNotifications.Count);
        //        TrySendNotificationBatch();
        //    }
        //}

        [MessageHandler]
        void HandleSendPushNotification(EntityId fromEntityId, SendPushNotification send)
        {
            // Store message as pending and try to send out a batch
            if (send.DeviceTokens.Count > 0)
            {
                _log.Debug("Enqueueing push notification from {EntityId}: {Message} ({NumDeviceTokens} tokens)", fromEntityId, PrettyPrint.Compact(send), send.DeviceTokens.Count);
                foreach (string deviceToken in send.DeviceTokens)
                {
                    PendingNotification notification = new PendingNotification(send.SourceEntityId, deviceToken, send.Title, send.Body, send.Data);
                    _pendingNotifications.Add(notification);
                }

                // Initiate actual sending
                TrySendNotificationBatch();
            }
            else
                _log.Warning("Sending push notification for {EntityId} with no device tokens", send.SourceEntityId);
        }

        static readonly AndroidConfig DefaultAndroidConfig = new AndroidConfig
        {
            Priority = Priority.High,
            Notification = new AndroidNotification
            {
                Sound = "default",
            }
        };

        static readonly ApnsConfig DefaultApnsConfig = new ApnsConfig
        {
            Aps = new Aps
            {
                Sound = "default",
            }
        };

        Task<BatchResponse> SendBatchAsync()
        {
            // Token for Petri's Honor 10
            //string deviceToken = "eksjMyqvVCw:APA91bH1Wb0ycmobaKPjIEjCMQKuLaVPd0wFpeN9wWK1hy1GiZlXaYAq9NrhzaMeUcfPXMdyAzLEZNIRpYxoT1Fu6PeaWFI0w9DYCvKjOBjCO1FrRRVTftQz55r6ub_PXVCPNi82ntqR";

            // Construct FCM message batch
            List<Message> messages = new List<Message>();

            // Split multicast notifications to individual messages
            const int MaxBatchSize = 100;
            int numSendNotifications = Math.Min(_pendingNotifications.Count, MaxBatchSize);
            for (int ndx = 0; ndx < numSendNotifications; ndx++)
            {
                PendingNotification pending = _pendingNotifications[ndx];

                // \todo [petri] re-use Android/Apns configs?
                messages.Add(new Message
                {
                    Token = pending.DeviceToken,
                    Notification = new Notification
                    {
                        Title = pending.Title,
                        Body = pending.Body,
                    },
                    Data = pending.Data,
                    Android = DefaultAndroidConfig,
                    Apns = DefaultApnsConfig,
                });
            };

            // Send the batch to FCM
            if (messages.Count > 0)
            {
                _log.Debug("Sending batch of {NumMessages} push notifications to FCM", messages.Count);
                _isSending = true;
                if (RuntimeOptionsRegistry.Instance.GetCurrent<PushNotificationOptions>().UseLegacyApi)
                    return FirebaseMessaging.DefaultInstance.SendAllAsync(messages);
                else
                    return FirebaseMessaging.DefaultInstance.SendEachAsync(messages);
            }
            else
            {
                _log.Warning("No messages to send!");
                return Task.FromResult<BatchResponse>(null);
            }
        }

        void TrySendNotificationBatch()
        {
            if (!_isSending && _pendingNotifications.Count > 0)
            {
                ContinueTaskOnActorContext(
                    SendBatchAsync(),
                    response =>
                    {
                        if (response != null)
                        {
                            if (response.FailureCount == 0)
                                _log.Info("Processing send message response ({NumSuccesses} successes, {NumFailures} failures, {NumResults} results)!", response.SuccessCount, response.FailureCount, response.SuccessCount);
                            else
                                _log.Warning("Processing send message response ({NumSuccesses} successes, {NumFailures} failures, {NumResults} results):\n{Responses}", response.SuccessCount, response.FailureCount, response.Responses.Count, GetSendResponseFailuresString(_pendingNotifications, response.Responses));

                            // Remove all sent messages
                            // \todo [petri] retry failures (with some delay & max retry count?)
                            _pendingNotifications.RemoveRange(0, response.SuccessCount + response.FailureCount);
                        }
                        else
                            _log.Warning("No BatchResponse from push notification batch!");

                        // Mark as no longer sending
                        _isSending = false;

                        // Try sending another batch
                        TrySendNotificationBatch();
                    },
                    failure =>
                    {
                        _log.Warning("Failed to send push notification batch: {Exception}", failure);

                        // Mark as no longer sending
                        _isSending = false;

                        // Try sending again
                        // \todo [petri] use small delay?
                        TrySendNotificationBatch();
                    });
            }
        }

        /// <summary>
        /// Given <paramref name="responses"/> and their corresponding <paramref name="notifications"/>
        /// (in the same order), this produces a human-readable information string describing the failed
        /// notifications (error codes and counts, plus example player id and device token for each error code).
        /// </summary>
        /// <remarks>
        /// <paramref name="notifications"/> must be at least as long as <paramref name="responses"/>, but can be longer.
        /// </remarks>
        static string GetSendResponseFailuresString(List<PendingNotification> notifications, IReadOnlyList<SendResponse> responses)
        {
            try
            {
                // For each error code (string), this collects the number of those errors, plus one example notification that got that error.
                MetaDictionary<string, (int Count, PendingNotification ExampleNotification)> errors = null;

                for (int i = 0; i < responses.Count; i++)
                {
                    SendResponse response = responses[i];
                    if (response.IsSuccess)
                        continue;

                    PendingNotification notification = notifications[i];

                    FirebaseAdmin.Messaging.MessagingErrorCode? messagingErrorCode = response.Exception?.MessagingErrorCode;
                    FirebaseAdmin.ErrorCode? generalErrorCode = response.Exception?.ErrorCode;

                    string errorCodeString;
                    if (messagingErrorCode.HasValue)
                        errorCodeString = "Messaging_" + messagingErrorCode.Value.ToString();
                    else if (generalErrorCode.HasValue)
                        errorCodeString = "General_" + generalErrorCode.Value.ToString();
                    else
                        errorCodeString = "ErrorCodeMissing";

                    if (errors == null)
                        errors = new();

                    if (errors.TryGetValue(errorCodeString, out (int Count, PendingNotification ExampleNotification) errorInfo))
                        errors[errorCodeString] = (errorInfo.Count + 1, errorInfo.ExampleNotification);
                    else
                        errors.Add(errorCodeString, (Count: 1, ExampleNotification: notification));
                }

                if (errors == null)
                    return "(no errors)";
                else
                {
                    StringBuilder errorsString = new StringBuilder(capacity: errors.Count * 256);
                    foreach ((string errorCode, (int count, PendingNotification exampleNotification)) in errors)
                        errorsString.AppendLine(Invariant($"  {errorCode} x {count}, example: entityId {exampleNotification.SourceEntityId}, deviceToken {exampleNotification.DeviceToken}"));
                    return errorsString.ToString();
                }
            }
            catch (Exception ex)
            {
                // Not really expected to happen, just being defensive as this method is only used for logging.
                return $"Error: {ex}";
            }
        }
    }
}
