using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AGS.Types;
using AGSProof;

namespace AGSSpellchecker {
public sealed class TextTarget {
 public string Key,Caption,File,Category,Snapshot; public Func<string> Read;public Action<string> Write;public Action<int> Navigate;public bool Script,Dialog,Prose,Translation;
}
public sealed class LineItem {
 public TextTarget Target;public Entry Entry;public List<Issue> Issues=new List<Issue>();public bool Checked,Ignored;
 public string AssignedLanguage;public LanguageDetection Detection;public bool Deferred;public string Original;public bool Reviewed;public int Ordinal; public bool Pending {get{return !Deferred&&!Ignored&&!Reviewed;}} public string Status {get{return Deferred?"Other language":!Checked?"Needs scan":Ignored?"Ignored":Reviewed?"Checked":"Not checked";}} public string Category {get{return Entry.Category;}} public string Source {get{return Target.Caption+" · "+Entry.Location;}}
 public string Text {get{return Entry.Text;}}public int Count {get{return Issues.Count(x=>!x.Ignored);}}
 public string Result {get{return Ignored?"Ignored":Checked?(Issues.Count==0?"Clear":Issues.Count+" issues"):"Not checked";}}
 public string Identity {get{return Target.Key+"|"+Entry.Start;}}
}
public class EditorBridge {
 public readonly IAGSEditor Editor;public IGame Game {get{return Editor.CurrentGame;}}public string Root {get{return Game.DirectoryPath;}}
 public readonly List<string> Warnings=new List<string>();
 public EditorBridge(IAGSEditor editor){Editor=editor;}
 public static object Get(object o,string property){if(o==null)return null;var p=o.GetType().GetProperty(property,BindingFlags.Public|BindingFlags.Instance);return p==null?null:p.GetValue(o,null);}
 public static object Call(object o,string name,params object[] args){var m=o.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).FirstOrDefault(x=>x.Name==name&&x.GetParameters().Length==args.Length&&x.GetParameters().Select((p,i)=>args[i]==null||p.ParameterType.IsInstanceOfType(args[i])).All(valid=>valid));if(m==null)throw new NotSupportedException("This AGS version does not expose "+name+". No game files were changed by this operation.");try{return m.Invoke(o,args);}catch(TargetInvocationException ex){throw ex.InnerException??ex;}}
 public IScriptEditorControl Buffer(string file){return (IScriptEditorControl)Call(Editor.GUIController,"GetScriptEditorControl",file,false);}
 public string ReadScript(string file,Func<string> fallback){var c=Buffer(file);return c==null?fallback():c.Text;}
 public void WriteScript(string file,string value,Action<string> model){var c=Buffer(file);if(c==null)throw new IOException("AGS could not open the editor buffer for "+file+".");Call(c,"ModifyText",value);model(value);}
 public bool SaveGame(){var a=Editor.GetType().Assembly;var t=a.GetType("AGS.Editor.AGSEditor",true);var prop=t.GetProperty("Instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);if(prop==null)throw new NotSupportedException("AGS save integration unavailable.");return (bool)Call(prop.GetValue(null,null),"SaveGameFiles");}
 public void CheckCapabilities(){if(Editor.GUIController.GetType().GetMethod("GetScriptEditorControl",new[]{typeof(string),typeof(bool)})==null)throw new NotSupportedException("Live script access is unavailable in this AGS version.");var t=Editor.GetType().Assembly.GetType("AGS.Editor.AGSEditor");if(t==null||t.GetMethod("SaveGameFiles")==null)throw new NotSupportedException("AGS save integration unavailable.");}
 public void OpenScript(string file,int line){Editor.GUIController.OpenEditorForScript(file,Math.Max(1,line));}
 void OpenObject(string component,string command,object obj,string property){var c=Editor.Components.FirstOrDefault(x=>x.ComponentID==component);if(c!=null&&command!=null)c.CommandClick(command);Call(Editor.GUIController,"SetPropertyGridObject",obj);Call(Editor.GUIController,"SelectPropertyByName",property);Editor.GUIController.RefreshPropertyGrid();}
 TextTarget Property(string key,string caption,object obj,string property,string category,string component,string command,bool prose=false){var p=obj.GetType().GetProperty(property);if(p==null||p.PropertyType!=typeof(string)||!p.CanWrite)return null;return new TextTarget{Key=key,Caption=caption,File="Game.agf",Category=category,Prose=prose,Read=()=> (string)p.GetValue(obj,null)??"",Write=v=>{p.SetValue(obj,v,null);Editor.GUIController.RefreshPropertyGrid();},Navigate=line=>OpenObject(component,command,obj,property)};}
 TextTarget ScriptTarget(Script script){string file=script.FileName;return new TextTarget{Key="script:"+file,Caption=file,File=file,Category=file.StartsWith("room",StringComparison.OrdinalIgnoreCase)?"Room script text":"Speech",Script=true,Read=()=>ReadScript(file,()=>script.Text),Write=v=>WriteScript(file,v,x=>{script.Text=x;script.Modified=true;}),Navigate=line=>OpenScript(file,line)};}
 TextTarget DialogTarget(Dialog d){return new TextTarget{Key="dialog:"+d.ID,Caption=d.Name+" (dialogue "+d.ID+")",File=d.FileName,Category="Dialogue",Script=true,Dialog=true,Read=()=>ReadScript(d.FileName,()=>d.Script),Write=v=>WriteScript(d.FileName,v,x=>{d.Script=x;d.ScriptChangedSinceLastConverted=true;}),Navigate=line=>OpenScript(d.FileName,line)};}
 public TextTarget Resolve(string key){if(key.StartsWith("roomprop:")){var bits=key.Split(':');return RoomTarget(int.Parse(bits[1]),bits[2],int.Parse(bits[3]));}return Targets(false).FirstOrDefault(x=>x.Key==key);}
 public List<TextTarget> Targets(bool includeRooms){var result=new List<TextTarget>();
  foreach(ScriptAndHeader pair in Game.ScriptsAndHeaders){result.Add(ScriptTarget(pair.Header));result.Add(ScriptTarget(pair.Script));}
  foreach(var d in Game.Dialogs){result.Add(DialogTarget(d));foreach(var opt in d.Options){var option=opt;result.Add(Property("option:"+d.ID+":"+option.ID,d.Name+" / option "+option.ID,option,"Text","Dialogue options","Dialogs","Dialog"+d.ID));}}
  foreach(var gui in Game.GUIs)foreach(var control in gui.Controls){var target=Property("gui:"+gui.ID+":"+control.ID,gui.Name+" / "+control.Name,control,"Text","GUI text","GUIs","GUI"+gui.ID);if(target!=null)result.Add(target);}
  foreach(var ch in Game.Characters)result.Add(Property("character:"+ch.ID,ch.ScriptName,ch,"RealName","Names / descriptions","Characters","Character"+ch.ID));
  foreach(var inv in Game.InventoryItems)result.Add(Property("inventory:"+inv.ID,inv.Name,inv,"Description","Names / descriptions","Inventory","InventoryItem"+inv.ID));
  result.Add(Property("settings:name","Game title",Game.Settings,"GameName","Names / descriptions","GeneralSettings","GeneralSettings"));
  var messages=Get(Game,"GlobalMessages") as string[];if(messages!=null)for(int i=0;i<messages.Length;i++){int n=i;result.Add(new TextTarget{Key="global:"+n,Caption="Global message "+(n+500),File="Game.agf",Category="Narration / UI",Prose=true,Read=()=>messages[n]??"",Write=v=>messages[n]=v,Navigate=line=>OpenObject("GeneralSettings","GeneralSettings",Game,"GlobalMessages")});}
  foreach(var room in Game.Rooms){try{if(room.Script==null)room.LoadScript();if(room.Script!=null)result.Add(ScriptTarget(room.Script));}catch(Exception ex){Warnings.Add(room.ScriptFileName+": "+ex.Message);}}
  if(includeRooms){foreach(var room in Game.Rooms){try{var loaded=Editor.RoomController.CurrentRoom;if(loaded!=null&&loaded.Number==room.Number){foreach(var h in loaded.Hotspots)result.Add(RoomTarget(room.Number,"hotspot",h.ID));foreach(var o in loaded.Objects)result.Add(RoomTarget(room.Number,"object",o.ID));foreach(var m in loaded.Messages)result.Add(RoomTarget(room.Number,"message",m.ID));}
    else {var p=new Project{Root=Root,GameEncoding=(Get(Game,"TextEncoding") as Encoding)??Source.StrictEncoding("windows-1252")};Rooms.Extract(p,new Source(Path.Combine(Root,room.FileName),true,p.GameEncoding));foreach(var item in p.Entries){string kind=item.Location.StartsWith("Hotspot")?"hotspot":item.Location.StartsWith("Object")?"object":"message";int id=int.Parse(Regex.Match(item.Location,@"\d+").Value);var target=RoomTarget(room.Number,kind,id);target.Snapshot=item.Text;result.Add(target);}}}catch(Exception ex){Warnings.Add(room.FileName+": "+ex.Message);}}}
  return result.Where(x=>x!=null).ToList();
 }
 object RoomObject(int number,string kind,int id,bool load){var r=Editor.RoomController.CurrentRoom;if(r==null||r.Number!=number){if(!load)return null;var info=Game.Rooms.FirstOrDefault(x=>x.Number==number);if(info==null||!Editor.RoomController.LoadRoom(info))throw new IOException("Could not load room "+number+"; the operation was stopped.");r=Editor.RoomController.CurrentRoom;}if(kind=="hotspot")return r.Hotspots.FirstOrDefault(x=>x.ID==id);if(kind=="object")return r.Objects.FirstOrDefault(x=>x.ID==id);return r.Messages.FirstOrDefault(x=>x.ID==id);}
 TextTarget RoomTarget(int number,string kind,int id){string property=kind=="message"?"Text":"Description";var t=new TextTarget{Key="roomprop:"+number+":"+kind+":"+id,Caption="Room "+number+" / "+kind+" "+id,File="room"+number+".crm",Category="Room names / messages",Prose=kind=="message"};t.Read=()=>{object obj=RoomObject(number,kind,id,true);if(obj==null)throw new IOException("Room item no longer exists. Rescan the project.");return (string)Get(obj,property)??"";};t.Write=v=>{object obj=RoomObject(number,kind,id,true);if(obj==null)throw new IOException("Room item no longer exists.");obj.GetType().GetProperty(property).SetValue(obj,v,null);Editor.RoomController.CurrentRoom.Modified=true;Editor.GUIController.RefreshPropertyGrid();};t.Navigate=line=>{object obj=RoomObject(number,kind,id,true);OpenObject("Rooms","Roe"+number,obj,property);};return t;}
 public List<LineItem> ExtractProjectTranslations(string file){var result=new List<LineItem>();foreach(var translation in Game.Translations){if(file!=null&&!string.Equals(file,translation.FileName,StringComparison.OrdinalIgnoreCase))continue;try{if(translation.Modified)throw new IOException("Save pending translation changes in AGS before scanning.");var rows=ReadTranslation(Path.Combine(Root,translation.FileName),translation.Encoding??((Get(Game,"TextEncoding") as Encoding)??Source.StrictEncoding("windows-1252")));if(rows.Count>0){var target=rows[0].Target;var read=target.Read;var write=target.Write;target.Read=()=>{if(translation.Modified)throw new IOException("Translation changed in AGS. Save and rescan before correcting it.");return read();};target.Write=text=>{write(text);translation.LoadData();};}result.AddRange(rows);}catch(Exception ex){Warnings.Add(translation.FileName+": "+ex.Message);}}return result;}
 public List<LineItem> ExtractTranslation(string path){Warnings.Clear();return ReadTranslation(path,(Get(Game,"TextEncoding") as Encoding)??Source.StrictEncoding("windows-1252"));}
 public static List<LineItem> ReadTranslation(string path,Encoding fallback){
  path=Path.GetFullPath(path);if(!path.EndsWith(".trs",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose an editable AGS translation source (.trs).");
  string header=Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(path));
  if(Regex.IsMatch(header,@"(?im)^//#Encoding\s*=\s*UTF-8\s*$"))fallback=new UTF8Encoding(false,true);
  fallback=Encoding.GetEncoding(fallback.CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);var source=new Source(path,false,fallback);var target=new TextTarget{Key="translation:"+path,Caption=Path.GetFileName(path),File=path,Category="Translations",Script=true,Translation=true,Snapshot=source.Text};
  target.Read=()=>new Source(path,false,fallback).Text;target.Write=text=>Project.Atomic(path,source.Encode(text));
  target.Navigate=line=>System.Diagnostics.Process.Start("notepad.exe","\""+path+"\"");
  var result=new List<LineItem>();int index=0;
  foreach(Match m in Regex.Matches(source.Text,@"[^\r\n]*(?:\r\n|\n|\r|$)")){if(m.Length==0)continue;string text=m.Value.TrimEnd('\r','\n');if(text.StartsWith("//"))continue;
   if(index%2==1&&Regex.IsMatch(text,@"\p{L}"))result.Add(new LineItem{Target=target,Ordinal=index/2,Original=text,Entry=new Entry{Text=text,File=path,Source=source,Category="Translations",Location="line "+Project.Line(source.Text,m.Index),Kind="plain",Start=m.Index,Length=text.Length,Prose=true}});index++;
  }if(index%2!=0)throw new IOException("Translation file has an unmatched original line. Each original must have a translation line (which may be blank).");return result;
 }
 public List<LineItem> Extract(){Warnings.Clear();var result=new List<LineItem>();foreach(var target in Targets(true)){try{string text=target.Snapshot??target.Read();target.Snapshot=text;if(target.Script){var source=new Source(Path.Combine(Root,target.File),text);var p=new Project{Root=Root};p.ExtractScript(source,text,0,target.Dialog);int ordinal=0;foreach(var entry in p.Entries)result.Add(new LineItem{Target=target,Entry=entry,Ordinal=ordinal++,Original=entry.Text});}else if(Regex.IsMatch(text,@"\p{L}")){result.Add(new LineItem{Target=target,Original=text,Entry=new Entry{Text=text,File=target.File,Category=target.Category,Location="property",Kind="property",Start=0,Length=text.Length,Prose=target.Prose}});}}catch(Exception ex){Warnings.Add(target.Caption+": "+ex.Message);}}return result;}
}
}




