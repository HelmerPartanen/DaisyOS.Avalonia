using DaisyOS.Core.Models;
using DaisyOS.Shell.Helpers;
using Xunit;

namespace DaisyOS.Tests;

public sealed class MediaArtworkLoaderTests
{
    [Fact]
    public void SelectArtworkUri_SpotifyPrefersMprisArtwork()
    {
        var session = Session(
            artwork: "https://i.scdn.co/image/cover",
            identity: "spotify",
            sourceUrl: "https://open.spotify.com/track/example");

        Assert.Equal("https://i.scdn.co/image/cover", MediaArtworkLoader.SelectArtworkUri(session)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://open.spotify.com/track/abc123", "https://open.spotify.com/oembed?url=https%3A%2F%2Fopen.spotify.com%2Ftrack%2Fabc123")]
    [InlineData("spotify:track:abc123", "https://open.spotify.com/oembed?url=https%3A%2F%2Fopen.spotify.com%2Ftrack%2Fabc123")]
    [InlineData("/com/spotify/track/abc123", "https://open.spotify.com/oembed?url=https%3A%2F%2Fopen.spotify.com%2Ftrack%2Fabc123")]
    public void SelectSpotifyOEmbedUri_UsesTrackEntity(string sourceUrl, string expected)
    {
        var session = Session("file:///tmp/spotify.png", "spotify", sourceUrl);

        Assert.Equal(expected, MediaArtworkLoader.SelectSpotifyOEmbedUri(session)?.AbsoluteUri);
    }

    [Theory]
    [InlineData("spotify:playlist:abc123")]
    [InlineData("https://open.spotify.com/playlist/abc123")]
    [InlineData("spotify:album:abc123")]
    public void SelectSpotifyOEmbedUri_DoesNotUseCollectionArtwork(string sourceUrl)
    {
        var session = Session(null, "spotify", sourceUrl);

        Assert.Null(MediaArtworkLoader.SelectSpotifyOEmbedUri(session));
    }

    [Theory]
    [InlineData("firefox", "https://www.youtube.com/watch?v=example", "https://www.youtube.com/favicon.ico")]
    [InlineData("chromium", "https://www.netflix.com/watch/123", "https://www.netflix.com/favicon.ico")]
    [InlineData("org.mpris.MediaPlayer2.plasma-browser-integration", "https://vimeo.com/123", "https://vimeo.com/favicon.ico")]
    public void SelectArtworkUri_BrowserVideoUsesSiteFavicon(string identity, string sourceUrl, string expected)
    {
        var session = Session("https://example.com/thumbnail.jpg", identity, sourceUrl);

        Assert.Equal(expected, MediaArtworkLoader.SelectArtworkUri(session)?.AbsoluteUri);
    }

    [Fact]
    public void SelectArtworkUri_NativePlayerUsesProvidedArtwork()
    {
        var session = Session("file:///tmp/cover.png", "vlc", null);

        Assert.Equal("file:///tmp/cover.png", MediaArtworkLoader.SelectArtworkUri(session)?.AbsoluteUri);
    }

    [Fact]
    public void SelectArtworkUri_MissingMetadataHasNoArtwork()
    {
        Assert.Null(MediaArtworkLoader.SelectArtworkUri(Session(null, null, null)));
    }

    [Theory]
    [InlineData("https://open.spotify.com/favicon.ico", true)]
    [InlineData("https://open.spotify.com/icons/icon-192.png", true)]
    [InlineData("https://open.spotify.com/image/cover-id", false)]
    [InlineData("https://i.scdn.co/image/cover-id", false)]
    public void IsLikelyGenericIcon_DistinguishesSpotifyCoverArt(string artwork, bool expected)
    {
        var session = Session(artwork, "spotify", "https://open.spotify.com/");

        Assert.Equal(expected, MediaArtworkLoader.IsLikelyGenericIcon(session, new Uri(artwork)));
    }

    private static MediaSession Session(string? artwork, string? identity, string? sourceUrl) =>
        new("Title", "Artist", artwork, 0, 100, true, identity, sourceUrl);
}
