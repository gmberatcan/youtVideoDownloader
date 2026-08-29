namespace youtVideoDownloader
{
    public partial class MainPage : ContentPage
    {
        public MainPage(ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        public MainPage()
        {
            InitializeComponent();
            BindingContext = Application.Current.MainPage?.Handler?.MauiContext?.Services?.GetService<ViewModels.MainViewModel>();
        }
    }
}
