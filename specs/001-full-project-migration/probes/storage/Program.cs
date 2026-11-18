using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using IsabelDb;
using Tailviewer.Archiver.Repository;
using Tailviewer.PluginRepository.Entities;
class Program
{
 static readonly Type[] Types = { typeof(User), typeof(PluginIdentifier), typeof(PluginInterface), typeof(Tailviewer.PluginRepository.Entities.Change), typeof(PublishedPlugin) };
 static int Main(string[] args)
 {
  try {
   string mode=args[0], path=Path.GetFullPath(args[1]);
   if(mode=="create") {
    if(File.Exists(path)) throw new Exception("Refusing overwrite");
    using(var db=Database.OpenOrCreate(path,Types)) {
     var users=db.GetOrCreateDictionary<string,User>("Users");
     var tokens=db.GetOrCreateDictionary<Guid,string>("UsersByAccessToken");
     var packages=db.GetOrCreateDictionary<PluginIdentifier,byte[]>("Plugins");
     var icons=db.GetOrCreateDictionary<PluginIdentifier,byte[]>("PluginIcons");
     var descriptions=db.GetOrCreateDictionary<PluginIdentifier,PublishedPlugin>("PluginDescriptions");
     for(int i=1;i<=2;i++) {
      var token=new Guid(i,0,0,new byte[8]); var username="probe-user-"+i;
      users.Put(username,new User {Username=username,Email=username+"@example.invalid",AccessToken=token}); tokens.Put(token,username);
     }
     for(int i=1;i<=3;i++) {
      var id=new PluginIdentifier(i==3?"probe.other":"probe.plugin",new Version(1,i,0));
      byte[] package=Enumerable.Range(0,257*i).Select(x=>(byte)(x%256)).ToArray();
      packages.Put(id,package); icons.Put(id,i==1?(args.Length>2 && args[2]=="null"?null:new byte[0]):Enumerable.Range(0,i*13).Select(x=>(byte)x).ToArray());
      descriptions.Put(id,new PublishedPlugin {Identifier=id,Publisher="probe-user-1",Name="Probe "+i,Author="Synthetic",Website=i==1?null:"https://example.invalid",Description="Unicode \u1019\u103c\u1014\u103a\u1019\u102c",SizeInBytes=package.Length,BuildDate=new DateTime(2020,1,i,0,0,0,DateTimeKind.Utc),PublishDate=new DateTime(2021,1,i,0,0,0,DateTimeKind.Utc),RequiredInterfaces=new List<PluginInterface>{new PluginInterface("Tailviewer.Api.Plugins.ILogEntryParserPlugin",1)}});
     }
    }
   } else if(mode=="export") {
    using(var db=Database.OpenRead(path,Types)) {
     Export(db.GetDictionary<string,User>("Users").GetAll().ToList(),args[2],"users");
     Export(db.GetDictionary<Guid,string>("UsersByAccessToken").GetAll().ToList(),args[2],"tokens");
     Export(db.GetDictionary<PluginIdentifier,byte[]>("Plugins").GetAll().OrderBy(x=>x.Key.ToString()).ToList(),args[2],"packages");
     Export(db.GetDictionary<PluginIdentifier,byte[]>("PluginIcons").GetAll().OrderBy(x=>x.Key.ToString()).ToList(),args[2],"icons");
     Export(db.GetDictionary<PluginIdentifier,PublishedPlugin>("PluginDescriptions").GetAll().OrderBy(x=>x.Key.ToString()).ToList(),args[2],"descriptions");
    }
   } else if(mode=="neutral") {
    Verify<string,User>(path,"users",2); Verify<Guid,string>(path,"tokens",2);
    Verify<PluginIdentifier,byte[]>(path,"packages",3); Verify<PluginIdentifier,byte[]>(path,"icons",3); Verify<PluginIdentifier,PublishedPlugin>(path,"descriptions",3);
   } else throw new Exception("Unknown mode");
   Console.WriteLine("PASS "+mode); return 0;
  } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
 }
 static void Export<T>(List<T> rows,string dir,string name) {
  Directory.CreateDirectory(dir); using(var f=File.Create(Path.Combine(dir,name+".xml"))) new DataContractSerializer(typeof(List<T>)).WriteObject(f,rows);
  Console.WriteLine(name+" count="+rows.Count);
 }
 static void Verify<K,V>(string dir,string name,int count) {
  var s=new DataContractSerializer(typeof(List<KeyValuePair<K,V>>)); List<KeyValuePair<K,V>> rows;
  using(var f=File.OpenRead(Path.Combine(dir,name+".xml"))) rows=(List<KeyValuePair<K,V>>)s.ReadObject(f);
  if(rows.Count!=count) throw new Exception("Count mismatch "+name);
  using(var m=new MemoryStream()) { s.WriteObject(m,rows); using(var h=SHA256.Create()) Console.WriteLine(name+" count="+rows.Count+" roundtrip="+BitConverter.ToString(h.ComputeHash(m.ToArray())).Replace("-","").ToLowerInvariant()); }
  if(typeof(K)==typeof(PluginIdentifier)) foreach(var row in rows) {
   Console.Write(name+" id="+row.Key);
   if(typeof(V)==typeof(byte[])) {var bytes=(byte[])(object)row.Value; using(var h=SHA256.Create())Console.Write(bytes==null?" null":" sha256="+BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant());}
   Console.WriteLine();
  }
 }
}
