namespace youtVideoDownloader.Services
{
    public interface INotificationService
    {
        void ShowProgressNotification(string title, int progress, int max);
        void CompleteProgressNotification(string title, string message);
        void CancelProgressNotification();
    }
}
