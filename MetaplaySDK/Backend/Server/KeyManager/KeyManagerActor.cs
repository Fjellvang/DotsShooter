// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.TypeCodes;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.KeyManager
{
    [EntityConfig]
    public class KeyManagerEntityConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.KeyManager;
        public override EntityShardGroup    EntityShardGroup        => EntityShardGroup.BaseServices;
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(60);
        public override Type                EntityActorType         => typeof(KeyManagerActor);

        public override bool UsesRealTimeForPersistedAt => true;
    }

    [Table("KeyManagers")]
    public class PersistedKeyManager : IPersistedEntity
    {
        [Key]
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string   EntityId        { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime PersistedAt     { get; set; }

        [Required]
        public byte[]   Payload         { get; set; }   // TaggedSerialized<KeyManagerState>

        [Required]
        public int      SchemaVersion   { get; set; }   // Schema version for object

        [Required]
        public bool     IsFinal         { get; set; }
    }

    [MetaSerializable]
    public class MetaPublicKey
    {
        [MetaMember(1)] [JsonPropertyName("kty")] public string KeyType     { get; private set; }
        [MetaMember(2)] [JsonPropertyName("use")] public string Use         { get; private set; }
        [MetaMember(3)] [JsonPropertyName("kid")] public string KeyId       { get; private set; }
        [MetaMember(4)] [JsonPropertyName("e")]   public string Exponent    { get; private set; }
        [MetaMember(5)] [JsonPropertyName("n")]   public string Modulus     { get; private set; }
        [MetaMember(6)] [JsonPropertyName("alg")] public string Algorithm   { get; private set; }
        [MetaMember(7)] [JsonPropertyName("iat")] public long   IssuedAt    { get; private set; }

        MetaPublicKey() { }
        public MetaPublicKey(string keyType, string use, string keyId, string exponent, string modulus, string algorithm, long issuedAt)
        {
            KeyType = keyType;
            Use = use;
            KeyId = keyId;
            Exponent = exponent;
            Modulus = modulus;
            Algorithm = algorithm;
            IssuedAt = issuedAt;
        }
    }

    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class KeyManagerState : ISchemaMigratable
    {
        [MetaMember(1)] public List<MetaPublicKey> PublicKeys { get; private set; } = new List<MetaPublicKey>();
    }

    /// <summary>
    /// Singleton service actor for managing key pairs and providing signing services for others to use. Also,
    /// publishes the recent history of public keys as jwks.json documents for 3rd parties to consume on the
    /// game's admin HTTP endpoints.
    ///
    /// The keys are frequently rotated and intended to be fairly short-lived and to be used for signing requests
    /// that can quickly be validated against the published public keys. This service is not intended for signing
    /// documents where the signatures need to be validatable for a long while.
    ///
    /// Only the most recent private key is remembered and it is only kept in memory. The (short) history of public
    /// keys is persisted in the database so that the service can remember them over reboots. The private key is
    /// always generated again after a reboot.
    ///
    /// The signing works by generating JWTs using this service for the specific request. Check out
    /// <see cref="CreateSignatureJwtRequest"/> and <see cref="HandleCreateSignatureJwt(KeyManagerActor.CreateSignatureJwtRequest)"/>
    /// for more details how to create JWTs are created.
    /// </summary>
    public class KeyManagerActor : PersistedEntityActor<PersistedKeyManager, KeyManagerState>
    {
        record KeyPair(string KeyId, DateTimeOffset IssuedAt, RSA Rsa, RsaSecurityKey RsaSecurityKey, MetaPublicKey PublicKey);

        public static readonly EntityId EntityId = EntityId.Create(EntityKindCloudCore.KeyManager, 0);

        protected sealed override AutoShutdownPolicy    ShutdownPolicy      => AutoShutdownPolicy.ShutdownNever();
        protected sealed override TimeSpan              SnapshotInterval    => TimeSpan.FromMinutes(1);

        KeyManagerOptions       _opts           = RuntimeOptionsRegistry.Instance.GetCurrent<KeyManagerOptions>();
        JwtSecurityTokenHandler _tokenHandler   = new JwtSecurityTokenHandler();

        /// <summary> Current private key to use for encryption. </summary>
        KeyPair                 _activeKey;
        /// <summary> Time when the <see cref="_activeKey"/> was rotated at. </summary>
        DateTime                _keyRotatedAt;

        /// <summary> Persisted state for KeyManager. </summary>
        KeyManagerState         _state;

        /// <summary> Pre-rendered JWKS JSON. </summary>
        string                  _jwksJson;

        // Hard-coded to 5 minutes as we only keep the public key history for a limited time.
        // The signatures are intended only for validating outgoing requests as they are being made,
        // not for long-term usage such as signing long-lived documents.
        static readonly TimeSpan _tokenValidFor = TimeSpan.FromMinutes(5);

        public KeyManagerActor(EntityId entityId) : base(entityId)
        {
        }

        protected override void RegisterHandlers()
        {
            Receive<ActorTick>(ReceiveActorTick);
            base.RegisterHandlers();
        }

        void ReceiveActorTick(ActorTick _)
        {
            // Rotate keys, if rotation interval exceeded
            DateTime now = DateTime.UtcNow;
            if (now >= _keyRotatedAt + _opts.RotationInterval)
                RotateKey();
        }

        protected override void PostStop()
        {
            // Dispose of private key
            _activeKey?.Rsa.Dispose();
            _activeKey = null;

            base.PostStop();
        }

        string CreateJwksJson()
        {
            Dictionary<string, List<MetaPublicKey>> keyDict = new()
            {
                { "keys", _state.PublicKeys }
            };
            return JsonSerializer.Serialize(keyDict);
        }

        KeyPair GenerateKeyPair()
        {
            DateTimeOffset issuedAt = DateTimeOffset.UtcNow;

            string keyId = MetaGuid.New().ToString();
            RSA rsa = RSA.Create(_opts.KeySize);

            RsaSecurityKey rsaSecurityKey = new RsaSecurityKey(rsa);
            rsaSecurityKey.KeyId = keyId;

            RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);
            MetaPublicKey publicKey = new MetaPublicKey("RSA", "sig", keyId, Base64UrlEncoder.Encode(parameters.Exponent), Base64UrlEncoder.Encode(parameters.Modulus), "RS256", issuedAt.ToUnixTimeSeconds());
            return new KeyPair(keyId, issuedAt, rsa, rsaSecurityKey, publicKey);
        }

        void RotateKey()
        {
            // Discard previous keyPair
            if (_activeKey != null)
            {
                _activeKey.Rsa.Dispose();
                _activeKey = null;
            }

            // Generate new KeyPair to use
            _activeKey = GenerateKeyPair();
            _keyRotatedAt = DateTime.UtcNow;

            // Remove any excess public keys
            _state.PublicKeys.Insert(0, _activeKey.PublicKey);
            if (_state.PublicKeys.Count > _opts.NumPublicKeysToRetain)
                _state.PublicKeys.RemoveRange(_opts.NumPublicKeysToRetain, _state.PublicKeys.Count - _opts.NumPublicKeysToRetain);

            // Update pre-generated JWKS JSON
            _jwksJson = CreateJwksJson();
        }

        [MetaMessage(MessageCodesCore.KeyManagerGetJwksRequest, MessageDirection.ServerInternal)]
        public class GetJwksRequest : EntityAskRequest<GetJwksResponse> { public static readonly GetJwksRequest Instance = new GetJwksRequest(); }
        [MetaMessage(MessageCodesCore.KeyManagerGetJwksResponse, MessageDirection.ServerInternal)]
        public class GetJwksResponse : EntityAskResponse
        {
            public string JwksJson { get; set; }

            GetJwksResponse() { }
            public GetJwksResponse(string jwksJson) { JwksJson = jwksJson; }
        }

        [EntityAskHandler]
        public GetJwksResponse HandleGetJwks(GetJwksRequest _) => new GetJwksResponse(_jwksJson);

        [MetaMessage(MessageCodesCore.KeyManagerCreateSignatureJwtRequest, MessageDirection.ServerInternal)]
        public class CreateSignatureJwtRequest : EntityAskRequest<CreateSignatureJwtResponse>
        {
            /// <summary> Audience for whom the message/signature is intended </summary>
            public string   Audience    { get; private set; }
            /// <summary> Subject that the message is about </summary>
            public string   Subject     { get; private set; }
            /// <summary> Hash of the payload, base64(SHA256(toUTF8(httpBody))) </summary>
            public string   PayloadHash { get; private set; }

            CreateSignatureJwtRequest() { }
            public CreateSignatureJwtRequest(string audience, string subject, string payloadHash)
            {
                Audience = audience;
                Subject = subject;
                PayloadHash = payloadHash;
            }
        }
        [MetaMessage(MessageCodesCore.KeyManagerCreateSignatureJwtResponse, MessageDirection.ServerInternal)]
        public class CreateSignatureJwtResponse : EntityAskResponse
        {
            public string Jwt { get; private set; }

            CreateSignatureJwtResponse() { }
            public CreateSignatureJwtResponse(string jwt) { Jwt = jwt; }
        }

        /// <summary>
        /// Returns the hostname (with optional port) of the AdminApi URL, for example
        /// 'idler-develop-admin.p1.metaplay.io' or 'localhost:5550'. Use the hostname (instead of an
        /// URL) to avoid attacks where someone could direct the request to a user-controlled file by
        /// injecting parts into the issuer path (eg, https://idler-develop-admin.p1.metaplay.io/file/userId/).
        /// </summary>
        /// <returns>Issuer to use for signing requests, the server's admin hostname plus optional port</returns>
        string ResolveIssuer()
        {
            DeploymentOptions   opts    = RuntimeOptionsRegistry.Instance.GetCurrent<DeploymentOptions>();
            Uri                 uri     = new Uri(opts.ApiUri);
            if (uri.IsDefaultPort)
                return uri.Host;
            else
                return Invariant($"{uri.Host}:{uri.Port}");
        }

        [EntityAskHandler]
        CreateSignatureJwtResponse HandleCreateSignatureJwt(CreateSignatureJwtRequest req)
        {
            // Generate token descriptor
            DateTime now = DateTime.UtcNow;
            SigningCredentials signingCredentials = new SigningCredentials(_activeKey.RsaSecurityKey, SecurityAlgorithms.RsaSha256);
            MetaDebug.Assert(signingCredentials.Key.KeyId == _activeKey.KeyId, "SigningCredentials.Key.KeyId does not match the key used for signing!");
            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, req.Subject),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("payloadHash", req.PayloadHash),
                ]),
                Audience = req.Audience,
                IssuedAt = now,
                Expires = now + _tokenValidFor,
                Issuer = ResolveIssuer(), // eg, idler-develop-admin.p1.metaplay.io or localhost:5550
                SigningCredentials = signingCredentials,
            };

            // Generate JWT as string
            SecurityToken token = _tokenHandler.CreateToken(tokenDescriptor);
            string jwt = _tokenHandler.WriteToken(token);
            return new CreateSignatureJwtResponse(jwt);
        }

        protected override sealed async Task Initialize()
        {
            PersistedKeyManager persisted = await MetaDatabase.Get().TryGetAsync<PersistedKeyManager>(_entityId.ToString());
            await InitializePersisted(persisted);
        }

        protected override Task<KeyManagerState> InitializeNew()
        {
            KeyManagerState state = new KeyManagerState();
            return Task.FromResult(state);
        }

        protected override Task<KeyManagerState> RestoreFromPersisted(PersistedKeyManager persisted)
        {
            KeyManagerState state = DeserializePersistedPayload<KeyManagerState>(persisted.Payload, resolver: null, logicVersion: null);
            return Task.FromResult(state);
        }

        protected override Task PostLoad(KeyManagerState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            // Store state
            _state = payload;

            // Create private key (and rotate public keys)
            RotateKey();

            return Task.CompletedTask;
        }

        protected override async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            SchemaMigrator migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator<KeyManagerState>();
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, migrator.CurrentSchemaVersion);

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            // Persist in database
            PersistedKeyManager persisted = new PersistedKeyManager
            {
                EntityId        = _entityId.ToString(),
                PersistedAt     = DateTime.UtcNow,
                Payload         = persistedPayload,
                SchemaVersion   = migrator.CurrentSchemaVersion,
                IsFinal         = isFinal,
            };

            if (isInitial)
                await MetaDatabase.Get().InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get().UpdateAsync(persisted).ConfigureAwait(false);
        }
    }
}
