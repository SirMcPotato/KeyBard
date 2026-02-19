using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KeyBard.Controls
{
    public partial class ProgressControl
    {
        public event DragStartedEventHandler? DragStarted;
        public event DragCompletedEventHandler? DragCompleted;
        public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

        public ProgressControl()
        {
            InitializeComponent();
        }

        public double Value
        {
            get => ProgressSlider.Value;
            set => ProgressSlider.Value = value;
        }

        public double Maximum
        {
            get => ProgressSlider.Maximum;
            set => ProgressSlider.Maximum = value;
        }

        public void SetTimes(string current, string total)
        {
            TxtCurrentTime.Text = current;
            TxtTotalTime.Text = total;
        }

        public void UpdateLoopMarkers(double loopStartMs, double loopEndMs, double totalDurationMs)
        {
            UpdateMarker(MarkerA, loopStartMs, totalDurationMs);
            UpdateMarker(MarkerB, loopEndMs, totalDurationMs);
        }

        private void UpdateMarker(System.Windows.Shapes.Polygon marker, double timeMs, double totalDurationMs)
        {
            if (timeMs < 0 || totalDurationMs <= 0)
            {
                marker.Visibility = Visibility.Collapsed;
                return;
            }

            marker.Visibility = Visibility.Visible;
            double ratio = timeMs / totalDurationMs;
            double x = ratio * ProgressSlider.ActualWidth;
            
            Canvas.SetLeft(marker, x - 5);
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e) => DragStarted?.Invoke(this, e);
        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e) => DragCompleted?.Invoke(this, e);
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
