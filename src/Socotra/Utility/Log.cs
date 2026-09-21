using System.Diagnostics;

namespace Socotra;

internal static class Log
{
    public static void Warning(string message) => Trace.TraceWarning($"Socotra: {message}");

    public static void Error(Exception exception) => Trace.TraceError($"Socotra: {exception}");
}
