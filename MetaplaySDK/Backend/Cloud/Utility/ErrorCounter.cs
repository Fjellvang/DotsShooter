// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Text;
using static System.FormattableString;

namespace Metaplay.Cloud.Utility
{
    /// <summary>
    /// Counts errors by type, and provides a string representation of the errors.
    /// </summary>
    public class ErrorCounter
    {
        class ErrorInfo
        {
            public string Message;
            public int Count;
        }

        public int Count { get; private set; }
        Dictionary<Type, ErrorInfo> InternalErrors { get; set; }

        /// <summary>
        /// Record an error. If the error type is already recorded, increment the count.
        /// Due to some exceptions sharing common types, every unique error might not be recorded properly,
        /// but it should be good enough to alert the developer that something is wrong.
        /// </summary>
        public void Increment(Exception e)
        {
            Count++;
            InternalErrors ??= new Dictionary<Type, ErrorInfo>();

            if (InternalErrors.TryGetValue(e.GetType(), out ErrorInfo errorInfo))
                errorInfo.Count++;
            else
            {
                if (InternalErrors.Count >= 50) // Don't store more than 50 different error types.
                    return;

                InternalErrors[e.GetType()] = new ErrorInfo { Message = e.Message, Count = 1 };
            }
        }

        /// <summary>
        /// Return a string representation of the recorded errors.
        /// </summary>
        /// <remarks>
        /// This method returns a multiline string. For log message boundary detection, each line is prefixed with
        /// one space character. This allows log processing to detect continuation lines.
        /// </remarks>
        public string GetErrorString()
        {
            if (InternalErrors == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach ((Type type, ErrorInfo info) in InternalErrors)
                sb.AppendLine(Invariant($" Type: {type} Count: {info.Count} Message: {info.Message}"));

            return sb.ToString();
        }
    }
}
