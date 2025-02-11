// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Server.Database;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi
{
    /// <summary>
    /// Methods for accessing and communicating with the entities on the server from outside the
    /// actor/entity realm. Primarily intended to be used by the ASP.NET HTTP controllers. This
    /// class only has the generic entity-related functionality while the game server -related
    /// functionality is implemented in <code>ServerApiBridge</code>.
    /// </summary>
    public class SharedApiBridge : IEntityMessagingApi, IMetaIntegration<SharedApiBridge>
    {
        /// <summary>
        /// Forward an <c>EntityAsk</c> (<see cref="MetaMessage"/>) to the target entity from
        /// an ASP.NET controller and return an awaitable <c>Task&lt;TResult&gt;</c> for handling
        /// the result.
        /// </summary>
        public class ForwardAskToEntity
        {
            public EntityId     EntityId    { get; private set; }
            public MetaMessage  Message     { get; private set; }

            ForwardAskToEntity(EntityId entityId, MetaMessage message) { EntityId = entityId; Message = message; }

            public static async Task<TResult> ExecuteAsync<TResult>(IActorRef hostActor, EntityId entityId, MetaMessage message)
            {
                object response = await hostActor.Ask(new ForwardAskToEntity(entityId, message), TimeSpan.FromSeconds(10));
                if (response is TResult result)
                    return result;
                else if (response is Exception ex)
                    throw ex;
                else
                    throw new InvalidOperationException($"Invalid response type for EntityAskAsync<{typeof(TResult).ToGenericTypeString()}>({message.GetType().ToGenericTypeString()}) to {entityId}: got {response.GetType().ToGenericTypeString()} (expecting {typeof(TResult).ToGenericTypeString()})");
            }

            public void HandleReceive(IActorRef sender, IMetaLogger log, IEntityMessagingApi asker)
            {
                _ = asker.EntityAskAsync<MetaMessage>(EntityId, Message)
                    .ContinueWith(response =>
                    {
                        if (response.IsFaulted)
                        {
                            // \note This flattening code is duplicated in EntityActor.EntityAskAsync
                            AggregateException flattened = response.Exception.Flatten();
                            Exception effectiveException = flattened.InnerException is EntityAskExceptionBase askException ? askException : flattened;
                            sender.Tell(effectiveException);
                        }
                        else if (response.IsCompletedSuccessfully)
                            sender.Tell(response.GetCompletedResult());
                        else if (response.IsCanceled)
                            sender.Tell(new OperationCanceledException($"EntityAsk<{Message.GetType().ToGenericTypeString()}> was canceled"));
                        else
                        {
                            log.Error("Invalid response status to EntityAsk<{RequestType}>: {TaskStatus}", Message.GetType().ToGenericTypeString(), response.Status);
                            sender.Tell(new InvalidOperationException($"EntityAsk Task faulted with status {response.Status}"));
                        }
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        protected readonly IMetaLogger  _logger;
        protected readonly IActorRef    _adminApiRef;

        public SharedApiBridge(IActorRef adminApiRef)
        {
            _logger = MetaLogger.ForContext(GetType());
            _adminApiRef = adminApiRef;
        }

        /// <summary>
        /// Routes an emulated "CastMessage" to the target entity, but such that it's transmitted as an EntityAsk
        /// so that any failures are thrown as <see cref="EntityAskExceptionBase"/>s such as <see cref="Cloud.Sharding.EntityShard.UnexpectedEntityAskError"/>
        /// or <see cref="EntityAskRefusal"/>.
        /// </summary>
        public async Task TellEntityAsync(EntityId entityId, MetaMessage message)
        {
            // \note Throws on failure, otherwise just ignore the return value
            _ = await EntityAskAsync<HandleMessageResponse>(entityId, new HandleMessageRequest(message));
        }

        #region IEntityMessagingApi

        /// <inheritdoc />
        public Task<TResult> EntityAskAsync<TResult>(EntityId entityId, MetaMessage message) where TResult : MetaMessage
        {
            return ForwardAskToEntity.ExecuteAsync<TResult>(_adminApiRef, entityId, message);
        }

        /// <inheritdoc />
        public Task<TResult> EntityAskAsync<TResult>(EntityId entity, EntityAskRequest<TResult> request) where TResult : EntityAskResponse
        {
            return ForwardAskToEntity.ExecuteAsync<TResult>(_adminApiRef, entity, request);
        }

        /// <inheritdoc />
        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        public Task<TResult> EntityAskAsync<TResult>(EntityId entity, EntityAskRequest request) where TResult : MetaMessage
        {
            return ForwardAskToEntity.ExecuteAsync<TResult>(_adminApiRef, entity, request);
        }

        /// <inheritdoc />
        void IEntityMessagingApi.CastMessage(EntityId targetEntityId, MetaMessage message) => throw new NotImplementedException();

        /// <inheritdoc />
        void IEntityMessagingApi.TellSelf(object message) => throw new NotImplementedException();

        #endregion // IEntityMessagingApi

        /// <summary>
        /// Utility function to parse an entity id of a certain kind. If the format is incorrect or the resolved entity is not of kind <paramref name="expectedKind"/>, throws 400.
        /// </summary>
        /// <param name="entityIdStr">String representation of the Entity Id</param>
        public EntityId ParseEntityIdStr(string entityIdStr, EntityKind expectedKind)
        {
            EntityId entityId;
            try
            {
                entityId = EntityId.ParseFromString(entityIdStr);
            }
            catch (FormatException ex)
            {
                throw new MetaplayHttpException(400, $"{expectedKind} not found.", $"{entityIdStr} is not a valid {expectedKind} entity ID: {ex.Message}");
            }
            if (!entityId.IsOfKind(expectedKind))
            {
                throw new MetaplayHttpException(400, $"{expectedKind} not found.", $"{entityIdStr} is not {expectedKind} entity ID.");
            }
            return entityId;
        }

         /// <summary>
         /// Returns the persisted state of an entity from the database.
         /// </summary>
        public async Task<(bool isFound, bool isInitialized, IPersistedEntity entity)> TryGetPersistedEntityAsync(EntityId entityId)
        {
            if (!EntityConfigRegistry.Instance.TryGetPersistedConfig(entityId.Kind, out PersistedEntityConfig entityConfig))
                throw new MetaplayHttpException(400, "Invalid Entity kind.", $"Entity kind {entityId.Kind} does not refer to a PersistedEntity");

            IPersistedEntity persisted = await MetaDatabase.Get().TryGetAsync<IPersistedEntity>(type: entityConfig.PersistedType, entityId.ToString()).ConfigureAwait(false);
            return (persisted != null, persisted?.Payload != null, persisted);
        }

        /// <summary>
        /// Returns the Persisted state of an entity. If the entity does not exist, throws 404.
        /// </summary>
        public async Task<IPersistedEntity> GetPersistedEntityAsync(EntityId entityId)
        {
            if (!EntityConfigRegistry.Instance.TryGetPersistedConfig(entityId.Kind, out PersistedEntityConfig entityConfig))
                throw new MetaplayHttpException(400, "Invalid Entity kind.", $"Entity kind {entityId.Kind} does not refer to a PersistedEntity");

            IPersistedEntity persisted = await MetaDatabase.Get().TryGetAsync<IPersistedEntity>(type: entityConfig.PersistedType, entityId.ToString()).ConfigureAwait(false);
            if (persisted == null || persisted.Payload == null)
                throw new MetaplayHttpException(404, "Persisted entity not found.", $"Cannot find {entityId}.");

            return persisted;
        }
    }
}
