using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using AGSProof;

namespace AGSSpellchecker {
public class EditRecord {public string Id,Target,Caption,BeforeHash,AfterHash,BeforeText,AfterText,When;public bool Undone,Saved;}
public class PluginState {public string Language="en-US";public List<string> Ignored=new List<string>();public List<string> Words=new List<string>();public List<EditRecord> History=new List<EditRecord>();}
public class ReviewSession {
 public readonly EditorBridge Bridge;public readonly string Root;public PluginState State;public string Store {get{return Path.Combine(Root,".ags-proofreader","AGS Spellchecker");}}
 public ReviewSession(EditorBridge bridge){Bridge=bridge;Root=Path.GetFullPath(bridge.Root);string path=Path.Combine(Store,"state.json");if(File.Exists(path))State=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<PluginState>(File.ReadAllText(path));else{State=new PluginState();string legacy=Path.Combine(Root,".ags-proofreader","state.json");if(File.Exists(legacy)){var old=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<AGSProof.State>(File.ReadAllText(legacy));State.Ignored=old.Ignored;State.Words=old.Words;}}if(State==null)throw new IOException("Spellchecker settings could not be read.");}
 public void EnsureGame(){if(!string.Equals(Path.GetFullPath(Bridge.Root),Root,StringComparison.OrdinalIgnoreCase))throw new IOException("The open game changed. Scan the current project again.");}
 public void SaveState(){EnsureGame();Directory.CreateDirectory(Store);Project.Atomic(Path.Combine(Store,"state.json"),Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(State)));}
 public AGSProof.State ProofState {get{return new AGSProof.State{Ignored=State.Ignored,Words=State.Words};}}
 public void Ignore(LineItem line){if(!State.Ignored.Contains(line.Entry.Key))State.Ignored.Add(line.Entry.Key);SaveState();}
 public void AddWord(string word){word=word.ToLowerInvariant();if(!State.Words.Contains(word))State.Words.Add(word);SaveState();}
 static string Hash(string text){return Project.Hash(Encoding.UTF8.GetBytes(text));}
 public void Apply(LineItem item,string replacement){EnsureGame();Bridge.CheckCapabilities();var entry=item.Entry;if(entry.Text==replacement)return;
  if(Project.Tokens(entry.Text)!=Project.Tokens(replacement))throw new IOException("Keep voice numbers, formatting placeholders and AGS line-break markers unchanged.");
  if(replacement.Contains("\0"))throw new IOException("Text cannot contain a null character.");
  if(entry.Kind=="dialog"&&(replacement.Contains("\n")||replacement.Contains("\r")))throw new IOException("Keep a dialogue speech line on one line.");
  string before=item.Target.Read();if(before!=item.Target.Snapshot)throw new IOException("This text changed in AGS since the scan. Rescan before applying the correction.");
  string after=item.Target.Script?before.Remove(entry.Start,entry.Length).Insert(entry.Start,entry.Kind=="script"?Project.Escape(replacement):replacement):replacement;
  var encoding=EditorBridge.Get(Bridge.Game,"TextEncoding") as Encoding;if(encoding!=null){var strict=Encoding.GetEncoding(encoding.CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);strict.GetBytes(after);}
  string id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");string backup=Path.Combine(Store,id);Directory.CreateDirectory(backup);Project.Atomic(Path.Combine(backup,"before.txt"),Encoding.UTF8.GetBytes(before));
  string disk=Path.Combine(Root,item.Target.File);if(File.Exists(disk))File.Copy(disk,Path.Combine(backup,"original-file.bak"));
  var record=new EditRecord{Id=id,Target=item.Target.Key,Caption=item.Source,BeforeHash=Hash(before),AfterHash=Hash(after),BeforeText=entry.Text,AfterText=replacement,When=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")};State.History.Add(record);SaveState();
  item.Target.Write(after);bool saved=Bridge.SaveGame();record.Saved=saved;SaveState();if(!saved)throw new IOException("The correction is in AGS, but AGS did not finish saving the project. Save from AGS or use Undo in Spellchecker. The backup is retained.");
 }
 public void Undo(){EnsureGame();Bridge.CheckCapabilities();var record=State.History.LastOrDefault(x=>!x.Undone);if(record==null)throw new IOException("No plugin edits to undo.");var target=Bridge.Resolve(record.Target);if(target==null)throw new IOException("The edited item no longer exists.");string current=target.Read();if(Hash(current)==record.BeforeHash){record.Undone=true;SaveState();return;}if(Hash(current)!=record.AfterHash)throw new IOException("The edited text changed again in AGS. Undo stopped to protect newer work. Your original text is backed up in "+Path.Combine(Store,record.Id));string before=File.ReadAllText(Path.Combine(Store,record.Id,"before.txt"),Encoding.UTF8);if(Hash(before)!=record.BeforeHash)throw new IOException("Backup verification failed.");target.Write(before);if(!Bridge.SaveGame())throw new IOException("Undo was applied in AGS but the project save did not complete. Save from AGS. The undo backup remains available.");record.Undone=true;SaveState();}
}
}

