using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using AGSProof;

namespace AGSSpellchecker {
public class EditRecord {public string Id,Target,Caption,BeforeHash,AfterHash,BeforeText,AfterText,When;public bool Undone,Saved;}
public class ReviewedLine {public string Target,Text,Original,Language;public int Ordinal;public bool Verified;public string Configuration;public List<Issue> Results=new List<Issue>();}
public class PluginState {public Dictionary<string,string> AssignedLanguages=new Dictionary<string,string>();public List<string> IgnoredLines=new List<string>();public bool OnlyPending;public bool AutoLanguage=true,IncludeUncertain=true;public Dictionary<string,LanguageDetection> Detections=new Dictionary<string,LanguageDetection>();public string Language="en-US";public List<string> Ignored=new List<string>();public List<string> Words=new List<string>();public List<EditRecord> History=new List<EditRecord>();public List<ReviewedLine> Reviewed=new List<ReviewedLine>();}
public class ReviewSession {
 public readonly EditorBridge Bridge;public readonly string Root;public PluginState State;public string Store {get{return Path.Combine(Root,".ags-proofreader","AGS Spellchecker");}}
 public ReviewSession(EditorBridge bridge){Bridge=bridge;Root=Path.GetFullPath(bridge.Root);string path=Path.Combine(Store,"state.json");if(File.Exists(path))State=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<PluginState>(File.ReadAllText(path));else{State=new PluginState();string legacy=Path.Combine(Root,".ags-proofreader","state.json");if(File.Exists(legacy)){var old=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<AGSProof.State>(File.ReadAllText(legacy));State.Ignored=old.Ignored;State.Words=old.Words;}}if(State==null)throw new IOException("Spellchecker settings could not be read.");if(State.AssignedLanguages==null)State.AssignedLanguages=new Dictionary<string,string>();if(State.IgnoredLines==null)State.IgnoredLines=new List<string>();if(State.Detections==null)State.Detections=new Dictionary<string,LanguageDetection>();if(State.Reviewed==null)State.Reviewed=new List<ReviewedLine>();State.Words=State.Words.Select(OfflineProof.NormalizeWord).Where(x=>x.Length>0).Distinct().ToList();}
 public void EnsureGame(){if(!string.Equals(Path.GetFullPath(Bridge.Root),Root,StringComparison.OrdinalIgnoreCase))throw new IOException("The open game changed. Scan the current project again.");}
 public void SaveState(){EnsureGame();Directory.CreateDirectory(Store);Project.Atomic(Path.Combine(Store,"state.json"),Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Serialize(State)));}
 public AGSProof.State ProofState {get{return new AGSProof.State{Ignored=State.Ignored,Words=State.Words};}}
 public static string LineKey(LineItem line){return line.Target.Key+"|"+line.Ordinal+"|"+line.Entry.Key;}
 public void Ignore(LineItem line){if(!State.Ignored.Contains(line.Entry.Key))State.Ignored.Add(line.Entry.Key);SaveState();}
 public void AddWord(string word){word=OfflineProof.NormalizeWord(word);if(!State.Words.Contains(word))State.Words.Add(word);SaveState();}
 public ReviewedLine ReviewFor(LineItem line){return State.Reviewed.LastOrDefault(x=>x.Target==line.Target.Key&&x.Ordinal==line.Ordinal&&x.Text==line.Text&&x.Language==State.Language);}
 public string Configuration {get{return "language-v1|"+State.Language+"|"+Hash(string.Join("\n",State.Words.OrderBy(x=>x)));}}
 public void Remember(LineItem line,bool reviewed,bool save=true){State.Reviewed.RemoveAll(x=>x.Target==line.Target.Key&&x.Ordinal==line.Ordinal&&x.Language==State.Language);State.Reviewed.Add(new ReviewedLine{Target=line.Target.Key,Ordinal=line.Ordinal,Text=line.Text,Original=line.Original??line.Text,Language=State.Language,Verified=true,Configuration=Configuration,Results=line.Issues.Select(x=>x.Copy()).ToList()});SetStatus(line);if(save)SaveState();}
 public static void SetStatus(LineItem line){line.Ignored=line.Count==0&&line.Issues.Any(x=>x.Ignored);line.Reviewed=line.Count==0&&!line.Ignored;line.Checked=true;}
 public static void CarryIgnored(string before,string after,List<Issue> old,List<Issue> fresh){int prefix=0;while(prefix<before.Length&&prefix<after.Length&&before[prefix]==after[prefix])prefix++;int suffix=0;while(suffix<before.Length-prefix&&suffix<after.Length-prefix&&before[before.Length-1-suffix]==after[after.Length-1-suffix])suffix++;foreach(var issue in old.Where(x=>x.Ignored)){int start=issue.Start;if(start+issue.Length<=prefix){}else if(start>=before.Length-suffix)start+=after.Length-before.Length;else continue;foreach(var match in fresh.Where(x=>x.RuleId==issue.RuleId&&x.Start==start&&x.Length==issue.Length))match.Ignored=true;}}
 static string Hash(string text){return Project.Hash(Encoding.UTF8.GetBytes(text));}
 public void Apply(LineItem item,string replacement){EnsureGame();Bridge.CheckCapabilities();var entry=item.Entry;if(entry.Text==replacement)return;
  if(Project.Tokens(entry.Text)!=Project.Tokens(replacement))throw new IOException("Keep voice numbers, formatting placeholders and AGS line-break markers unchanged.");
  if(replacement.Contains("\0"))throw new IOException("Text cannot contain a null character.");
  if((entry.Kind=="dialog"||item.Target.Translation)&&(replacement.Contains("\n")||replacement.Contains("\r")))throw new IOException("Keep a dialogue speech line on one line.");
  string before=item.Target.Read();if(before!=item.Target.Snapshot)throw new IOException("This text changed in AGS since the scan. Rescan before applying the correction.");
  string after=item.Target.Script?before.Remove(entry.Start,entry.Length).Insert(entry.Start,entry.Kind=="script"?Project.Escape(replacement):replacement):replacement;
  var encoding=EditorBridge.Get(Bridge.Game,"TextEncoding") as Encoding;if(encoding!=null&&!item.Target.Translation){var strict=Encoding.GetEncoding(encoding.CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);strict.GetBytes(after);}
  string id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");string backup=Path.Combine(Store,id);Directory.CreateDirectory(backup);Project.Atomic(Path.Combine(backup,"before.txt"),Encoding.UTF8.GetBytes(before));
  string disk=Path.Combine(Root,item.Target.File);if(File.Exists(disk))File.Copy(disk,Path.Combine(backup,"original-file.bak"));
  var record=new EditRecord{Id=id,Target=item.Target.Key,Caption=item.Source,BeforeHash=Hash(before),AfterHash=Hash(after),BeforeText=entry.Text,AfterText=replacement,When=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")};State.History.Add(record);SaveState();
  item.Target.Write(after);bool saved=item.Target.Translation||Bridge.SaveGame();record.Saved=saved;SaveState();if(!saved)throw new IOException("The correction is in AGS, but AGS did not finish saving the project. Save from AGS or use Undo in Spellchecker. The backup is retained.");
 }
 public void Undo(){EnsureGame();Bridge.CheckCapabilities();var record=State.History.LastOrDefault(x=>!x.Undone);if(record==null)throw new IOException("No plugin edits to undo.");var target=Bridge.Resolve(record.Target);if(target==null)throw new IOException("The edited item no longer exists.");string current=target.Read();if(Hash(current)==record.BeforeHash){record.Undone=true;SaveState();return;}if(Hash(current)!=record.AfterHash)throw new IOException("The edited text changed again in AGS. Undo stopped to protect newer work. Your original text is backed up in "+Path.Combine(Store,record.Id));string before=File.ReadAllText(Path.Combine(Store,record.Id,"before.txt"),Encoding.UTF8);if(Hash(before)!=record.BeforeHash)throw new IOException("Backup verification failed.");target.Write(before);if(!Bridge.SaveGame())throw new IOException("Undo was applied in AGS but the project save did not complete. Save from AGS. The undo backup remains available.");record.Undone=true;SaveState();}
}
}





