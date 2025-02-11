// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;

namespace Metaplay.Cloud.Sharding
{
    public static class EntityStackTraceUtil
    {
        public static string ExceptionToEntityStackTrace(Exception ex)
        {
            string stackTrace;
            if (ex.InnerException != null)
            {
                // \note: 3 spaces with normal lines. Starting with whitespace also keeps all lines as a single log entry
                stackTrace = ex.InnerException.ToString() + "\n   --- End of inner exception stack trace ---\n" + ex.StackTrace;
            }
            else
                stackTrace = ex.StackTrace;

            return stackTrace;
        }
    }
}
