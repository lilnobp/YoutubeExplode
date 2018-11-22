﻿using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Internal;
using YoutubeExplode.Internal.Parsers;

namespace YoutubeExplode
{
    /// <summary>
    /// The entry point for <see cref="YoutubeExplode"/>.
    /// </summary>
    public partial class YoutubeClient : IYoutubeClient
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Creates an instance of <see cref="YoutubeClient"/>.
        /// </summary>
        public YoutubeClient(HttpClient httpClient)
        {
            _httpClient = httpClient.GuardNotNull(nameof(httpClient));
        }

        /// <summary>
        /// Creates an instance of <see cref="YoutubeClient"/>.
        /// </summary>
        public YoutubeClient()
            : this(HttpClientEx.GetSingleton())
        {
        }

        private async Task<VideoWatchPageParser> GetVideoWatchPageParserAsync(string videoId)
        {
            var url = $"https://www.youtube.com/watch?v={videoId}&disable_polymer=true&bpctr=9999999999&hl=en";
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            return VideoWatchPageParser.Initialize(raw);
        }

        private async Task<VideoInfoParser> GetVideoInfoParserAsync(string videoId, string el = "embedded")
        {
            // This parameter does magic and a lot of videos don't work without it
            var eurl = $"https://youtube.googleapis.com/v/{videoId}".UrlEncode();

            var url = $"https://www.youtube.com/get_video_info?video_id={videoId}&el={el}&eurl={eurl}&hl=en";
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            return VideoInfoParser.Initialize(raw);
        }

        private async Task<PlayerResponseParser> GetPlayerResponseParserAsync(string videoId, bool ensureIsPlayable = false)
        {
            // Get player response parser via video info
            var videoInfoParser = await GetVideoInfoParserAsync(videoId).ConfigureAwait(false);
            var playerResponseParser = videoInfoParser.GetPlayerResponse();

            // If the video is not available - throw exception
            if (!playerResponseParser.ParseIsAvailable())
            {
                var errorReason = playerResponseParser.ParseErrorReason();
                throw new VideoUnavailableException(videoId, $"Video [{videoId}] is unavailable. {errorReason}");
            }

            // If requested to ensure playability but the video is not playable - try again
            if (ensureIsPlayable && !playerResponseParser.ParseIsPlayable())
            {
                // Get player response parser via watch page
                var watchPageParser = await GetVideoWatchPageParserAsync(videoId).ConfigureAwait(false);
                playerResponseParser = watchPageParser.GetPlayerResponse();

                // If the video is still not playable - throw exception
                if (!playerResponseParser.ParseIsPlayable())
                {
                    var errorReason = playerResponseParser.ParseErrorReason();
                    throw new VideoUnplayableException(videoId, $"Video [{videoId}] is unplayable. {errorReason}");
                }
            }

            return playerResponseParser;
        }

        private async Task<DashManifestParser> GetDashManifestParserAsync(string dashManifestUrl)
        {
            var raw = await _httpClient.GetStringAsync(dashManifestUrl).ConfigureAwait(false);
            return DashManifestParser.Initialize(raw);
        }

        private async Task<ClosedCaptionTrackAjaxParser> GetClosedCaptionTrackAjaxParserAsync(string url)
        {
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
            return ClosedCaptionTrackAjaxParser.Initialize(raw);
        }

        private async Task<ChannelPageParser> GetChannelPageParserAsync(string channelId)
        {
            var url = $"https://www.youtube.com/channel/{channelId}";
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            return ChannelPageParser.Initialize(raw);
        }

        private async Task<ChannelPageParser> GetChannelPageParserByUsernameAsync(string username)
        {
            username = username.UrlEncode();

            var url = $"https://www.youtube.com/user/{username}";
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            return ChannelPageParser.Initialize(raw);
        }

        private async Task<PlaylistAjaxParser> GetPlaylistAjaxParserAsync(string playlistId, int index)
        {
            var url = $"https://www.youtube.com/list_ajax?style=json&action_get_list=1&list={playlistId}&index={index}&hl=en";
            var raw = await _httpClient.GetStringAsync(url).ConfigureAwait(false);

            return PlaylistAjaxParser.Initialize(raw);
        }

        private async Task<PlaylistAjaxParser> GetPlaylistAjaxParserForSearchAsync(string query, int page)
        {
            query = query.UrlEncode();

            // Don't ensure success here so that empty pages could be parsed

            var url = $"https://www.youtube.com/search_ajax?style=json&search_query={query}&page={page}&hl=en";
            var raw = await _httpClient.GetStringAsync(url, false).ConfigureAwait(false);

            return PlaylistAjaxParser.Initialize(raw);
        }
    }
}