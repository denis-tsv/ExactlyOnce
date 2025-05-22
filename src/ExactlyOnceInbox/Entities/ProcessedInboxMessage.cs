namespace ExactlyOnceInbox.Entities;

public class ProcessedInboxMessage
{
    public string IdempotenceKey { get; set; } = null!;
}