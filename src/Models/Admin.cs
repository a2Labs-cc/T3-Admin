namespace Furien_Admin.Models;

public class Admin
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Immunity { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? AddedBy { get; set; }
    public ulong? AddedBySteamId { get; set; }

    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsActive => !IsExpired;
}
