using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using log4net;
#if !NET10_0_OR_GREATER
using Metrolib;
#endif

namespace Tailviewer.Settings
{
	/// <summary>Proxy credentials, repository endpoints and persisted update preferences.</summary>
	public sealed class AutoUpdateSettings
		: IAutoUpdateSettings
		, ICloneable
	{
		private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private const int PasswordSaltLength = 20;

		/// <inheritdoc />
		public DateTime LastChecked { get; set; }
		/// <inheritdoc />
		public bool CheckForUpdates { get; set; }
		/// <inheritdoc />
		public bool AutomaticallyInstallUpdates { get; set; }
		/// <inheritdoc />
		public string ProxyServer { get; set; }
		/// <inheritdoc />
		public string ProxyUsername { get; set; }
		/// <inheritdoc />
		public IReadOnlyList<string> PluginRepositories { get; set; }

		/// <inheritdoc />
		public string ProxyPassword
		{
			get
			{
				try
				{
					if (_password == null || _password.Length == 0)
					{
						if (_passwordIsEmpty)
							return string.Empty;

						return null;
					}

#if NET10_0_OR_GREATER
					if (!OperatingSystem.IsWindows()) return null;
#endif
					var unprotected = ProtectedData.Unprotect(_password, _passwordSalt, DataProtectionScope.CurrentUser);
					try { return Encoding.UTF8.GetString(unprotected); }
					finally { Array.Clear(unprotected, 0, unprotected.Length); }
				}
				catch (Exception)
				{
					Log.Warn("Stored proxy password is unavailable; re-enter the password.");
					return null;
				}
			}
			set
			{
				try
				{
					if (string.IsNullOrEmpty(value))
					{
						_password = null;
						_passwordIsEmpty = value == string.Empty;
						return;
					}
#if NET10_0_OR_GREATER
					if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
#endif
					var salt = _passwordSalt;
					if (salt == null || salt.Length == 0)
					{
						salt = new byte[PasswordSaltLength];
						using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
					}
					var data = Encoding.UTF8.GetBytes(value);
					byte[] encrypted;
					try { encrypted = ProtectedData.Protect(data, salt, DataProtectionScope.CurrentUser); }
					finally { Array.Clear(data, 0, data.Length); }
					_password = encrypted;
					_passwordSalt = salt;
					_passwordIsEmpty = false;
				}
				catch (Exception)
				{
					Log.Warn("Unable to protect proxy password; previous encrypted credentials retained.");
				}
			}
		}

		/// <inheritdoc />
		public bool ProxyPasswordRequiresReentry => _password != null && _password.Length > 0 && ProxyPassword == null;

		private byte[] _password;
		private byte[] _passwordSalt;
		private bool _passwordIsEmpty;

		/// <summary>Creates settings with the original defaults.</summary>
		public AutoUpdateSettings()
		{
			PluginRepositories = new string[0];
		}

		/// <inheritdoc />
		public void Save(XmlWriter writer)
		{
			writer.WriteAttributeBool("checkforupdates", CheckForUpdates);
			writer.WriteAttributeBool("automaticallyinstallupdates", AutomaticallyInstallUpdates);
			writer.WriteAttributeBase64("passwordsalt", _passwordSalt);
			writer.WriteAttributeString("proxyserver", ProxyServer);
			writer.WriteAttributeString("proxyusername", ProxyUsername);
			writer.WriteAttributeBase64("proxypassword", _password);
			writer.WriteAttributeString("pluginrepositories", string.Join(";", PluginRepositories));
		}

		/// <inheritdoc />
		public void Restore(XmlReader reader)
		{
			for (int i = 0; i < reader.AttributeCount; ++i)
			{
				reader.MoveToAttribute(i);
				switch (reader.Name)
				{
					case "checkforupdates":
						CheckForUpdates = reader.ReadContentAsBool();
						break;

					case "automaticallyinstallupdates":
						AutomaticallyInstallUpdates = reader.ReadContentAsBool();
						break;

					case "passwordsalt":
						_passwordSalt = reader.ReadContentAsBase64();
						break;

					case "proxyserver":
						ProxyServer = reader.ReadContentAsString();
						break;

					case "proxyusername":
						ProxyUsername = reader.ReadContentAsString();
						break;

					case "proxypassword":
						_password = reader.ReadContentAsBase64();
						break;

					case "pluginrepositories":
						PluginRepositories = reader.ReadContentAsString().Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries);
						break;

					default:
						Log.WarnFormat("Skipping unknown attribute '{0}'", reader.Name);
						break;
				}
			}

		}

		/// <inheritdoc />
		public ICredentials GetProxyCredentials()
		{
			var username = ProxyUsername;
			var password = ProxyPassword;

			if (password == null && _password != null && _password.Length > 0)
				return null;

			if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
				return null;

			var securePassword = new SecureString();
			if (password != null)
			{
				foreach (var c in password)
				{
					securePassword.AppendChar(c);
				}
			}

			var credentials = new NetworkCredential(username, securePassword);
			return credentials;
		}

		/// <inheritdoc />
		public IWebProxy GetWebProxy()
		{
			var credentials = GetProxyCredentials();
			if (!string.IsNullOrEmpty(ProxyServer))
			{
				return new WebProxy(ProxyServer, true, null, credentials);
			}

			// shortcut: preserve the per-instance legacy system proxy until the HTTP client migration.
#pragma warning disable SYSLIB0014
			var proxy = WebRequest.GetSystemWebProxy();
#pragma warning restore SYSLIB0014
			if (credentials != null)
			{
				proxy.Credentials = credentials;
			}
			return proxy;
		}

		/// <summary>Creates an independent settings copy.</summary>
		[Pure]
		public AutoUpdateSettings Clone()
		{
			return new AutoUpdateSettings
			{
				AutomaticallyInstallUpdates = AutomaticallyInstallUpdates,
				CheckForUpdates = CheckForUpdates,
				LastChecked = LastChecked,
				ProxyServer = ProxyServer,
				ProxyUsername = ProxyUsername,
				_password = _password?.ToArray(),
				_passwordIsEmpty = _passwordIsEmpty,
				_passwordSalt = _passwordSalt?.ToArray(),
				PluginRepositories = PluginRepositories?.ToArray()
			};
		}

		object ICloneable.Clone()
		{
			return Clone();
		}
	}
}
