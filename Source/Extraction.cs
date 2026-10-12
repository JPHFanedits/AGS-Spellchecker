using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace AGSProof {
public class Entry {
 public string File {get;set;} public string Category {get;set;} public string Location {get;set;} public string Text {get;set;}
 public bool Prose; public string Kind; public int Start,Length,BlockLengthOffset=-1,BlockLengthSize; public long BlockLength;
 public Source Source; public string Key { get { return Project.Hash(Encoding.UTF8.GetBytes(Text)); } }
 public string Preview {get {return Text.Replace("\r"," ").Replace("\n"," ");}}
}
public class Source {
 public string Path; public byte[] Bytes; public string Text; public Encoding Encoding; public int Bom;
 public Source(string path,string text){Path=path;Text=text;Encoding=System.Text.Encoding.UTF8;Bytes=Encoding.GetBytes(text);}
 public Source(string path, bool binary, Encoding fallback) {
  Path=path; Bytes=System.IO.File.ReadAllBytes(path); Encoding=fallback;
  if(binary)return;
  if(Bytes.Length>=3 && Bytes[0]==239 && Bytes[1]==187 && Bytes[2]==191){Encoding=new UTF8Encoding(false,true);Bom=3;}
  else if(Bytes.Length>=2 && Bytes[0]==255 && Bytes[1]==254){Encoding=new UnicodeEncoding(false,false,true);Bom=2;}
  else if(Bytes.Length>=2 && Bytes[0]==254 && Bytes[1]==255){Encoding=new UnicodeEncoding(true,false,true);Bom=2;}
  else if(System.IO.Path.GetExtension(path).Equals(".agf",StringComparison.OrdinalIgnoreCase)){
   var m=Regex.Match(System.Text.Encoding.ASCII.GetString(Bytes.Take(160).ToArray()),"encoding=[\"']([^\"']+)");
   if(m.Success)Encoding=StrictEncoding(m.Groups[1].Value);
  }
  Text=Encoding.GetString(Bytes,Bom,Bytes.Length-Bom);
 }
 public static Encoding StrictEncoding(string name){return Encoding.GetEncoding(name,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);}
 public byte[] Encode(string s){return Bytes.Take(Bom).Concat(Encoding.GetBytes(s)).ToArray();}
}
public class Record {public string Id,File,BeforeHash,AfterHash,Before,After,When;public bool Reverted;}
public class State {public List<string> Ignored=new List<string>();public List<string> Words=new List<string>();public List<Record> History=new List<Record>();}
public class Project {
 public string Root; public List<Entry> Entries=new List<Entry>(); public List<string> Warnings=new List<string>(); public State State=new State();
 public Encoding GameEncoding=Source.StrictEncoding("windows-1252");
 public string StateDir {get{return Path.Combine(Root,".ags-proofreader");}}
 public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
 public static string SafePath(string root,string relative){var p=Path.GetFullPath(Path.Combine(root,relative));if(!p.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Path is outside the game folder.");return p;}
 public void Load(string root){Root=Path.GetFullPath(root);Entries.Clear();Warnings.Clear();
  var agfs=Directory.GetFiles(Root,"*.agf");if(agfs.Length!=1)throw new IOException("Choose a folder containing exactly one editable AGS .agf project.");
  var statePath=Path.Combine(StateDir,"state.json");if(System.IO.File.Exists(statePath)){try{State=new JavaScriptSerializer().Deserialize<State>(System.IO.File.ReadAllText(statePath));}catch(Exception e){throw new IOException("Ignore/history data could not be read. Preserve .ags-proofreader before repairing it. "+e.Message);}}
  else State=new State();
  var agf=new Source(agfs[0],false,GameEncoding);var doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(agf.Text);
  var enc=doc.SelectSingleNode("//Settings/GameTextEncoding");if(enc!=null && enc.InnerText.Equals("UTF-8",StringComparison.OrdinalIgnoreCase))GameEncoding=Source.StrictEncoding("utf-8");
  ExtractXml(agf);
  var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(XmlNode node in doc.SelectNodes("//Scripts//FileName")){var ext=Path.GetExtension(node.InnerText).ToLowerInvariant();if(ext==".asc"||ext==".ash")files.Add(node.InnerText);}
  foreach(XmlNode node in doc.SelectNodes("//Rooms//UnloadedRoom/Number | //Rooms//Room/Number")){files.Add("room"+node.InnerText+".asc");files.Add("room"+node.InnerText+".crm");}
  // AGS keeps room and script source in the project root. No recursive backup/build scans.
  foreach(string p in Directory.GetFiles(Root))if(Path.GetExtension(p).Equals(".trs",StringComparison.OrdinalIgnoreCase))files.Add(Path.GetFileName(p));
  foreach(var file in files.OrderBy(x=>x)){try{var p=SafePath(Root,file);if(!System.IO.File.Exists(p)){Warnings.Add(file+": referenced source is missing.");continue;}
   var ext=Path.GetExtension(p).ToLowerInvariant();var s=new Source(p,ext==".crm",GameEncoding);
   if(ext==".crm")Rooms.Extract(this,s);else if(ext==".trs")ExtractTranslation(s);else ExtractScript(s,s.Text,0,false);
  }catch(Exception e){Warnings.Add(file+": "+e.Message);}}
 }
 public void Add(Source s,string category,string location,string text,string kind,int start,int length,bool prose=false){if(!Regex.IsMatch(text,@"\p{L}"))return;Entries.Add(new Entry{Source=s,File=s.Path.Substring(Root.Length).TrimStart('\\'),Category=category,Location=location,Text=text,Kind=kind,Start=start,Length=length,Prose=prose});}
 public static string Unescape(string s){return Regex.Replace(s,@"\\([\\""nrt])",m=>m.Groups[1].Value=="n"?"\n":m.Groups[1].Value=="r"?"\r":m.Groups[1].Value=="t"?"\t":m.Groups[1].Value);}
 public static string Escape(string s){return s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t");}
 public void ExtractScript(Source s,string code,int offset,bool dialog){
  var masked=code.ToCharArray();bool block=false;
  for(int i=0;i<code.Length;){
   if(block){if(i+1<code.Length&&code[i]=='*'&&code[i+1]=='/'){masked[i++]= ' ';masked[i++]=' ';block=false;}else {if(code[i]!='\n'&&code[i]!='\r')masked[i]=' ';i++;}continue;}
   if(i+1<code.Length&&code[i]=='/'&&code[i+1]=='*'){block=true;masked[i++]=' ';masked[i++]=' ';continue;}
   if(i+1<code.Length&&code[i]=='/'&&code[i+1]=='/'){while(i<code.Length&&code[i]!='\n'){masked[i++]=' ';}continue;}
   // Dialogue's speaker syntax allows literal quotes; consume the entire spoken line first.
   if(dialog && (i==0||code[i-1]=='\n')){int end=code.IndexOf('\n',i);if(end<0)end=code.Length;string line=code.Substring(i,end-i).TrimEnd('\r');var dm=Regex.Match(line,@"^[A-Za-z_][\w]*:\s*(.*)$");if(dm.Success){var g=dm.Groups[1];Add(s,"Dialogue", "line "+Line(s.Text,offset+i+g.Index),g.Value,"dialog",offset+i+g.Index,g.Length,true);for(int j=i;j<end;j++)masked[j]=' ';i=end;continue;}}
   if(code[i]=='"'){int start=++i;while(i<code.Length){if(code[i]=='\\'){i+=2;continue;}if(code[i]=='"')break;i++;}if(i>=code.Length)throw new IOException("Unterminated script string.");
    int statementStart=start-1;while(statementStart>0 && masked[statementStart-1]!=';' && masked[statementStart-1]!='{' && masked[statementStart-1]!='}')statementStart--;
    string context=new string(masked,statementStart,start-1-statementStart);bool prose;string category=VisibleCategory(context,out prose);
    if(category!=null){if(Path.GetFileName(s.Path).StartsWith("room",StringComparison.OrdinalIgnoreCase)&&category!="Names / descriptions")category="Room script text";Add(s,category,"line "+Line(s.Text,offset+start),Unescape(code.Substring(start,i-start)),"script",offset+start,i-start,prose);}
    for(int j=start-1;j<=i;j++)masked[j]=' ';i++;continue;
   }i++;
  }
 }
 class CallContext {public string Name;public int Arg;}
 static string VisibleCategory(string context,out bool prose){prose=false;var calls=new List<CallContext>();
  foreach(Match t in Regex.Matches(context,@"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*|[(),]")){if(t.Value=="("){string prefix=context.Substring(0,t.Index);var name=Regex.Match(prefix,@"([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*$");calls.Add(new CallContext{Name=name.Success?name.Groups[1].Value:""});}else if(t.Value==")"){if(calls.Count>0)calls.RemoveAt(calls.Count-1);}else if(t.Value==","&&calls.Count>0)calls[calls.Count-1].Arg++;}
  for(int i=calls.Count-1;i>=0;i--){string name=calls[i].Name.Split('.').Last();int arg=calls[i].Arg;
   if(name=="Format"&&calls[i].Name=="String.Format")continue;
   if(name=="GetTranslation"){if(i==0)return "Narration / UI";continue;}
   int first=-1;bool speech=false;
   switch(name){case "Say":case "Think":case "SayBackground":first=0;speech=true;break;case "SayAt":first=3;speech=true;break;case "SayBackgroundVoiced":first=1;speech=true;break;case "DisplaySpeech":case "DisplayThought":case "DisplaySpeechBackground":first=1;speech=true;break;case "DisplaySpeechAt":first=4;speech=true;break;
   case "Display":first=0;speech=true;break;case "DisplayAt":first=3;speech=true;break;case "DisplayAtY":first=1;speech=true;break;case "DisplayTopBar":first=3;speech=true;break;
   case "SetText":case "AddItem":case "SetName":first=0;break;case "SetItemText":case "InsertItemAt":case "SaveGameSlot":case "SetObjectName":case "SetHotspotName":case "SetInvName":first=1;break;case "SetLabelText":case "SetButtonText":first=2;break;case "DrawString":first=3;break;case "DrawStringWrapped":first=5;break;case "CreateTextual":first=5;break;case "SetTextual":first=3;break;
   }
   if(first>=0 && arg>=first){prose=speech;return speech?"Speech":"Narration / UI";}
   // Unknown nested calls, comparisons and file operations are not display text.
   if(name!="")return null;
  }
  if(Regex.IsMatch(context,@"\.Text\s*=\s*(?:String\.Format\s*\([^;]*)?$"))return "Narration / UI";
  if(Regex.IsMatch(context,@"\.Name\s*=\s*(?:String\.Format\s*\([^;]*)?$"))return "Names / descriptions";
  return null;
 }
 public static int Line(string s,int pos){int n=1;for(int i=0;i<pos;i++)if(s[i]=='\n')n++;return n;}
 public void ExtractXml(Source s){
  var stack=new List<string>();
  var tokens=Regex.Matches(s.Text,@"<!\[CDATA\[[\s\S]*?\]\]>|<!--[\s\S]*?-->|<[^>]+>|[^<]+");
  foreach(Match t in tokens){string v=t.Value;
   if(v.StartsWith("<![CDATA[")){if(stack.LastOrDefault()=="Script"&&stack.Contains("Dialog"))ExtractScript(s,v.Substring(9,v.Length-12),t.Index+9,true);continue;}
   if(v.StartsWith("<!--")||v.StartsWith("<?"))continue;
   if(v.StartsWith("</")){if(stack.Count>0)stack.RemoveAt(stack.Count-1);continue;}
   if(v.StartsWith("<")){if(!v.EndsWith("/>")){var m=Regex.Match(v,@"^<([\w]+)");if(m.Success)stack.Add(m.Groups[1].Value);}continue;}
   string tag=stack.LastOrDefault();string category=null;
   if(tag=="TitleText" || (tag=="Message" && stack.Contains("GlobalMessages")))category="Narration / UI";
   if(tag=="Text")category=stack.Contains("DialogOption")?"Dialogue options":"Narration / UI";
   if(tag=="RealName"||tag=="Description"&&(stack.Contains("InventoryItem")||stack.Contains("Character")))category="Names / descriptions";
   if((tag=="GameName"||tag=="Description")&&stack.Contains("Settings"))category="Names / descriptions";
   if(category!=null){string decoded=System.Net.WebUtility.HtmlDecode(v);Add(s,category,"line "+Line(s.Text,t.Index),decoded,"xml",t.Index,t.Length,tag=="Message"&&stack.Contains("GlobalMessages"));}
   if(tag=="Script"&&stack.Contains("Dialog")&&!string.IsNullOrWhiteSpace(v))Warnings.Add("Escaped (non-CDATA) dialog script at line "+Line(s.Text,t.Index)+" is unsupported.");
  }
 }
 void ExtractTranslation(Source s){int pos=0;int index=0;foreach(Match m in Regex.Matches(s.Text,@"[^\r\n]*(?:\r\n|\n|\r|$)")){string line=m.Value.TrimEnd('\r','\n');if(m.Length==0)continue;if(!line.StartsWith("//")){if(index%2==1)Add(s,"Translations","line "+Line(s.Text,pos),line,"plain",pos,line.Length);index++;}pos+=m.Length;}}
 public void SaveState(){Directory.CreateDirectory(StateDir);Atomic(Path.Combine(StateDir,"state.json"),Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(State)));}
 public static void Atomic(string path,byte[] bytes){string temp=path+".proof-"+Guid.NewGuid().ToString("N")+".tmp";try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}if(System.IO.File.Exists(path))System.IO.File.Replace(temp,path,null);else System.IO.File.Move(temp,path);}finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}}
 public void Ignore(Entry e){if(!State.Ignored.Contains(e.Key))State.Ignored.Add(e.Key);SaveState();}
 public static string Tokens(string text){return string.Join("|",Regex.Matches(text,@"&\d+|%(?:\d+\$)?[-+0 #]*\d*(?:\.\d+)?[a-zA-Z%]|@[A-Za-z_][\w]*@|\\[A-Za-z]|\[").Cast<Match>().Select(m=>m.Value));}

}
public static class Rooms {
 class Reader {public byte[] B;public int P,End; public Reader(byte[] b,int p,int end){B=b;P=p;End=end;}public void Skip(int n){if(n<0||P+n>End)throw new IOException("Invalid room block length.");P+=n;}public int I(){int v=BitConverter.ToInt32(B,P);Skip(4);return v;}public int H(){int v=BitConverter.ToUInt16(B,P);Skip(2);return v;}public int Count(){int n=I();if(n<0||n>100000)throw new IOException("Invalid room item count.");return n;}public void Z(){while(P<End&&B[P++]!=0){}if(P>End)throw new IOException("Invalid room string.");}}
 public static void Extract(Project p,Source s){var b=s.Bytes;if(b.Length<3)throw new IOException("Truncated room.");int version=BitConverter.ToUInt16(b,0);if(version<31||version>33)throw new IOException("Room format "+version+" is unsupported (supported: AGS 3.4.1.5 through 3.6.2, formats 31–33). Room script is checked separately.");
  int pos=2;var entries=new List<Entry>();try{while(pos<b.Length){int id=b[pos++];if(id==255)break;if(id==0)pos+=16;int size=id==0||version>=32?8:4;int lengthOffset=pos;long len=size==8?BitConverter.ToInt64(b,pos):BitConverter.ToInt32(b,pos);pos+=size;if(len<0||len>b.Length-pos)throw new IOException("Invalid room block.");int end=pos+(int)len;var r=new Reader(b,pos,end);
   if(id==1){r.Skip(4);int wb=r.H();r.Skip(wb*2);int hs=r.Count();r.Skip(hs*4);for(int i=0;i<hs;i++)Add(p,s,r,entries,"Hotspot "+i,false,lengthOffset,size,len);for(int i=0;i<hs;i++){int n=r.Count();r.Skip(n);}if(r.I()!=0)throw new IOException("Legacy room polygons unsupported.");r.Skip(8);int objects=r.H();r.Skip(objects*10);if(r.I()!=0)throw new IOException("Legacy room variables unsupported.");int regions=r.Count();for(int i=0;i<1+hs+objects+regions;i++){int events=r.Count();for(int j=0;j<events;j++)r.Z();}r.Skip(objects*4+4+objects*2+2);int areas=r.Count();r.Skip(areas*10+11+10);int messages=r.H();r.Skip(4+messages*2);for(int i=0;i<messages;i++)Add(p,s,r,entries,"Room message "+i,true,lengthOffset,size,len);}
   else if(id==5){int n=b[r.P];r.Skip(1);for(int i=0;i<n;i++)Add(p,s,r,entries,"Object "+i,false,lengthOffset,size,len);}
   pos=end;
  }p.Entries.AddRange(entries);}catch{throw;}
 }
 static void Add(Project p,Source s,Reader r,List<Entry> entries,string location,bool encrypted,int bo,int bs,long bl){int start=r.P;int n=r.Count();if(n>r.End-r.P)throw new IOException("Invalid room text length.");byte[] text=s.Bytes.Skip(r.P).Take(n).ToArray();r.Skip(n);if(encrypted){byte[] key=Encoding.ASCII.GetBytes("Avis Durgan");for(int i=0;i<text.Length;i++)text[i]=unchecked((byte)(text[i]-key[i%key.Length]));}
  string value=p.GameEncoding.GetString(text).TrimEnd('\0');if(!Regex.IsMatch(value,@"\p{L}"))return;s.Encoding=p.GameEncoding;entries.Add(new Entry{Source=s,File=Path.GetFileName(s.Path),Category="Room names / messages",Location=location,Text=value,Kind=encrypted?"roomEncrypted":"room",Prose=encrypted,Start=start,Length=4+n,BlockLengthOffset=bo,BlockLengthSize=bs,BlockLength=bl});}
}
}
