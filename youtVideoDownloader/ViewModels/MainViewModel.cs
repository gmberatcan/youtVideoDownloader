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

        private readonly INotificationService _notificationService;

        public MainViewModel(IYoutubeDownloadService youtubeService, INotificationService notificationService)
        {
            _youtubeService = youtubeService;
            _notificationService = notificationService;
        }

        partial void OnSelectedFormatChanged(string value)
        {
            UpdateQualities();
        }

        private void UpdateQualities()
        {
            Qualities.Clear();
            if (SelectedFormat.ToLower().Contains("audio") || SelectedFormat.ToLower().Contains("mp3"))
            {
                Qualities.Add("Highest Bitrate");
                SelectedQuality = "Highest Bitrate";
            }
            else
            {
                if (VideoInfo != null && VideoInfo.AvailableQualities.Any())
                {
                    foreach (var q in VideoInfo.AvailableQualities)
                    {
                        Qualities.Add(q);
                    }
                    SelectedQuality = Qualities.First();
                }
                else
                {
                    Qualities.Add("1080p");
                    SelectedQuality = "1080p";
                }
            }
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
                _notificationService.CancelProgressNotification();
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
                VideoInfo = await Task.Run(() => _youtubeService.GetVideoInfoAsync(VideoUrl, _cancellationTokenSource.Token));
                
                UpdateQualities();

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

#if ANDROID
            if (Microsoft.Maui.Devices.DeviceInfo.Version.Major < 13)
            {
                var status = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.StorageWrite>();
                if (status != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.StorageWrite>();
                }
                if (status != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    StatusText = "Storage permission is required to save videos.";
                    return;
                }
            }
            else
            {
                var notifyStatus = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.PostNotifications>();
                if (notifyStatus != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                {
                    await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.PostNotifications>();
                }
            }
#endif

            IsDownloading = true;
            IsBusy = true;
            DownloadProgress = 0;
            StatusText = "Downloading...";

            _cancellationTokenSource = new System.Threading.CancellationTokenSource();

            string title = VideoInfo?.Title ?? "Download";

            try
            {
                var progress = new Progress<double>(p =>
                {
                    // p is between 0.0 and 1.0 from YoutubeExplode
                    DownloadProgress = p;
                    StatusText = $"Downloading... {(p * 100):0.0}%";
                    
                    int currentProgress = (int)(p * 100);
                    _notificationService.ShowProgressNotification(title, currentProgress, 100);
                });

                string path = await Task.Run(() => _youtubeService.DownloadMediaAsync(VideoUrl, SelectedFormat, SelectedQuality, progress, _cancellationTokenSource.Token));
                
                StatusText = $"Saved to: {path}";
                _notificationService.CompleteProgressNotification("Download Complete", $"Saved: {title}");
            }
            catch (OperationCanceledException)
            {
                StatusText = "Download cancelled.";
                _notificationService.CancelProgressNotification();
            }
            catch (Exception ex)
            {
                StatusText = $"Download Failed: {ex.Message}";
                _notificationService.CompleteProgressNotification("Download Failed", "An error occurred during download.");
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
