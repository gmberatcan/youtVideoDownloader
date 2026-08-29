namespace youtVideoDownloader.Services
{
    public class DummyNotificationService : INotificationService
    {
        public void ShowProgressNotification(string title, int progress, int max) { }
        public void CompleteProgressNotification(string title, string message) { }
        public void CancelProgressNotification() { }
    }
}
