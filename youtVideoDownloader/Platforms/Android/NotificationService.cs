using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using youtVideoDownloader.Services;

namespace youtVideoDownloader.Platforms.Android
{
    public class NotificationService : INotificationService
    {
        private NotificationManager _notificationManager;
        private NotificationCompat.Builder _builder;
        private const int NotificationId = 1001;
        private const int CompleteNotificationId = 1002;
        private const string ChannelId = "download_channel";

        public NotificationService()
        {
            _notificationManager = (NotificationManager)global::Android.App.Application.Context.GetSystemService(Context.NotificationService);
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Downloads", NotificationImportance.Low);
                _notificationManager.CreateNotificationChannel(channel);
            }
        }

        private DateTime _lastUpdateTime = DateTime.MinValue;
        private int _lastProgress = -1;
        private string _lastTitle = "";
        private bool _isServiceRunning = false;

        public void ShowProgressNotification(string title, int progress, int max)
        {
            if (progress == _lastProgress && title == _lastTitle) return;
            if (progress < max && title == _lastTitle && (DateTime.Now - _lastUpdateTime).TotalMilliseconds < 500) return;

            _lastUpdateTime = DateTime.Now;
            _lastProgress = progress;
            _lastTitle = title;

            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(DownloadForegroundService));
            intent.SetAction(_isServiceRunning ? DownloadForegroundService.ACTION_UPDATE_PROGRESS : DownloadForegroundService.ACTION_START_SERVICE);
            intent.PutExtra("title", title);
            intent.PutExtra("progress", progress);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
            _isServiceRunning = true;
        }

        public void CompleteProgressNotification(string title, string message)
        {
            StopForegroundService();

            _builder = new NotificationCompat.Builder(global::Android.App.Application.Context, ChannelId)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownloadDone)
                .SetOngoing(false)
                .SetProgress(0, 0, false);

            _notificationManager.Notify(CompleteNotificationId, _builder.Build());
        }
        
        public void CancelProgressNotification()
        {
            StopForegroundService();
            _notificationManager.Cancel(NotificationId);
            _notificationManager.Cancel(CompleteNotificationId);
        }

        private void StopForegroundService()
        {
            if (_isServiceRunning)
            {
                var context = global::Android.App.Application.Context;
                var intent = new Intent(context, typeof(DownloadForegroundService));
                intent.SetAction(DownloadForegroundService.ACTION_STOP_SERVICE);
                context.StartService(intent);
                _isServiceRunning = false;
            }
        }
    }
}
