using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using AGSProof;

namespace AGSSpellchecker {
public sealed class CheckingLanguage {public string name;public string code;public string longCode;public override string ToString(){return name+" ("+longCode+")";}}
public sealed class LanguageDetection {public string Code,Name;public double Confidence;public bool Reliable,Mixed;public int Version=1;public string Label {get{return Mixed?"Mixed / uncertain":Reliable?Name:"Uncertain";}}}
public sealed class ProofResult {public List<Issue> Issues=new List<Issue>();public LanguageDetection Detection;}
public sealed class OfflineProof : IDisposable {
 List<CheckingLanguage> languageCatalog;readonly object gate=new object();Process process;string activeLanguage,endpoint,serverConfig;bool disposed;string lastError="";
 public string RuntimeRoot {get;private set;}
 readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=8000000};
 public OfflineProof(){string beside=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location),"Runtime");if(File.Exists(Path.Combine(beside,"LanguageTool","languagetool-server.jar")))RuntimeRoot=beside;else foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})using(var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var key=baseKey.OpenSubKey(@"SOFTWARE\AGS Spellchecker")){string root=key==null?null:key.GetValue("InstallPath") as string;if(root!=null&&File.Exists(Path.Combine(root,"Runtime","LanguageTool","languagetool-server.jar"))){RuntimeRoot=Path.Combine(root,"Runtime");break;}}}
 public List<CheckingLanguage> Languages(){if(languageCatalog!=null)return languageCatalog;if(RuntimeRoot==null)return new List<CheckingLanguage>{new CheckingLanguage{name="English (US)",code="en",longCode="en-US"}};string path=Path.Combine(RuntimeRoot,"languages.json");languageCatalog=json.Deserialize<List<CheckingLanguage>>(File.ReadAllText(path));return languageCatalog;}
 public List<Issue> Check(string text,string language,IEnumerable<string> words){return CheckDetailed(text,language,words).Issues;}
 public ProofResult CheckDetailed(string text,string language,IEnumerable<string> words){lock(gate){if(process!=null&&activeLanguage!=language){StopProcess();try{process.WaitForExit(3000);}catch(InvalidOperationException){}process.Dispose();process=null;}activeLanguage=language;EnsureStarted();if(text.Length>20000)throw new IOException("This text exceeds the offline checker's 20,000-character limit.");string masked=Proof.Mask(text).Replace('[','\n');string body="language="+Uri.EscapeDataString(language)+"&text="+Uri.EscapeDataString(masked)+"&enabledOnly=false";var response=json.Deserialize<CheckResponse>(Request("check",body,45000));var list=new List<Issue>();var dictionary=new HashSet<string>(words.Select(NormalizeWord),StringComparer.OrdinalIgnoreCase);foreach(var match in response.matches??new List<Match>()){int start=match.offset,length=match.length;if(start<0||length<0||start+length>text.Length)continue;if(length>0&&text.Substring(start,length)!=masked.Substring(start,length))continue;string kind=match.rule!=null&&match.rule.issueType=="misspelling"?"Spelling":match.rule!=null&&match.rule.category!=null&&match.rule.category.id=="PUNCTUATION"?"Punctuation":match.rule!=null&&match.rule.category!=null&&match.rule.category.id=="CASING"?"Capitalization":"Grammar / style";bool eligible=length>0&&System.Text.RegularExpressions.Regex.IsMatch(text.Substring(start,length),@"\p{L}");if(AcceptedIssue(text,start,length,dictionary))continue;list.Add(new Issue{Start=start,Length=length,Kind=kind,DictionaryEligible=eligible,RuleId=match.rule==null?kind:match.rule.id,Message=match.message,Suggestions=(match.replacements??new List<Replacement>()).Select(x=>x.value).Take(8).ToList()});}return new ProofResult{Issues=list.OrderBy(x=>x.Start).ToList(),Detection=null};}}
 public static string BaseLanguage(string code){return string.IsNullOrEmpty(code)?"":code.Split('-')[0].ToLowerInvariant();}
 public static bool OtherLanguage(LanguageDetection detection,string selected){return detection!=null&&detection.Version==1&&detection.Reliable&&BaseLanguage(detection.Code)!=BaseLanguage(selected);}
 LanguageDetection Detect(string text,string selected,CheckResponse response){var result=Classify(text,response.language==null?null:response.language.detectedLanguage);if(!result.Reliable)return result;
  var parts=System.Text.RegularExpressions.Regex.Split(text,@"(?<=[.!?。！？])\s+|[\r\n]+").Where(x=>System.Text.RegularExpressions.Regex.IsMatch(x,@"\p{L}")).ToArray();
  if(parts.Length>6){result.Reliable=false;result.Mixed=true;return result;}
  if(parts.Length>1){foreach(var part in parts){string body="language="+Uri.EscapeDataString(selected)+"&text="+Uri.EscapeDataString(part)+"&enabledOnly=true&enabledRules=WHITESPACE_RULE";var single=json.Deserialize<CheckResponse>(Request("check",body,45000));var d=Classify(part,single.language==null?null:single.language.detectedLanguage);if(!d.Reliable||BaseLanguage(d.Code)!=BaseLanguage(result.Code)){result.Reliable=false;result.Mixed=true;break;}}}return result;
 }
 LanguageDetection Classify(string text,DetectedLanguage detected){var d=new LanguageDetection();if(detected==null)return d;d.Code=detected.code;d.Name=detected.name;d.Confidence=detected.confidence;int letters=System.Text.RegularExpressions.Regex.Matches(text,@"\p{L}").Count;int words=System.Text.RegularExpressions.Regex.Matches(text,@"\p{L}+").Count;int cjk=System.Text.RegularExpressions.Regex.Matches(text,@"[\u3040-\u30ff\u3400-\u9fff\uac00-\ud7af]").Count;d.Reliable=detected.confidence>=0.98&&((letters>=20&&words>=4)||cjk>=12)&&Languages().Any(x=>BaseLanguage(x.code)==BaseLanguage(d.Code));return d;}
 public static bool AcceptedIssue(string text,int start,int length,IEnumerable<string> words){foreach(string word in words){string normalized=NormalizeWord(word);if(normalized.Length==0)continue;string pattern=@"(?<![\p{L}\p{M}\p{N}_])"+System.Text.RegularExpressions.Regex.Escape(normalized).Replace("'","['\u2019]").Replace(@"\ ",@"\s+")+@"(?![\p{L}\p{M}\p{N}_])";foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,pattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant)){if(start>=match.Index&&start+length<=match.Index+match.Length)return true;}}return false;}
 public static string NormalizeWord(string word){return (word??"").Normalize(NormalizationForm.FormC).Trim().Trim('\'', '"', '‘', '’', '“', '”').Replace('’','\'').ToLowerInvariant();}
 public static bool KnownWord(string word,IEnumerable<string> words){string normalized=NormalizeWord(word);return words.Any(x=>NormalizeWord(x)==normalized||(normalized.EndsWith("'s",StringComparison.Ordinal)&&NormalizeWord(x)==normalized.Substring(0,normalized.Length-2)));}
 void EnsureStarted(){if(disposed)throw new ObjectDisposedException("Offline checker");if(process!=null&&!process.HasExited)return;if(RuntimeRoot==null)throw new IOException("The offline language engine was not found. Install AGS Spellchecker using Setup, or place the portable Runtime folder beside the plugin DLL.");string java=Path.Combine(RuntimeRoot,"Java","bin","java.exe"),engine=Path.Combine(RuntimeRoot,"LanguageTool");if(!File.Exists(java))throw new IOException("The bundled Java runtime is missing. Reinstall AGS Spellchecker.");int port;var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();endpoint="http://127.0.0.1:"+port+"/v2/";lastError="";if(process!=null)process.Dispose();serverConfig=Path.Combine(Path.GetTempPath(),"ags-spellchecker-"+Guid.NewGuid().ToString("N")+".properties");File.WriteAllText(serverConfig,"pipelineCaching=true\npipelineExpireTimeInSeconds=600\nmaxPipelinePoolSize=2\nmaxCheckThreads=1\nmaxTextCheckerThreads=1\ncacheSize=1000\n",new UTF8Encoding(false));process=new Process{StartInfo=new ProcessStartInfo{FileName=java,Arguments="-Xms64m -Xmx2048m -Djava.net.preferIPv4Stack=true -XX:ActiveProcessorCount=2 -cp languagetool-server.jar org.languagetool.server.HTTPServer --config \""+serverConfig+"\" --port "+port,WorkingDirectory=engine,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true}};process.OutputDataReceived+=(s,e)=>{if(e.Data!=null){lastError=(lastError+Environment.NewLine+e.Data);if(lastError.Length>6000)lastError=lastError.Substring(lastError.Length-6000);}};process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null){lastError=(lastError+Environment.NewLine+e.Data);if(lastError.Length>6000)lastError=lastError.Substring(lastError.Length-6000);}};process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<60000){if(disposed)throw new ObjectDisposedException("Offline checker");if(process.HasExited)throw new IOException("The offline engine could not start: "+lastError);try{Request("languages",null,700);ClearConfig();return;}catch(WebException){}Thread.Sleep(150);}StopProcess();throw new IOException("The offline engine took too long to start. Try scanning again.");}
 string Request(string route,string body,int timeout){var req=(HttpWebRequest)WebRequest.Create(endpoint+route);req.Proxy=null;req.AllowAutoRedirect=false;req.Timeout=timeout;req.ReadWriteTimeout=timeout;req.KeepAlive=false;if(body!=null){req.Method="POST";req.ContentType="application/x-www-form-urlencoded; charset=UTF-8";byte[] bytes=Encoding.UTF8.GetBytes(body);req.ContentLength=bytes.Length;using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);}using(var response=req.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return reader.ReadToEnd();}
 void ClearConfig(){try{if(serverConfig!=null)File.Delete(serverConfig);}catch(IOException){}catch(UnauthorizedAccessException){}serverConfig=null;}
 void StopProcess(){try{if(process!=null&&!process.HasExited)process.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}ClearConfig(); }
 public void Dispose(){disposed=true;StopProcess();}
 public sealed class CheckResponse {public List<Match> matches;public ResponseLanguage language;} public sealed class ResponseLanguage {public DetectedLanguage detectedLanguage;} public sealed class DetectedLanguage {public string name,code;public double confidence;}
 public sealed class Match {public string message;public int offset,length;public List<Replacement> replacements;public Rule rule;}
 public sealed class Replacement {public string value;}
 public sealed class Rule {public string id,issueType;public Category category;}
 public sealed class Category {public string id,name;}
}
}










