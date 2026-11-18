using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using log4net;
using Tailviewer.Archiver.Plugins.Description;

namespace Tailviewer.Ui.Plugins
{
	public sealed class InstalledPluginViewModel
		: IPluginViewModel
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(InstalledPluginViewModel));
		private readonly IPluginDescription _description;
		private readonly ImageSource _icon;
		//private readonly DelegateCommand _deleteCommand;

		public InstalledPluginViewModel(IPluginDescription description)
		{
			_description = description;
			_icon = DecodeIcon(description.Icon);

			// TODO: Deleting requires a bit more care because we store plugins
			//       in Program Files which is (for good reason) protected.
			//       Uninstalling a plugin should probably force the UAC dialog
			//       and thus be performed in a separate process. However in order
			//       for this to succeed, we must make sure that we don't hold anymore
			//       handles to the plugin...
			//_deleteCommand = new DelegateCommand();
		}

		public string Name => string.IsNullOrEmpty(_description.Name) ? _description.Id.ToString() : _description.Name;
		public Version Version => _description.Version;
		public string Author => _description.Author ?? "N/A";
		public string Description => _description.Description;
		public string Error => _description.Error;
		public Uri Website => _description.Website;
		public ImageSource Icon => _icon;
		public ICommand DeleteCommand => null;
		public ICommand DownloadCommand => null;

		public bool HasError => !string.IsNullOrEmpty(_description.Error);

		private static ImageSource DecodeIcon(byte[] bytes)
		{
			if (bytes == null)
				return null;

			try
			{
				using (var stream = new MemoryStream(bytes))
				{
					var image = new BitmapImage();
					image.BeginInit();
					image.CacheOption = BitmapCacheOption.OnLoad;
					image.StreamSource = stream;
					image.EndInit();
					image.Freeze();
					return image;
				}
			}
			catch (Exception e)
			{
				Log.Warn("Unable to decode plugin icon", e);
				return null;
			}
		}
	}
}
