using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.Serialization;
#if NET10_0_OR_GREATER
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
#endif

namespace Tailviewer.Archiver.Plugins
{
	/// <summary>
	///     Describes a .NET assembly contained in a plugin package.
	/// </summary>
	[DataContract]
	public sealed class AssemblyDescription : IAssemblyDescription
	{
		/// <inheritdoc />
		[DataMember]
		public string EntryName { get; set; }

		/// <inheritdoc />
		[DataMember]
		public string AssemblyName { get; set; }

		/// <inheritdoc />
		Version IAssemblyDescription.AssemblyVersion
		{
			get
			{
				Version version;
				Version.TryParse(AssemblyVersion, out version);
				return version;
			}
		}

		[DataMember]
		public string AssemblyVersion { get; set; }

		/// <inheritdoc />
		Version IAssemblyDescription.AssemblyFileVersion
		{
			get
			{
				Version version;
				Version.TryParse(AssemblyFileVersion, out version);
				return version;
			}
		}

		[DataMember]
		public string AssemblyFileVersion { get; set; }

		/// <inheritdoc />
		[DataMember]
		public string AssemblyInformationalVersion { get; set; }

		/// <inheritdoc />
		[DataMember]
		public List<AssemblyReference> Dependencies { get; set; }

		IReadOnlyList<IAssemblyReference> IAssemblyDescription.Dependencies => Dependencies;

		public static AssemblyDescription FromRawData(byte[] rawAssembly)
		{
#if NET10_0_OR_GREATER
			return FromRawData(rawAssembly, out _);
#else
			var assembly = Assembly.Load(rawAssembly);
			return FromAssembly(assembly);
#endif
		}

		public static AssemblyDescription FromFile(string assemblyFile)
		{
#if NET10_0_OR_GREATER
			return FromRawData(File.ReadAllBytes(assemblyFile));
#else
			var assembly = Assembly.LoadFrom(assemblyFile);
			return FromAssembly(assembly);
#endif
		}

#if NET10_0_OR_GREATER
		internal static AssemblyDescription FromRawData(byte[] bytes, out string targetFramework)
		{
			using var stream = new MemoryStream(bytes);
			using var pe = new PEReader(stream);
			var reader = pe.GetMetadataReader();
			var definition = reader.GetAssemblyDefinition();
			var result = new AssemblyDescription
			{
				AssemblyName = reader.GetString(definition.Name),
				AssemblyVersion = definition.Version.ToString(),
				Dependencies = new List<AssemblyReference>()
			};
			targetFramework = null;
			foreach (var handle in definition.GetCustomAttributes())
			{
				var attribute = reader.GetCustomAttribute(handle);
				if (attribute.Constructor.Kind != HandleKind.MemberReference)
					continue;
				var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
				if (constructor.Parent.Kind != HandleKind.TypeReference)
					continue;
				var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
				var name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
				if (name != "System.Reflection.AssemblyVersionAttribute" && name != "System.Reflection.AssemblyFileVersionAttribute" && name != "System.Reflection.AssemblyInformationalVersionAttribute" && name != "System.Runtime.Versioning.TargetFrameworkAttribute")
					continue;
				var value = reader.GetBlobReader(attribute.Value);
				if (value.ReadUInt16() != 1)
					throw new BadImageFormatException("Invalid assembly attribute encoding");
				var text = value.ReadSerializedString();
				switch (name)
				{
					case "System.Reflection.AssemblyVersionAttribute": result.AssemblyVersion = text; break;
					case "System.Reflection.AssemblyFileVersionAttribute": result.AssemblyFileVersion = TryParse(text)?.ToString(); break;
					case "System.Reflection.AssemblyInformationalVersionAttribute": result.AssemblyInformationalVersion = text; break;
					case "System.Runtime.Versioning.TargetFrameworkAttribute": targetFramework = text; break;
				}
			}
			foreach (var handle in reader.AssemblyReferences)
			{
				var reference = reader.GetAssemblyReference(handle);
				var name = new AssemblyName { Name = reader.GetString(reference.Name), Version = reference.Version, CultureName = reader.GetString(reference.Culture) };
				var key = reader.GetBlobBytes(reference.PublicKeyOrToken);
				if ((reference.Flags & AssemblyFlags.PublicKey) != 0) name.SetPublicKey(key);
				else name.SetPublicKeyToken(key);
				result.Dependencies.Add(new AssemblyReference { FullName = name.FullName });
			}
			return result;
		}
#endif

		public static AssemblyDescription FromAssembly(Assembly assembly)
		{
			var description = new AssemblyDescription
			{
				AssemblyName = assembly.GetName().Name,
				Dependencies = new List<AssemblyReference>(),
			};

			var assemblyVersion = assembly.GetCustomAttribute<AssemblyVersionAttribute>();
			var assemblyFileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			var assemblyInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

			description.AssemblyVersion = assemblyVersion?.Version ?? assembly.GetName().Version?.ToString();
			if (assemblyFileVersion != null)
				description.AssemblyFileVersion = TryParse(assemblyFileVersion.Version)?.ToString();
			if (assemblyInformationalVersion != null)
				description.AssemblyInformationalVersion = assemblyInformationalVersion.InformationalVersion;

			var references = assembly.GetReferencedAssemblies();
			foreach (var referencedAssembly in references)
				description.Dependencies.Add(new AssemblyReference
				{
					FullName = referencedAssembly.FullName
				});

			return description;
		}

		[Pure]
		private static Version TryParse(string versionString)
		{
			Version version;
			Version.TryParse(versionString, out version);
			return version;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return AssemblyName ?? string.Empty;
		}
	}
}
