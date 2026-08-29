using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using youtVideoDownloader.Models;

namespace youtVideoDownloader.Services
{
    public class YoutubeDownloadService : IYoutubeDownloadService
    {
        private readonly YoutubeClient _youtube;

        public YoutubeDownloadService()
        {
            _youtube = new YoutubeClient();
        }

        public async Task<VideoInfoModel> GetVideoInfoAsync(string videoUrl, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsValidYoutubeUrl(videoUrl))
                    throw new ArgumentException("Geçersiz YouTube bağlantısı.");

                var video = await _youtube.Videos.GetAsync(videoUrl, cancellationToken);

                return new VideoInfoModel
                {
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration ?? TimeSpan.Zero,
                    ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                    VideoUrl = videoUrl
                };
            }
            catch (Exception ex)
            {
                // In a real application, consider using ILogger to log this error
                System.Diagnostics.Debug.WriteLine($"Error fetching video info: {ex.Message}");
                throw;
            }
        }

        public async Task<string> DownloadMediaAsync(string videoUrl, string format, string quality, IProgress<double> progress, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsValidYoutubeUrl(videoUrl))
                    throw new ArgumentException("Geçersiz YouTube bağlantısı.");

                var video = await _youtube.Videos.GetAsync(videoUrl, cancellationToken);
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl, cancellationToken);

                string safeTitle = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
                
                string downloadFolder = string.Empty;

                // Configure platform-specific download folders
#if ANDROID
                // On Android, use standard Downloads directory or Movies/Music depending on type
                downloadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#elif IOS
                downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#elif WINDOWS
                downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                downloadFolder = Path.Combine(downloadFolder, "Downloads");
#else
                downloadFolder = FileSystem.Current.AppDataDirectory;
#endif

                if (!Directory.Exists(downloadFolder))
                {
                    Directory.CreateDirectory(downloadFolder);
                }

                string outputPath = string.Empty;

                if (format.ToLower().Contains("audio") || format.ToLower().Contains("mp3"))
                {
                    // Audio Only
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    if (audioStreamInfo == null)
                        throw new Exception("No suitable audio stream found.");

                    outputPath = Path.Combine(downloadFolder, $"{safeTitle}.mp3");
                    EnsureSecurePath(downloadFolder, outputPath);

                    await _youtube.Videos.DownloadAsync(videoUrl, outputPath, builder => builder.SetPreset(ConversionPreset.UltraFast), progress, cancellationToken);
                }
                else
                {
                    // Video (Muxing Audio + Video)
                    var videoStreamInfos = streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4);
                    
                    IStreamInfo videoStreamInfo = null;
                    if (quality == "1080p")
                    {
                        videoStreamInfo = videoStreamInfos.FirstOrDefault(s => s.VideoQuality.Label == "1080p") ?? videoStreamInfos.GetWithHighestVideoQuality();
                    }
                    else if (quality == "720p")
                    {
                        videoStreamInfo = videoStreamInfos.FirstOrDefault(s => s.VideoQuality.Label == "720p") ?? videoStreamInfos.GetWithHighestVideoQuality();
                    }
                    else if (quality == "4K")
                    {
                        videoStreamInfo = videoStreamInfos.FirstOrDefault(s => s.VideoQuality.Label == "2160p") ?? videoStreamInfos.GetWithHighestVideoQuality();
                    }
                    else
                    {
                        videoStreamInfo = videoStreamInfos.GetWithHighestVideoQuality();
                    }

                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (videoStreamInfo == null || audioStreamInfo == null)
                        throw new Exception("No suitable video/audio streams found.");

                    var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
                    
                    outputPath = Path.Combine(downloadFolder, $"{safeTitle}.mp4");
                    EnsureSecurePath(downloadFolder, outputPath);

                    await _youtube.Videos.DownloadAsync(streamInfos, new ConversionRequestBuilder(outputPath).SetPreset(ConversionPreset.UltraFast).Build(), progress, cancellationToken);
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading media: {ex.Message}");
                throw;
            }
        }

        private bool IsValidYoutubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) && 
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                var host = uriResult.Host.ToLower();
                return host.Contains("youtube.com") || host.Contains("youtu.be");
            }
            return false;
        }

        private void EnsureSecurePath(string baseFolder, string fullPath)
        {
            var baseDir = new DirectoryInfo(baseFolder).FullName;
            var targetDir = new FileInfo(fullPath).Directory.FullName;

            if (!targetDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Güvenlik ihlali: Geçersiz dosya dizini yolu (Path Traversal attempt).");
            }
        }
    }
}
