using System.Windows;

namespace KeyBard.Controls
{
    public partial class PlaybackControl
    {
        public event EventHandler? PlayClicked;
        public event EventHandler? PauseClicked;
        public event EventHandler? StopClicked;
        public event EventHandler? RestartClicked;
        public event EventHandler? SetLoopStartClicked;
        public event EventHandler? SetLoopEndClicked;
        public event EventHandler? ClearLoopClicked;

        public PlaybackControl()
        {
            InitializeComponent();
        }

        public void SetPlaybackState(bool isPlaying, bool isPaused, bool hasFile)
        {
            if (isPlaying)
            {
                BtnPlay.IsEnabled = false;
                BtnPlay.Content = "▶  Play";
                NameBtnPause.IsEnabled = true;
                BtnStop.IsEnabled = true;
                BtnRestart.IsEnabled = true;
            }
            else if (isPaused)
            {
                BtnPlay.IsEnabled = true;
                BtnPlay.Content = "▶  Resume";
                NameBtnPause.IsEnabled = false;
                BtnStop.IsEnabled = true;
                BtnRestart.IsEnabled = true;
            }
            else // stopped
            {
                BtnPlay.IsEnabled = hasFile;
                BtnPlay.Content = "▶  Play";
                NameBtnPause.IsEnabled = false;
                BtnStop.IsEnabled = false;
                BtnRestart.IsEnabled = false;
            }
        }

        public void UpdateLoopStart(string text) => BtnSetLoopStart.Content = text;
        public void UpdateLoopEnd(string text) => BtnSetLoopEnd.Content = text;

        public void ResetLoopButtons()
        {
            BtnSetLoopStart.Content = "Set A";
            BtnSetLoopEnd.Content = "Set B";
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e) => PlayClicked?.Invoke(this, EventArgs.Empty);
        private void BtnPause_Click(object sender, RoutedEventArgs e) => PauseClicked?.Invoke(this, EventArgs.Empty);
        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopClicked?.Invoke(this, EventArgs.Empty);
        private void BtnRestart_Click(object sender, RoutedEventArgs e) => RestartClicked?.Invoke(this, EventArgs.Empty);
        private void BtnSetLoopStart_Click(object sender, RoutedEventArgs e) => SetLoopStartClicked?.Invoke(this, EventArgs.Empty);
        private void BtnSetLoopEnd_Click(object sender, RoutedEventArgs e) => SetLoopEndClicked?.Invoke(this, EventArgs.Empty);
        private void BtnClearLoop_Click(object sender, RoutedEventArgs e) => ClearLoopClicked?.Invoke(this, EventArgs.Empty);
    }
}
