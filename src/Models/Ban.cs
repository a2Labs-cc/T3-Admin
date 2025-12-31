using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Furien_Admin.Models;

[Table("t3_bans")]
public class Ban
{
    [Key]
    public int Id { get; set; }

    [Column("steamid")]
    public ulong SteamId { get; set; }

    [Column("admin_name")]
    public string AdminName { get; set; } = string.Empty;

    [Column("admin_steamid")]
    public ulong AdminSteamId { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("status")]
    public BanStatus Status { get; set; } = BanStatus.Active;

    [Column("unban_admin_name")]
    public string? UnbanAdminName { get; set; }

    [Column("unban_admin_steamid")]
    public ulong? UnbanAdminSteamId { get; set; }

    [Column("unban_reason")]
    public string? UnbanReason { get; set; }

    [Column("unban_date")]
    public DateTime? UnbanDate { get; set; }

    [NotMapped]
    public bool IsPermanent => ExpiresAt == null;
    
    [NotMapped]
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    
    [NotMapped]
    public bool IsActive => Status == BanStatus.Active && !IsExpired;
    
    [NotMapped]
    public TimeSpan? TimeRemaining => ExpiresAt?.Subtract(DateTime.UtcNow);
}

public enum BanStatus
{
    Active,
    Expired,
    Unbanned
}
