#if NET10_0_OR_GREATER
using System;
using System.Text;

namespace Tailviewer.Settings
{
    internal static class SettingsEncoding
    {
        internal static Encoding Resolve(string name)
        {
            try { return Encoding.GetEncoding(name); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException)
            {
                var encoding = CodePagesEncodingProvider.Instance.GetEncoding(name);
                if (encoding != null) return encoding;
                throw;
            }
        }
    }
}
#endif
