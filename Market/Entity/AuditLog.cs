namespace Market.Entity;

public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; } 

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string OldValues { get; set; } = string.Empty;

    public string NewValues { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}