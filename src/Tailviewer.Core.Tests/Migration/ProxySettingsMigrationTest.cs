#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class ProxySettingsMigrationTest
    {
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(20)]
        public void UnavailablePasswordPreservesCiphertextAndExactSalt(int saltLength)
        {
            var salt = Convert.ToBase64String(new byte[saltLength]);
            var ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
            var settings = Restore(new XElement("autoupdate", new XAttribute("passwordsalt", salt), new XAttribute("proxypassword", ciphertext), new XAttribute("proxyusername", "synthetic-user")));
            Assert.That(settings.ProxyPassword, Is.Null);
            Assert.That(settings.ProxyPasswordRequiresReentry, Is.True);
            var clone = settings.Clone();
            var saved = Save(clone);
            Assert.Multiple(() =>
            {
                Assert.That((string)saved.Attribute("passwordsalt"), Is.EqualTo(salt));
                Assert.That((string)saved.Attribute("proxypassword"), Is.EqualTo(ciphertext));
                Assert.That(settings.GetProxyCredentials(), Is.Null, "Do not silently authenticate with an empty password when decryption failed");
            });
        }

        [Test]
        public void CurrentUserPasswordSurvivesRestoreAndCloneWithoutPlaintextXml()
        {
            Assert.That(OperatingSystem.IsWindows(), Is.True, "DPAPI acceptance targets Windows x64");
            const string password = "synthetic-proxy-secret-42";
            var settings = new AutoUpdateSettings { ProxyUsername = "synthetic-user", ProxyPassword = password };
            var saved = Save(settings);
            Assert.That(saved.ToString(), Does.Not.Contain(password));
            Assert.That(Convert.FromBase64String((string)saved.Attribute("passwordsalt")).Length, Is.EqualTo(20));
            var restored = Restore(saved);
            var clone = restored.Clone();
            Assert.That(clone.ProxyPassword == password, Is.True);
            Assert.That(clone.ProxyPasswordRequiresReentry, Is.False);
            clone.ProxyPassword = "replacement";
            Assert.That(restored.ProxyPassword == password, Is.True);
            Assert.That((string)Save(restored).Attribute("proxypassword"), Is.EqualTo((string)saved.Attribute("proxypassword")));
        }

        [Test]
        public void ExplicitPasswordReentryRepairsCredentialsWithoutReplacingExistingSalt()
        {
            Assert.That(OperatingSystem.IsWindows(), Is.True);
            const string salt = "AQID";
            var settings = Restore(XElement.Parse("<autoupdate passwordsalt='" + salt + "' proxypassword='AQIDBA==' proxyusername='synthetic-user'/>"));
            Assert.That(settings.ProxyPasswordRequiresReentry, Is.True);
            settings.ProxyPassword = "synthetic-replacement";
            Assert.That(settings.ProxyPasswordRequiresReentry, Is.False);
            Assert.That(settings.ProxyPassword == "synthetic-replacement", Is.True);
            Assert.That(settings.GetProxyCredentials(), Is.Not.Null);
            Assert.That((string)Save(settings).Attribute("passwordsalt"), Is.EqualTo(salt));
        }

        private static AutoUpdateSettings Restore(XElement element)
        {
            using var reader = element.CreateReader();
            reader.MoveToContent();
            var settings = new AutoUpdateSettings();
            settings.Restore(reader);
            return settings;
        }

        private static XElement Save(AutoUpdateSettings settings)
        {
            using var text = new StringWriter();
            using (var writer = XmlWriter.Create(text))
            {
                writer.WriteStartElement("autoupdate");
                settings.Save(writer);
                writer.WriteEndElement();
            }
            return XElement.Parse(text.ToString());
        }
    }
}
#endif
