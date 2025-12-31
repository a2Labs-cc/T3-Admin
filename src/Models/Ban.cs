namespace Furien_Admin.Models;

public class Ban
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public ulong AdminSteamId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public BanStatus Status { get; set; } = BanStatus.Active;
    public string? UnbanAdminName { get; set; }
    public ulong? UnbanAdminSteamId { get; set; }
    public string? UnbanReason { get; set; }
    public DateTime? UnbanDate { get; set; }

    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsActive => Status == BanStatus.Active && !IsExpired;
    public TimeSpan? TimeRemaining => ExpiresAt?.Subtract(DateTime.UtcNow);
}

public enum BanStatus
{
    Active,
    Expired,
    Unbanned
}
