namespace ExactlyOnce.Entities;

public class InboxMessageOffset
{
    public int Id { get; set; }
    public string Topic { get; set; } = null!;
    public int Partition { get; set; }
    public long LastProcessedOffset { get; set; }
    public DateTime AvailableAfter { get; set; }
}