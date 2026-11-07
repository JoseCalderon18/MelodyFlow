using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Define nombres, longitudes y restricción de duración de la tabla de proyectos.</summary>
public sealed class MusicProjectConfiguration : IEntityTypeConfiguration<MusicProject>
{
    public void Configure(EntityTypeBuilder<MusicProject> builder)
    {
        builder.ToTable("music_projects", table =>
            table.HasCheckConstraint("ck_music_projects_target_duration_positive", "target_duration_seconds > 0"));

        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(project => project.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(project => project.Genre).HasColumnName("genre").HasMaxLength(80).IsRequired();
        builder.Property(project => project.Mood).HasColumnName("mood").HasMaxLength(80);
        builder.Property(project => project.TargetDurationSeconds).HasColumnName("target_duration_seconds").IsRequired();
        builder.Property(project => project.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();
        builder.Property(project => project.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(project => project.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
