using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Tests.Domain;

/// <summary>Comprueba las validaciones, normalización y estado inicial de MusicProject.</summary>
public sealed class MusicProjectTests
{
    [Fact]
    public void ConstructorTrimsDetailsAndStartsAsDraft()
    {
        var project = new MusicProject("  Night Drive  ", "  Lo-fi  ", 3600, "  Calm  ");

        Assert.Equal("Night Drive", project.Title);
        Assert.Equal("Lo-fi", project.Genre);
        Assert.Equal("Calm", project.Mood);
        Assert.Equal(ProjectStatus.Draft, project.Status);
        Assert.Equal(project.CreatedAt, project.UpdatedAt);
    }

    [Theory]
    [InlineData(" ", "Lo-fi", 120)]
    [InlineData("Title", " ", 120)]
    [InlineData("Title", "Lo-fi", 0)]
    public void ConstructorRejectsInvalidRequiredDetails(string title, string genre, int duration)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MusicProject(title, genre, duration));
    }

    [Fact]
    public void UpdateDetailsNormalizesOptionalMood()
    {
        var project = new MusicProject("Title", "Genre", 120, "Mood");

        project.UpdateDetails(" New title ", " Ambient ", "  ", 240);

        Assert.Equal("New title", project.Title);
        Assert.Equal("Ambient", project.Genre);
        Assert.Null(project.Mood);
        Assert.Equal(240, project.TargetDurationSeconds);
    }
}
