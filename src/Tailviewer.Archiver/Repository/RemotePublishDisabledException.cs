using System;
using System.Runtime.Serialization;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif

namespace Tailviewer.Archiver.Repository
{
	/// <summary>
	///     This exception is thrown when a plugin is published to a repository and the repository
	///     has remote publishing disabled.
	/// </summary>
	[Serializable]
	public class RemotePublishDisabledException
		: Exception
	{
		public RemotePublishDisabledException()
			: base("Remote publishing is disabled in this repository")
		{
		}

		public RemotePublishDisabledException(string message)
			: base(message)
		{
		}

		public RemotePublishDisabledException(string message, Exception inner)
			: base(message, inner)
		{
		}

#if NETFRAMEWORK
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected RemotePublishDisabledException(SerializationInfo info, StreamingContext context) : base(info, context)
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