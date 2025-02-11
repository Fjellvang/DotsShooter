// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Message;
using Metaplay.Core.Network;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;

namespace Cloud.Tests
{
    public class WireProtocolTests
    {
        /// <summary>
        /// Client Hello should not change accidentally.
        /// </summary>
        [Test]
        public void EnsureClientHelloByteFormat()
        {
            Handshake.ClientHello hello = new Handshake.ClientHello(
                buildVersion:               "123-test",
                buildNumber:                "345-build",
                clientVersion:              new ClientVersion(10, 0),
                fullProtocolHash:           0xAABBCCDD,
                commitId:                   "678-git",
                timestamp:                  new DateTime(2020, 1, 1, 11, 30, 12, 345, 789, DateTimeKind.Utc),
                appLaunchId:                123,
                clientSessionNonce:         456,
                clientSessionConnectionNdx: 1,
                platform:                   ClientPlatform.Android,
                loginProtocolVersion:       2,
                targetHostname:             "www.metaplay.io");

            byte[] payload = MetaSerialization.SerializeTagged<MetaMessage>(hello, MetaSerializationFlags.SendOverNetwork, logicVersion: null);
            byte[] packet = new byte[WireProtocol.PacketHeaderSize + payload.Length];
            WireProtocol.EncodePacketHeader(new WirePacketHeader(WirePacketType.Message, WirePacketCompression.None, payload.Length), packet);
            Buffer.BlockCopy(payload, 0, packet, WireProtocol.PacketHeaderSize, payload.Length);

            Assert.AreEqual(new byte[]
            {
                1,0,0,107,14,10,12,2,16,49,50,51,45,116,101,115,116,12,4,18,51,52,53,45,98,117,105,108,100,15,6,2,2,2,
                20,2,4,20,17,2,8,221,153,239,213,10,12,10,14,54,55,56,45,103,105,116,16,12,2,2,242,187,246,141,236,91,
                17,2,14,123,2,16,200,3,2,18,1,2,20,4,2,22,4,12,24,30,119,119,119,46,109,101,116,97,112,108,97,121,46,
                105,111,16,26,2,2,20,2,4,0,17,17,
            }, packet);
        }

        /// <summary>
        /// Should be able to parse a handshake that has custom magic and is missing project name.
        /// </summary>
        [Test]
        public void ParseServerHelloWithMagicAndNoProjectName()
        {
            byte[] payload = new byte[]
            {
                73, 68, 76, 82, 10, 3, 0, 0, // IDLR
                1, 0, 0, 31, 14, 8, 12, 2, 14, 48, 46, 49, 46, 48, 46, 48, 12, 4, 10, 108, 111, 99, 97, 108, 2, 6, 148, 229, 219, 255, 13, 12, 8, 1, 17
            };
            Handshake.ServerHello hello = Parse(payload, "IDLR", ProtocolStatus.ClusterRunning);

            Assert.AreEqual(hello.ServerVersion, "0.1.0.0");
            Assert.AreEqual(hello.BuildNumber, "local");
            Assert.AreEqual(hello.FullProtocolHash, 3757503124);
            Assert.AreEqual(hello.CommitId, null);
            Assert.AreEqual(hello.ProjectName, null);
        }

        /// <summary>
        /// Should be able to parse a handshake that has common magic and a project name.
        /// </summary>
        [Test]
        public void ParseV10HeaderWithGenericMagicAndProjectName()
        {
            byte[] payload = new byte[]
            {
                77, 78, 80, 120, 10, 3, 0, 0, // MNPx
                1, 0, 0, 39, 14, 8, 12, 2, 14, 48, 46, 49, 46, 48, 46, 48, 12, 4, 10, 108, 111, 99, 97, 108, 2, 6, 151, 155, 205, 189, 6, 12, 8, 1, 12, 10, 10, 73, 100, 108, 101, 114, 17
            };
            Handshake.ServerHello hello = Parse(payload, "xxxx", ProtocolStatus.ClusterRunning);

            Assert.AreEqual(hello.ServerVersion, "0.1.0.0");
            Assert.AreEqual(hello.BuildNumber, "local");
            Assert.AreEqual(hello.FullProtocolHash, 1739804055);
            Assert.AreEqual(hello.CommitId, null);
            Assert.AreEqual(hello.ProjectName, "Idler");
        }

        Handshake.ServerHello Parse(byte[] buffer, string expectedGameMagic, ProtocolStatus expectedProtocolStatus)
        {
            ProtocolStatus protocolStatus = WireProtocol.ParseProtocolHeader(buffer, 0, expectedGameMagic);
            Assert.AreEqual(expectedProtocolStatus, protocolStatus);

            WirePacketHeader header = WireProtocol.DecodePacketHeader(buffer, WireProtocol.ProtocolHeaderSize, enforcePacketPayloadSizeLimit: false);
            return (Handshake.ServerHello)WireProtocol.DecodeMessage(buffer, WireProtocol.ProtocolHeaderSize+WireProtocol.PacketHeaderSize, header.PayloadSize, resolver: null);
        }
    }
}
