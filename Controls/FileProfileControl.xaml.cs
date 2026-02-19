using System.Windows;
using System.Windows.Controls;

namespace KeyBard.Controls
{
    public partial class FileProfileControl

    {
    public event EventHandler? BrowseClicked;
    public event EventHandler<string>? ProfileChanged;
    public event EventHandler? LoadBindingsClicked;
    public event EventHandler? SaveBindingsClicked;
    public event EventHandler? SaveProfileAsClicked;
    public event EventHandler? ClearBindingsClicked;
    public event EventHandler? SaveForSongClicked;

    public FileProfileControl()
    {
        InitializeComponent();
    }

    public void SetFileName(string name, bool hasFile)
    {
        TxtFileName.Text = name;
        TxtFileName.Foreground = new System.Windows.Media.SolidColorBrush(hasFile
            ? System.Windows.Media.Color.FromRgb(220, 220, 220)
            : System.Windows.Media.Color.FromRgb(153, 153, 153));
        BtnSaveForSong.IsEnabled = hasFile;
    }

    public void SetProfiles(IEnumerable<string?> profiles, string selectedProfile)
    {
        ProfileComboBox.ItemsSource = profiles;
        ProfileComboBox.SelectedItem = selectedProfile;
    }

    public string? SelectedProfile => ProfileComboBox.SelectedItem as string;

    public void SetBrowseEnabled(bool enabled)
    {
        BtnBrowse.IsEnabled = enabled;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e) => BrowseClicked?.Invoke(this, EventArgs.Empty);

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is string profileName) ProfileChanged?.Invoke(this, profileName);
    }

    private void BtnLoadBindings_Click(object sender, RoutedEventArgs e) =>
        LoadBindingsClicked?.Invoke(this, EventArgs.Empty);

    private void BtnSaveBindings_Click(object sender, RoutedEventArgs e) =>
        SaveBindingsClicked?.Invoke(this, EventArgs.Empty);

    private void BtnSaveProfileAs_Click(object sender, RoutedEventArgs e) =>
        SaveProfileAsClicked?.Invoke(this, EventArgs.Empty);

    private void BtnClearBindings_Click(object sender, RoutedEventArgs e) =>
        ClearBindingsClicked?.Invoke(this, EventArgs.Empty);

    private void BtnSaveForSong_Click(object sender, RoutedEventArgs e) =>
        SaveForSongClicked?.Invoke(this, EventArgs.Empty);
    }
}