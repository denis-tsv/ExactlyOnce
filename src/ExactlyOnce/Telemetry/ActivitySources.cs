using System.Diagnostics;

namespace ExactlyOnce.Telemetry;


public static class ActivitySources
{
    public const string ExactlyOnceSourceName = "ExactlyOnce";
    public static readonly ActivitySource ExactlyOnce = new(ExactlyOnceSourceName);
}