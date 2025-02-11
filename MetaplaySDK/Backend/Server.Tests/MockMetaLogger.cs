// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using NUnit.Framework;
using System;

namespace Metaplay.Server.Tests;

public class MockMetaLogger : IMetaLogger
{
    public bool IsVerboseEnabled     => true;
    public bool IsDebugEnabled       => true;
    public bool IsInformationEnabled => true;
    public bool IsWarningEnabled     => true;
    public bool IsErrorEnabled       => true;
    public bool IsLevelEnabled(LogLevel level) => true;

    public int NumWarnings = 0;
    public int NumErrors   = 0;
    public int NumMessages = 0;

    public void LogEvent(LogLevel level, Exception ex, string format, params object[] args)
    {
        NumMessages++;
        if (level == LogLevel.Error)
            NumErrors++;
        if (level == LogLevel.Warning)
            NumWarnings++;
    }

    public void AssertNoFails()
    {
        Assert.AreEqual(0, NumWarnings);
        Assert.AreEqual(0, NumErrors);
    }
}
