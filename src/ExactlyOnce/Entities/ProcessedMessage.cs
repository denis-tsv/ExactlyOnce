namespace ExactlyOnce.Entities;

public class ProcessedMessage
{
    public string IdempotenceKey { get; set; } = null!;
}