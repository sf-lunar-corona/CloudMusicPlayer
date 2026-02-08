using System.Collections.ObjectModel;
using CloudMusicPlayer.Models;
using CloudMusicPlayer.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CloudMusicPlayer.ViewModels;

public partial class SearchViewModel : BaseViewModel
{
    private readonly ISearchService _searchService;
    private readonly IAudioPlaybackService _playbackService;

    public ObservableCollection<AudioTrack> Results { get; } = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _resultCount;

    [ObservableProperty]
    private bool _hasSearched;

    public SearchViewModel(ISearchService searchService, IAudioPlaybackService playbackService)
    {
        _searchService = searchService;
        _playbackService = playbackService;
        Title = "Search";
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        await ExecuteAsync(async () =>
        {
            HasSearched = true;
            var results = await _searchService.SearchAsync(SearchQuery);

            Results.Clear();
            foreach (var track in results)
                Results.Add(track);

            ResultCount = Results.Count;
            IsEmpty = Results.Count == 0;
            EmptyMessage = $"No results for \"{SearchQuery}\"";
        }, "Search failed");
    }

    [RelayCommand]
    private async Task PlayTrackAsync(AudioTrack track)
    {
        var allResults = Results.ToList();
        var index = allResults.IndexOf(track);
        await _playbackService.PlayAsync(allResults, index);
        await Shell.Current.GoToAsync("nowplaying");
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Results.Clear();
            ResultCount = 0;
            HasSearched = false;
            IsEmpty = false;
        }
    }
}
