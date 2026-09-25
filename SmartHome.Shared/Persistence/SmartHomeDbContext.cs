using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;

namespace SmartHome.Shared.Persistence;

public sealed class SmartHomeDbContext(DbContextOptions<SmartHomeDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<OauthCode> OauthCodes => Set<OauthCode>();

    public DbSet<OauthToken> OauthTokens => Set<OauthToken>();

    public DbSet<HistorialReproduccion> HistorialReproducciones => Set<HistorialReproduccion>();

    public DbSet<MiniDTO> Minis => Set<MiniDTO>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Username).HasMaxLength(100).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(item => item.AgentUserId).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.Username).IsUnique();
            entity.HasIndex(item => item.AgentUserId).IsUnique();
        });

        modelBuilder.Entity<OauthCode>(entity =>
        {
            entity.ToTable("OauthCodes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(200).IsRequired();
            entity.Property(item => item.AgentUserId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ExpiresAt).IsRequired();
            entity.Property(item => item.IsUsed).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<OauthToken>(entity =>
        {
            entity.ToTable("OauthTokens");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AccessToken).HasMaxLength(500).IsRequired();
            entity.Property(item => item.RefreshToken).HasMaxLength(500).IsRequired();
            entity.Property(item => item.AgentUserId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.AccessExpiresAt).IsRequired();
            entity.HasIndex(item => item.AccessToken).IsUnique();
            entity.HasIndex(item => item.RefreshToken).IsUnique();
        });

        modelBuilder.Entity<HistorialReproduccion>(entity =>
        {
            entity.ToTable("HistorialReproduccion");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.QueryTexto).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Fecha).IsRequired();
            entity.Property(item => item.Exitoso).IsRequired();
            entity.HasIndex(item => item.Fecha);
        });
    }
}
