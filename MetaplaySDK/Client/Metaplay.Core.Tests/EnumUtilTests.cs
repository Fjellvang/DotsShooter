// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;

namespace Metaplay.Core.Tests
{
    class EnumUtilTests
    {
        [Test]
        public void Parse()
        {
            Assert.That(EnumUtil.Parse<WireDataType>("Invalid"), Is.EqualTo(WireDataType.Invalid));
            Assert.That(EnumUtil.Parse<WireDataType>("VarInt"), Is.EqualTo(WireDataType.VarInt));
            Assert.Throws<ArgumentException>(() => EnumUtil.Parse<WireDataType>("varint"));
            Assert.Throws<ArgumentException>(() => EnumUtil.Parse<WireDataType>("NonExistentValue"));
        }

        [Test]
        public void ParseCaseInsensitive()
        {
            Assert.That(EnumUtil.ParseCaseInsensitive<WireDataType>("VarInt"), Is.EqualTo(WireDataType.VarInt));
            Assert.That(EnumUtil.ParseCaseInsensitive<WireDataType>("varint"), Is.EqualTo(WireDataType.VarInt));
            Assert.Throws<ArgumentException>(() => EnumUtil.ParseCaseInsensitive<WireDataType>("nonexistentvalue"));
        }

        [Test]
        public void EnumToInt()
        {
            Assert.That(EnumUtil.ToInt(WireDataType.Invalid), Is.EqualTo(0));
            Assert.That(EnumUtil.ToInt(WireDataType.Null), Is.EqualTo(1));
        }

        [Test]
        public void EnumFromInt()
        {
            Assert.That(EnumUtil.FromInt<WireDataType>(0), Is.EqualTo(WireDataType.Invalid));
            Assert.That(EnumUtil.FromInt<WireDataType>(1), Is.EqualTo(WireDataType.Null));
        }
    }
}
