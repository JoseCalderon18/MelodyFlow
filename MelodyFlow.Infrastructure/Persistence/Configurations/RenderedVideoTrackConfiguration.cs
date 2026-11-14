using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MelodyFlow.Infrastructure.Persistence.Configurations;

/// <summary>Persiste las canciones seleccionadas y su orden en cada videoclip.</summary>
public sealed class RenderedVideoTrackConfiguration : IEntityTypeConfiguration<RenderedVideoTrack>
{
    public void Configure(EntityTypeBuilder<RenderedVideoTrack> builder)
    {
        builder.ToTable("rendered_video_tracks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(item => item.RenderedVideoId).HasColumnName("rendered_video_id").IsRequired();
        builder.Property(item => item.MusicTrackId).HasColumnName("music_track_id").IsRequired();
        builder.Property(item => item.SequenceNumber).HasColumnName("sequence_number").IsRequired();

        builder.HasOne<MusicTrack>().WithMany()
            .HasForeignKey(item => item.MusicTrackId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.RenderedVideoId, item.SequenceNumber })
            .IsUnique()
            .HasDatabaseName("ux_rendered_video_tracks_video_sequence");
        builder.HasIndex(item => item.MusicTrackId)
            .HasDatabaseName("ix_rendered_video_tracks_music_track_id");
    }
}
