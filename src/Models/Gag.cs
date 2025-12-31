namespace Furien_Admin.Models;

public class Gag
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public ulong AdminSteamId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public GagStatus Status { get; set; } = GagStatus.Active;
    public string? UngagAdminName { get; set; }
    public ulong? UngagAdminSteamId { get; set; }
    public string? UngagReason { get; set; }
    public DateTime? UngagDate { get; set; }

    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsActive => Status == GagStatus.Active && !IsExpired;
    public TimeSpan? TimeRemaining => ExpiresAt?.Subtract(DateTime.UtcNow);
}

public enum GagStatus
{
    Active,
    Expired,
    Ungagged
}
