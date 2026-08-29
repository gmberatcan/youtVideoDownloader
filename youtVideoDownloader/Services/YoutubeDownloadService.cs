using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using youtVideoDownloader.Models;
#if ANDROID
using Ffmpegkit.Droid;
#endif

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
                var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl, cancellationToken);
                
                var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                double audioSizeMb = audioStream?.Size.MegaBytes ?? 0;

                var availableQualities = manifest.GetVideoOnlyStreams()
                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                    .GroupBy(s => s.VideoQuality.Label)
                    .Select(g => g.First())
                    .OrderByDescending(s => int.Parse(new string(s.VideoQuality.Label.Where(char.IsDigit).ToArray())))
                    .Select(s => $"{s.VideoQuality.Label} (~{(s.Size.MegaBytes + audioSizeMb):0.0} MB)")
                    .ToList();

                if (!availableQualities.Any())
                {
                    availableQualities = new List<string> { "1080p", "720p", "360p" };
                }

                return new VideoInfoModel
                {
                    Title = video.Title,
                    Author = video.Author.ChannelTitle,
                    Duration = video.Duration ?? TimeSpan.Zero,
                    ThumbnailUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Area).FirstOrDefault()?.Url,
                    VideoUrl = videoUrl,
                    AvailableQualities = availableQualities
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

                // Download to app's cache directory first to avoid Scoped Storage restrictions with .tmp files
                string tempFolder = FileSystem.Current.CacheDirectory;
                string tempOutputPath = string.Empty;
                string finalOutputPath = string.Empty;

                string ffmpegPath = string.Empty;

                // Configure platform-specific final download folders
#if ANDROID
                downloadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
                var nativeLibDir = Android.App.Application.Context.ApplicationInfo.NativeLibraryDir;
                ffmpegPath = Path.Combine(nativeLibDir, "libffmpeg.so");
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

                if (format.ToLower().Contains("audio") || format.ToLower().Contains("mp3"))
                {
                    // Audio Only
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    if (audioStreamInfo == null)
                        throw new Exception("No suitable audio stream found.");

                    string ext = audioStreamInfo.Container.Name;
                    finalOutputPath = Path.Combine(downloadFolder, $"{safeTitle}.mp3");
                    EnsureSecurePath(downloadFolder, finalOutputPath);

#if ANDROID
                    // Native Android implementation using FFmpegKit
                    string tempAudioPath = Path.Combine(tempFolder, $"{safeTitle}.{ext}");
                    if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

                    // Download raw audio
                    await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioPath, progress, cancellationToken);
                    
                    if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                    
                    // Convert to mp3 using FFmpegKit
                    tempOutputPath = Path.Combine(tempFolder, $"{safeTitle}_converted.mp3");
                    if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);

                    string command = $"-y -i \"{tempAudioPath}\" -vn -ar 44100 -ac 2 -b:a 192k \"{tempOutputPath}\"";
                    var session = await FFmpegKit.ExecuteAsync(command);
                    var returnCode = session.ReturnCode;

                    if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

                    if (!returnCode.IsValueSuccess)
                    {
                        var logs = session.FailStackTrace ?? session.Output;
                        throw new Exception($"FFmpegKit failed to convert audio. {logs}");
                    }
#else
                    tempOutputPath = Path.Combine(tempFolder, $"{safeTitle}.mp3");
                    var builder = new ConversionRequestBuilder(tempOutputPath).SetPreset(ConversionPreset.UltraFast);
                    if (!string.IsNullOrEmpty(ffmpegPath))
                        builder.SetFFmpegPath(ffmpegPath);

                    await _youtube.Videos.DownloadAsync(new IStreamInfo[] { audioStreamInfo }, builder.Build(), progress, cancellationToken);
