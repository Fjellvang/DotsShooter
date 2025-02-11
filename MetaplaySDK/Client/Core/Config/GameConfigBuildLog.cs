// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.FormattableString;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// An individual log entry in <see cref="GameConfigBuildLog"/>. Contains the relevant
    /// metadata of where (in data and in code) the message is logged. This is serializable
    /// so that it can be included in <see cref="GameConfigBuildReport"/>.
    /// </summary>
    [MetaSerializable]
    public class GameConfigBuildMessage
    {
        // Context information that message is associated with
        [MetaMember(1)] public string       SourceInfo;
        [MetaMember(6)] public string       ShortSource;
        [MetaMember(2)] public string       SourceLocation;
        [MetaMember(3)] public string       LocationUrl;
        [MetaMember(4)] public string       ItemId;             // \todo Can we get this to match ConfigKey -- would need to parse keys early
        [MetaMember(5)] public string       VariantId;

        // Message content
        [MetaMember(10)] public GameConfigLogLevel  Level;
        [MetaMember(11)] public string              Message;
        [MetaMember(12)] public string              Exception;

        // Context where error occurred in code
        [MetaMember(20)] public string      CallerFileName;
        [MetaMember(21)] public string      CallerMemberName;
        [MetaMember(22)] public int         CallerLineNumber;

        GameConfigBuildMessage() { }

        public GameConfigBuildMessage(
            string sourceInfo, string shortSource, string sourceLocation, string locationUrl, string itemId, string variantId,
            GameConfigLogLevel level, string message, string exception,
            string callerFileName, string callerMemberName, int callerLineNumber)
        {
            SourceInfo = sourceInfo;
            ShortSource = shortSource;
            SourceLocation = sourceLocation;
            LocationUrl = locationUrl;
            ItemId = itemId;
            VariantId = variantId;

            Level = level;
            Message = message;
            Exception = exception;

            CallerFileName = callerFileName;
            CallerMemberName = callerMemberName;
            CallerLineNumber = callerLineNumber;
        }

        public override string ToString()
        {
            string variantStr       = VariantId != null ? $" variant {VariantId}" : "";
            string itemStr          = ItemId != null ? $"Item '{ItemId}'{variantStr} in " : "";
            string sourceStr        = (SourceInfo != null || ItemId != null) ? $"\n  Source: {itemStr}{ShortSource}" : "";
            string locationUrlStr   = LocationUrl != null ? $"\n  Link: {LocationUrl}" : "";
            string exceptionStr     = Exception != null ? $"\n  {Exception}" : "";
            return Invariant($"{Level}: {Message}{sourceStr}{locationUrlStr}{exceptionStr}");
        }
    }

    /// <summary>
    /// Collects/contains the output of a game config build, from the fetching through to validating
    /// the individual items. The build messages are of type <see cref="GameConfigBuildMessage"/>.
    /// </summary>
    public class GameConfigBuildLog
    {
        class SharedErrorCounter
        {
            public int NumErrorMessages;
            public int NumWarningMessages;
        }

        // Context (each copy tracks its own context)
        public GameConfigSourceInfo             SourceInfo  { get; private set; }
        public GameConfigSyntaxTree.ObjectId?   ItemId      { get; private set; }
        public string                           VariantId   { get; private set; }
        public GameConfigSourceLocation         Location    { get; private set; }

        // State (shared across all copies)
        List<GameConfigBuildMessage> _messages;
        SharedErrorCounter _errorCounter;

        // Not shared
        int _numErrorsAtStartOfLocalErrorScope;

        public IEnumerable<GameConfigBuildMessage> Messages => _messages;

        public GameConfigBuildLog()
        {
            _messages = new List<GameConfigBuildMessage>();
            _errorCounter = new SharedErrorCounter();
            _numErrorsAtStartOfLocalErrorScope = -1;
        }

        /// <summary>
        /// Used to create a sub-log for another build log with an updated (usually narrowed) context. Note that
        /// the messages is shared with the parent so that all messages go into the same log.
        /// </summary>
        /// <param name="sourceInfo"></param>
        /// <param name="itemId"></param>
        /// <param name="variantId"></param>
        /// <param name="location"></param>
        GameConfigBuildLog(GameConfigSourceInfo sourceInfo, GameConfigSyntaxTree.ObjectId? itemId, string variantId, GameConfigSourceLocation location, GameConfigBuildLog parent)
        {
            SourceInfo = sourceInfo;
            ItemId = itemId;
            VariantId = variantId;
            Location = location;

            _messages = parent._messages;
            _errorCounter = parent._errorCounter;
        }

        public GameConfigBuildLog WithSource(GameConfigSourceInfo sourceInfo) => new GameConfigBuildLog(sourceInfo, ItemId, VariantId, Location, this);
        public GameConfigBuildLog WithItemId(GameConfigSyntaxTree.ObjectId itemId) => new GameConfigBuildLog(SourceInfo, itemId, VariantId, Location, this);
        public GameConfigBuildLog WithVariantId(string variantId) => new GameConfigBuildLog(SourceInfo, ItemId, variantId, Location, this);
        public GameConfigBuildLog WithLocation(GameConfigSourceLocation location)
        {
            GameConfigSourceInfo sourceInfo = location?.SourceInfo ?? SourceInfo;
            return new GameConfigBuildLog(sourceInfo, ItemId, VariantId, location, this);
        }
        public GameConfigBuildLog WithNewLocalErrorScope()
        {
            int numErrorsAtStartOfLocalErrorScope = _errorCounter.NumErrorMessages;
            GameConfigBuildLog log = new GameConfigBuildLog(SourceInfo, ItemId, VariantId, Location, this);
            log._numErrorsAtStartOfLocalErrorScope = numErrorsAtStartOfLocalErrorScope;
            return log;
        }

        public bool HasErrors() => _errorCounter.NumErrorMessages > 0;
        public bool HasWarnings() => _errorCounter.NumWarningMessages > 0;

        /// <summary>
        /// Returns true if there has been any errors since the creation of this log <see cref="WithNewLocalErrorScope"/>
        /// </summary>
        public bool HasLocalErrors()
        {
            if (_numErrorsAtStartOfLocalErrorScope == -1)
                throw new InvalidOperationException("Cannot use HasLocalErrors() without calling WithNewLocalErrorScope().");
            return _numErrorsAtStartOfLocalErrorScope != _errorCounter.NumErrorMessages;
        }

        void AddMessage(GameConfigLogLevel level, string message, Exception exception, string callerFilePath, string callerMemberName, int callerLineNumber)
        {
            string locationUrl = (SourceInfo != null && Location != null) ? SourceInfo.GetLocationUrl(Location) : null;
            _messages.Add(new GameConfigBuildMessage(
                SourceInfo?.ToString(), SourceInfo?.GetShortName(), Location?.ToString(), locationUrl, ItemId?.ToString(), VariantId,
                level, message, exception?.ToString(),
                callerFilePath, callerMemberName, callerLineNumber));

            if (level == GameConfigLogLevel.Error)
                _errorCounter.NumErrorMessages++;
            else if (level == GameConfigLogLevel.Warning)
                _errorCounter.NumWarningMessages++;
        }

        public void Verbose(string message, Exception exception = null, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null, [CallerLineNumber] int callerLineNumber = 0) =>
            AddMessage(GameConfigLogLevel.Verbose, message, exception, callerFilePath, callerMemberName, callerLineNumber);

        public void Debug(string message, Exception exception = null, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null, [CallerLineNumber] int callerLineNumber = 0) =>
            AddMessage(GameConfigLogLevel.Debug, message, exception, callerFilePath, callerMemberName, callerLineNumber);

        public void Information(string message, Exception exception = null, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null, [CallerLineNumber] int callerLineNumber = 0) =>
            AddMessage(GameConfigLogLevel.Information, message, exception, callerFilePath, callerMemberName, callerLineNumber);

        public void Warning(string message, Exception exception = null, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null, [CallerLineNumber] int callerLineNumber = 0) =>
            AddMessage(GameConfigLogLevel.Warning, message, exception, callerFilePath, callerMemberName, callerLineNumber);

        public void Error(string message, Exception exception = null, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null, [CallerLineNumber] int callerLineNumber = 0) =>
            AddMessage(GameConfigLogLevel.Error, message, exception, callerFilePath, callerMemberName, callerLineNumber);
    }
}
