using System.Windows;

namespace KeyBard.Controls
{
    public partial class SettingsControl
    {
        public event EventHandler<double>? SpeedChanged;
        public event EventHandler<double>? VolumeChanged;
        public event EventHandler<bool>? MuteChanged;

        private double _savedVolume = 100;

        public SettingsControl()
        {
            InitializeComponent();
            // Start muted by default without raising events
            IsMuted = true;
            _savedVolume = 100; // Default restore volume
            if (BtnMute != null) BtnMute.Content = "🔇";
            if (VolumeSlider != null)
            {
                VolumeSlider.Value = 0;
                VolumeSlider.IsEnabled = true; // Slider is now always enabled
            }
        }

        public double Speed => SpeedSlider.Value;
        public double Volume => VolumeSlider.Value;
        public bool IsMuted { get; private set; } = true;

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSpeed != null) TxtSpeed.Text = $"{e.NewValue:F2}x";
            SpeedChanged?.Invoke(this, e.NewValue);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var value = (int)e.NewValue;
            if (TxtVolume != null) TxtVolume.Text = $"{value}%";

            // If moving the slider while muted to a value > 0, unmute
            if (IsMuted && value > 0)
            {
                IsMuted = false;
                UpdateMuteIcon();
                MuteChanged?.Invoke(this, IsMuted);
            }
            // If slider is moved to 0, automatically mute?
            // The requirement says "When muting, volume slider is set to 0",
            // but doesn't explicitly say "When setting slider to 0, it should mute".
            // However, usually these are correlated. Let's see if we should auto-mute.
            // If we don't, the icon will just stay as 🔇 because of UpdateMuteIcon logic.
            else if (!IsMuted && value == 0)
            {
                // We don't necessarily toggle the IsMuted state to true here unless asked,
                // but we update the icon.
                UpdateMuteIcon();
            }
            else
            {
                UpdateMuteIcon();
            }

            VolumeChanged?.Invoke(this, e.NewValue);
        }

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            IsMuted = !IsMuted;
            if (IsMuted)
            {
                _savedVolume = VolumeSlider.Value > 0 ? VolumeSlider.Value : (_savedVolume > 0 ? _savedVolume : 100);
                VolumeSlider.Value = 0;
                BtnMute.Content = "🔇";
            }
            else
            {
                VolumeSlider.Value = _savedVolume > 0 ? _savedVolume : 100;
                UpdateMuteIcon();
            }

            MuteChanged?.Invoke(this, IsMuted);
        }

        private void UpdateMuteIcon()
        {
            if (IsMuted || BtnMute == null) return;

            int value = (int)VolumeSlider.Value;
            BtnMute.Content = value switch
            {
                0 => "🔇",
                <= 33 => "🔈",
                <= 66 => "🔉",
                _ => "🔊"
            };
        }
    }
}