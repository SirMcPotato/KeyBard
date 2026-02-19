using System.Collections;

namespace KeyBard.Controls
{
    public partial class ChannelFilterControl
    {
        public ChannelFilterControl()
        {
            InitializeComponent();
        }

        public void SetChannelsSource(IEnumerable source)
        {
            ChannelsItemsControl.ItemsSource = source;
        }
    }
}