using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace youtVideoDownloader.Models
{
    public class VideoInfoModel
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string VideoUrl { get; set; } = string.Empty;
        public List<string> AvailableQualities { get; set; } = new();
    }
}
