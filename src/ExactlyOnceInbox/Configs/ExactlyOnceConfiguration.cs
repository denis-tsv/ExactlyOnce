namespace ExactlyOnceInbox.Configs;

public class ExactlyOnceConfiguration
{
    public TimeSpan LockedDelay { get; set; }
    public TimeSpan NoKafkaMessagesDelay { get; set; }
    public TimeSpan NoInboxMessagesDelay { get; set; }
    public int BatchSize { get; set; }
}