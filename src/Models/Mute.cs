namespace Furien_Admin.Models;

public class Mute
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public ulong AdminSteamId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public MuteStatus Status { get; set; } = MuteStatus.Active;
    public string? UnmuteAdminName { get; set; }
    public ulong? UnmuteAdminSteamId { get; set; }
    public string? UnmuteReason { get; set; }
    public DateTime? UnmuteDate { get; set; }

    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsActive => Status == MuteStatus.Active && !IsExpired;
    public TimeSpan? TimeRemaining => ExpiresAt?.Subtract(DateTime.UtcNow);
}

public enum MuteStatus
{
    Active,
    Expired,
    Unmuted
}
