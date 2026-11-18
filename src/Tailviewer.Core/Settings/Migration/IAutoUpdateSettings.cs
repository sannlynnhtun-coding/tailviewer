using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;

namespace Tailviewer.Settings
{
	/// <summary>Proxy credentials, repository endpoints and persisted update preferences.</summary>
	public interface IAutoUpdateSettings
	{
		/// <summary>The last update-check timestamp.</summary>
		DateTime LastChecked { get; set; }
		/// <summary>Whether automatic checks are enabled.</summary>
		bool CheckForUpdates { get; set; }
		/// <summary>The persisted legacy preference; modern installation still requires consent.</summary>
		bool AutomaticallyInstallUpdates { get; set; }
		/// <summary>The configured proxy URL.</summary>
		string ProxyServer { get; set; }
		/// <summary>The configured proxy username.</summary>
		string ProxyUsername { get; set; }
		/// <summary>The password protected for the current Windows user.</summary>
		string ProxyPassword { get; set; }
		/// <summary>The configured repository endpoints.</summary>
		IReadOnlyList<string> PluginRepositories { get; set; }

		/// <summary>Whether stored encrypted credentials are unavailable and require password re-entry.</summary>
		bool ProxyPasswordRequiresReentry { get; }

		/// <summary>Writes the legacy update and encrypted credential attributes.</summary>
		void Save(XmlWriter writer);
		/// <summary>Restores legacy update and encrypted credential attributes.</summary>
		void Restore(XmlReader reader);
		/// <summary>Returns configured proxy credentials when available.</summary>
		ICredentials GetProxyCredentials();
		/// <summary>Returns the explicit or system proxy.</summary>
		IWebProxy GetWebProxy();
	}
}
