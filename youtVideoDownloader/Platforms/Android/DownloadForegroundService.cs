using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace youtVideoDownloader.Platforms.Android
{
    [Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class DownloadForegroundService : Service
    {
        public const string ACTION_START_SERVICE = "ACTION_START_SERVICE";
        public const string ACTION_STOP_SERVICE = "ACTION_STOP_SERVICE";
        public const string ACTION_UPDATE_PROGRESS = "ACTION_UPDATE_PROGRESS";

        private const int NotificationId = 1001;
        private const string ChannelId = "download_channel";

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == ACTION_START_SERVICE || intent?.Action == ACTION_UPDATE_PROGRESS)
            {
                string title = intent.GetStringExtra("title") ?? "İndiriliyor...";
                int progress = intent.GetIntExtra("progress", 0);
                
                var notification = CreateNotification(title, progress);
                
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeDataSync);
                }
                else
                {
                    StartForeground(NotificationId, notification);
                }
            }
            else if (intent?.Action == ACTION_STOP_SERVICE)
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
            }

            return StartCommandResult.Sticky;
        }

        private Notification CreateNotification(string title, int progress)
        {
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Downloads", NotificationImportance.Low);
                notificationManager.CreateNotificationChannel(channel);
            }

            var builder = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle(title)
                .SetContentText($"%{progress}")
                .SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetOngoing(true)
                .SetOnlyAlertOnce(true)
                .SetProgress(100, progress, false);

            return builder.Build();
        }
    }
}