#endif
                }
                else
                {
                    // Video (Muxing Audio + Video)
                    var videoStreamInfos = streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4);
                    
                    // Extract just the quality label, e.g. "1080p" from "1080p (~50.5 MB)"
                    string targetLabel = quality.Split(' ')[0];

                    var videoStreamInfo = videoStreamInfos.FirstOrDefault(s => s.VideoQuality.Label == targetLabel) 
                                          ?? videoStreamInfos.GetWithHighestVideoQuality();

                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (videoStreamInfo == null || audioStreamInfo == null)
                        throw new Exception("No suitable video/audio streams found.");

                    finalOutputPath = Path.Combine(downloadFolder, $"{safeTitle}.mp4");
                    EnsureSecurePath(downloadFolder, finalOutputPath);

#if ANDROID
                    // Native Android implementation using FFmpegKit
                    string tempVideoPath = Path.Combine(tempFolder, $"{safeTitle}_video.mp4");
                    string tempAudioPath = Path.Combine(tempFolder, $"{safeTitle}_audio.{audioStreamInfo.Container.Name}");
                    
                    if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
                    if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

                    // Divide progress between audio and video artificially (e.g., 50% each)
                    var videoProgress = new Progress<double>(p => progress?.Report(p * 0.7));
                    var audioProgress = new Progress<double>(p => progress?.Report(0.7 + (p * 0.2)));

                    await _youtube.Videos.Streams.DownloadAsync(videoStreamInfo, tempVideoPath, videoProgress, cancellationToken);
                    await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempAudioPath, audioProgress, cancellationToken);

                    progress?.Report(0.95); // Muxing phase

                    if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);

                    // Mux using FFmpegKit
                    tempOutputPath = Path.Combine(tempFolder, $"{safeTitle}_muxed.mp4");
                    if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);

                    string command = $"-y -i \"{tempVideoPath}\" -i \"{tempAudioPath}\" -c:v copy -c:a aac \"{tempOutputPath}\"";
                    var session = await FFmpegKit.ExecuteAsync(command);
                    var returnCode = session.ReturnCode;

                    if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
                    if (File.Exists(tempAudioPath)) File.Delete(tempAudioPath);

                    if (!returnCode.IsValueSuccess)
                    {
                        var logs = session.FailStackTrace ?? session.Output;
                        throw new Exception($"FFmpegKit failed to mux video. {logs}");
                    }
                    
                    progress?.Report(1.0);
#else
                    tempOutputPath = Path.Combine(tempFolder, $"{safeTitle}.mp4");
                    var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
                    
                    var builder = new ConversionRequestBuilder(tempOutputPath).SetPreset(ConversionPreset.UltraFast);
                    if (!string.IsNullOrEmpty(ffmpegPath))
                        builder.SetFFmpegPath(ffmpegPath);

                    await _youtube.Videos.DownloadAsync(streamInfos, builder.Build(), progress, cancellationToken);
#endif
                }

                // Copy to final destination (Handle Android Scoped Storage)
#if ANDROID
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    var context = Android.App.Application.Context;
                    var resolver = context.ContentResolver;
                    var contentValues = new Android.Content.ContentValues();
                    string fileName = Path.GetFileName(finalOutputPath);
                    string mimeType = fileName.EndsWith(".mp3") ? "audio/mpeg" : "video/mp4";
                    
                    contentValues.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, fileName);
                    contentValues.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, mimeType);
                    contentValues.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);
                    
                    var uri = resolver?.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri, contentValues);
                    if (uri != null && resolver != null)
                    {
                        using (var destStream = resolver.OpenOutputStream(uri))
                        using (var sourceStream = File.OpenRead(tempOutputPath))
                        {
                            if (destStream != null)
                            {
                                sourceStream.CopyTo(destStream);
                            }
                        }
                        finalOutputPath = "Downloads/" + fileName;
                    }
                    else
                    {
                        if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                        File.Move(tempOutputPath, finalOutputPath);
                    }
                }
                else
                {
                    if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                    File.Move(tempOutputPath, finalOutputPath);
                }
#else
                if (File.Exists(finalOutputPath))
                {
                    File.Delete(finalOutputPath);
                }
                File.Move(tempOutputPath, finalOutputPath);
#endif
                
                if (File.Exists(tempOutputPath))
                {
                    File.Delete(tempOutputPath);
                }

                return finalOutputPath;
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
