using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using youtVideoDownloader.Models;
using youtVideoDownloader.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace youtVideoDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IYoutubeDownloadService _youtubeService;
        private System.Threading.CancellationTokenSource? _cancellationTokenSource;

        [ObservableProperty]
        private string _videoUrl = string.Empty;

        [ObservableProperty]
        private VideoInfoModel? _videoInfo;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _hasVideoInfo;

        // Formats: Video (MP4) or Audio (MP3)
        public ObservableCollection<string> Formats { get; } = new() { "Video (MP4)", "Audio (MP3)" };
        
        [ObservableProperty]
        private string _selectedFormat = "Video (MP4)";

        // Qualities
        public ObservableCollection<string> Qualities { get; } = new() { "4K", "1440p", "1080p", "720p", "360p" };
        
        [ObservableProperty]
        private string _selectedQuality = "1080p";

        // Progress properties
        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private bool _isDownloading;

        public MainViewModel(IYoutubeDownloadService youtubeService)
        {
            _youtubeService = youtubeService;
        }

        [RelayCommand]
        private async Task PasteFromClipboardAsync()
        {
            if (Clipboard.Default.HasText)
            {
                string? text = await Clipboard.Default.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    VideoUrl = text;
                }
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                StatusText = "Cancelling...";
            }
        }

        [RelayCommand]
        private async Task FetchVideoInfoAsync()
        {
            if (string.IsNullOrWhiteSpace(VideoUrl))
                return;

            IsBusy = true;
            StatusText = "Fetching video info...";
            HasVideoInfo = false;
            VideoInfo = null;

            _cancellationTokenSource = new System.Threading.CancellationTokenSource();

            try
            {
                VideoInfo = await _youtubeService.GetVideoInfoAsync(VideoUrl, _cancellationTokenSource.Token);
                HasVideoInfo = true;
                StatusText = string.Empty;
            }
            catch (OperationCanceledException)
            {
                StatusText = "Operation cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        [RelayCommand]
        private async Task DownloadAsync()
        {
            if (string.IsNullOrWhiteSpace(VideoUrl) || VideoInfo == null)
                return;

            IsDownloading = true;
            IsBusy = true;
            DownloadProgress = 0;
            StatusText = "Downloading...";

            _cancellationTokenSource = new System.Threading.CancellationTokenSource();

            try
            {
                var progress = new Progress<double>(p =>
                {
                    // p is between 0.0 and 1.0 from YoutubeExplode
                    DownloadProgress = p;
                    StatusText = $"Downloading... {(p * 100):0.0}%";
                });

                string path = await _youtubeService.DownloadMediaAsync(VideoUrl, SelectedFormat, SelectedQuality, progress, _cancellationTokenSource.Token);
                
                StatusText = $"Saved to: {path}";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Download cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = $"Download Failed: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
                IsBusy = false;
                DownloadProgress = 0; // Reset or keep full
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }
}
