﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RadioTracklistsOnSpotify.Services.SpotifyClientService.DTOs;

namespace RadioTracklistsOnSpotify.Services.SpotifyClientService.Abstraction
{
    public interface ISpotifyClientService
    {
        bool IsAuthenticated();
        Uri GetAuthorizationUri();
        Task SetupAccessToken(string authorizationCode);
        Task<string> RequestForUserId();
        Task<IReadOnlyList<TrackItem>> GetPlaylistTracks(string playlistId);
        Task PopulatePlaylist(string id, IReadOnlyCollection<string> spotifyTrackIds);
        Task<TrackItem> GetTrackInfo(string author, string title);
        Task SetPlaylistDescription(string playlistId, string description);
        Task SetPlaylistCoverImage(string playlistId, string filePath);
        Task<string> CreatePlaylist(string userId, string name, bool @public = false, string description = null);
        Task<string> CreateCurrentUserPlaylist(string name, bool @public = false, string description = null);
        Task<IEnumerable<PlaylistDto>> RequestForUserPlaylists();
        Task ClearPlaylist(string playlistId);
        Task MakePlaylistPrivate(string playlistId);
        Task MakePlaylistPublic(string playlistId);
        Task SetPlaylistsVisibility(IEnumerable<string> playlistIds, bool isVisible);
    }
}