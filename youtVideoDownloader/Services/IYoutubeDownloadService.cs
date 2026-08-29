using System;
using System.Threading.Tasks;
using youtVideoDownloader.Models;

namespace youtVideoDownloader.Services
{
    public interface IYoutubeDownloadService
    {
        /// <summary>
        /// Fetches video information (title, author, duration, thumbnail) from a YouTube URL.
        /// </summary>
        Task<VideoInfoModel> GetVideoInfoAsync(string videoUrl);

        /// <summary>
        /// Downloads the video or audio based on the selected quality/format.
        /// Reports progress through an IProgress<double> and returns the path to the downloaded file.
        /// </summary>
        Task<string> DownloadMediaAsync(string videoUrl, string format, string quality, IProgress<double> progress);
    }
}
