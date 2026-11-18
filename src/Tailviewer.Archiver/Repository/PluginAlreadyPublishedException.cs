using System;
using System.Runtime.Serialization;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif

namespace Tailviewer.Archiver.Repository
{
	[Serializable]
	public class PluginAlreadyPublishedException
		: Exception
	{
		public PluginAlreadyPublishedException()
			: base("The given plugin has already been published in that version and cannot be modified.")
		{
		}

		public PluginAlreadyPublishedException(string message)
			: base(message)
		{
		}

		public PluginAlreadyPublishedException(string message, Exception inner)
			: base(message, inner)
		{
		}

#if NETFRAMEWORK
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected PluginAlreadyPublishedException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
		}
#endif
	}
}