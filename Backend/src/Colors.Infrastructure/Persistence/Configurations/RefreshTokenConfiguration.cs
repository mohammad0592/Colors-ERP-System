using Colors.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Colors.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ReplacedByTokenHash)
            .HasMaxLength(64);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();

        // Every refresh looks a token up by its hash, so this index carries the load.
        // Unique because two tokens can never hash to the same value.
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_hash");

        // Used when a stolen token forces every session for one worker to be revoked.
        builder.HasIndex(t => t.UserId)
            .HasDatabaseName("ix_refresh_tokens_user");

        // Deleting a user removes their tokens. This is the one place a cascade is
        // right: a token has no meaning without the person it belongs to. Users are
        // deactivated rather than deleted anyway (specification section 16).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
