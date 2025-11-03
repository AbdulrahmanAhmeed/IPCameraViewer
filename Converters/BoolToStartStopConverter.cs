using Microsoft.Maui.Controls;

namespace IPCameraViewer.Converters
{
	public class BoolToStartStopConverter : IValueConverter
	{
		private const string StartText = "Start";
		private const string StopText = "Stop";

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return (value is bool isRunning && isRunning) ? BoolToStartStopConverter.StopText : BoolToStartStopConverter.StartText;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}


