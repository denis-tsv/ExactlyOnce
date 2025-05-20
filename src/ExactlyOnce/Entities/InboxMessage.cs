namespace ExactlyOnce.Entities;

public class InboxMessage
{
    public int Id { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public string IdempotenceKey { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public Dictionary<string, string> Headers { get; set; } = null!;
    public DateTime CreatedAt { get; set; }   
}