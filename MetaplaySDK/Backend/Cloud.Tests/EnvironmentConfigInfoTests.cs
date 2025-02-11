// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Client;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Tests
{
    internal class EnvironmentConfigInfoTests
    {
        [Test]
        public void Empty()
        {
            Assert.Catch<ArgumentException>(() => MetaplayEnvironmentConfigInfo.ParseFromJson(""));
        }

        [Test]
        public void Garbage()
        {
            Assert.Catch<ArgumentException>(() => MetaplayEnvironmentConfigInfo.ParseFromJson(",,,,,"));
        }

        [Test]
        public void InvalidEnvironmentFamily()
        {
            string data = "{\"EnvironmentFamily\":\"ThisIsAnInvalidValue\",\"EnvironmentId\":\"develop\",\"ServerHost\":\"idler-develop.p1.metaplay.io\",\"ServerPorts\":[9339],\"ServerPortsForWebSocket\":[9380],\"EnableTls\":true,\"CdnBaseUrl\":\"https://idler-develop-assets.p1.metaplay.io/\",\"AdminApiBaseUrl\":\"https://idler-develop-admin.p1.metaplay.io/api/\", \"AdminApiUseOpenIdConnectIdToken\": true, \"OAuth2ClientID\": \"metaplay.idler.develop\", \"OAuth2ClientSecret\": \"abcde\", \"OAuth2Audience\": \"metaplay.idler.develop\", \"OAuth2AuthorizationEndpoint\": \"https://auth.metaplay.dev/oauth2/auth\", \"OAuth2TokenEndpoint\": \"https://auth.metaplay.dev/oauth2/token\", \"OAuth2LocalCallback\": \"http://localhost:42543/oauth2/\", \"OAuth2UseStateParameter\": true}";
            Assert.Catch<ArgumentException>(() => MetaplayEnvironmentConfigInfo.ParseFromJson(data));
        }

        [Test]
        public void MissingValue()
        {
            string data = "{\"EnvironmentFamily\":\"Development\",\"EnvironmentId\":\"\",\"ServerHost\":\"idler-develop.p1.metaplay.io\",\"ServerPorts\":[9339],\"ServerPortsForWebSocket\":[9380],\"EnableTls\":true,\"CdnBaseUrl\":\"https://idler-develop-assets.p1.metaplay.io/\",\"AdminApiBaseUrl\":\"https://idler-develop-admin.p1.metaplay.io/api/\", \"AdminApiUseOpenIdConnectIdToken\": true, \"OAuth2ClientID\": \"metaplay.idler.develop\", \"OAuth2ClientSecret\": \"abcde\", \"OAuth2Audience\": \"metaplay.idler.develop\", \"OAuth2AuthorizationEndpoint\": \"https://auth.metaplay.dev/oauth2/auth\", \"OAuth2TokenEndpoint\": \"https://auth.metaplay.dev/oauth2/token\", \"OAuth2LocalCallback\": \"http://localhost:42543/oauth2/\", \"OAuth2UseStateParameter\": true}";
            Assert.Catch<ArgumentException>(() => MetaplayEnvironmentConfigInfo.ParseFromJson(data));
        }

        [Test]
        public void ValidData()
        {
            string data = "{\"EnvironmentFamily\":\"Development\",\"EnvironmentId\":\"develop\",\"ServerHost\":\"idler-develop.p1.metaplay.io\",\"ServerPorts\":[9339],\"ServerPortsForWebSocket\":[9380],\"EnableTls\":true,\"CdnBaseUrl\":\"https://idler-develop-assets.p1.metaplay.io/\",\"AdminApiBaseUrl\":\"https://idler-develop-admin.p1.metaplay.io/api/\", \"AdminApiUseOpenIdConnectIdToken\": true, \"OAuth2ClientID\": \"metaplay.idler.develop\", \"OAuth2ClientSecret\": \"abcde\", \"OAuth2Audience\": \"metaplay.idler.develop\", \"OAuth2AuthorizationEndpoint\": \"https://auth.metaplay.dev/oauth2/auth\", \"OAuth2TokenEndpoint\": \"https://auth.metaplay.dev/oauth2/token\", \"OAuth2LocalCallback\": \"http://localhost:42543/oauth2/\", \"OAuth2UseStateParameter\": true}";
            Assert.DoesNotThrow(() => MetaplayEnvironmentConfigInfo.ParseFromJson(data));
            MetaplayEnvironmentConfigInfo info = MetaplayEnvironmentConfigInfo.ParseFromJson(data)[0];
            Assert.IsNotNull(info);
            Assert.AreEqual(info.EnvironmentFamily, EnvironmentFamily.Development);
            Assert.AreEqual(info.EnvironmentId, "develop");
            Assert.AreEqual(info.ServerHost, "idler-develop.p1.metaplay.io");
            Assert.AreEqual(info.ServerPorts, new int[]{9339});
            Assert.AreEqual(info.ServerPortsForWebSocket, new int[]{9380});
            Assert.AreEqual(info.EnableTls, true);
            Assert.AreEqual(info.CdnBaseUrl, "https://idler-develop-assets.p1.metaplay.io/");
            Assert.AreEqual(info.AdminApiBaseUrl, "https://idler-develop-admin.p1.metaplay.io/api/");
            Assert.AreEqual(info.AdminApiUseOpenIdConnectIdToken, true);
            Assert.AreEqual(info.OAuth2ClientID, "metaplay.idler.develop");
            Assert.AreEqual(info.OAuth2ClientSecret, "abcde");
            Assert.AreEqual(info.OAuth2Audience, "metaplay.idler.develop");
            Assert.AreEqual(info.OAuth2AuthorizationEndpoint, "https://auth.metaplay.dev/oauth2/auth");
            Assert.AreEqual(info.OAuth2TokenEndpoint, "https://auth.metaplay.dev/oauth2/token");
            Assert.AreEqual(info.OAuth2LocalCallback, "http://localhost:42543/oauth2/");
            Assert.AreEqual(info.OAuth2UseStateParameter, true);
        }
    }
}
