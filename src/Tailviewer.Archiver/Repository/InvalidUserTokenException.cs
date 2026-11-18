using System;
using System.Runtime.Serialization;
#if NETFRAMEWORK
using System.Security.Permissions;
#endif

namespace Tailviewer.Archiver.Repository
{
	/// <summary>
	///     This exception is thrown when the given user token is invalid.
	/// </summary>
	[Serializable]
	public class InvalidUserTokenException
		: Exception
	{
		public InvalidUserTokenException()
			: base("The given user token is invalid")
		{
		}

		public InvalidUserTokenException(string message)
			: base(message)
		{
		}

		public InvalidUserTokenException(string message, Exception inner)
			: base(message, inner)
		{
		}

#if NETFRAMEWORK
		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		protected InvalidUserTokenException(SerializationInfo info, StreamingContext context) : base(info, context)
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