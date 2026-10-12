using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;using System.Windows.Controls;
namespace AGSProof {
public class Issue {
 public int Start,Length; public string Message,Kind; public List<string> Suggestions=new List<string>();
 public override string ToString(){return Kind+": "+Message;}
}
public static class Proof {
 public static List<Issue> Rules(string text,bool prose=false){var list=new List<Issue>();string raw=text;text=Mask(text);
  Add(list,text,@"\b([A-Za-z]+)(\s+)\1\b","Repeated word","Grammar",m=>m.Groups[1].Value);
  foreach(Match m in Regex.Matches(text,@"\bi\b"))list.Add(new Issue{Start=m.Index,Length=1,Kind="Grammar",Message="Capitalize the pronoun I",Suggestions=new List<string>{"I"}});
  Add(list,text,@"\b(could|would|should|must|might) of\b","Use 'have' after this verb","Grammar",m=>m.Groups[1].Value+" have");
  Add(list,text,@"\b(he|she|it) (are|were)\b","Check subject–verb agreement","Grammar",m=>m.Groups[1].Value+(m.Groups[2].Value.Equals("are",StringComparison.OrdinalIgnoreCase)?" is":" was"));
  Add(list,text,@"\b(they|we|you) is\b","Check subject–verb agreement","Grammar",m=>m.Groups[1].Value+" are");
  Add(list,text,@"\b(a) ([aeiou][A-Za-z]+)\b","Check a/an (pronunciation can change this)","Grammar",m=>(m.Groups[1].Value=="A"?"An ":"an ")+m.Groups[2].Value);
  Add(list,text,@"\b(dont|doesnt|didnt|isnt|arent|wasnt|werent|couldnt|wouldnt|shouldnt|havent|hasnt|hadnt)\b","Possible missing apostrophe","Punctuation",m=>m.Value.Insert(m.Value.Length-1,"'"));
  Add(list,text,@"\bIm\b","Possible missing apostrophe","Punctuation",m=>"I'm");
  Add(list,text,@"[ \t]{2,}","Repeated spaces","Punctuation",m=>" ");
  Add(list,text,@"[ \t]+([,;:!?])","Space before punctuation","Punctuation",m=>m.Groups[1].Value);
  Add(list,text,@"([,;!?])([A-Za-z])","Missing space after punctuation","Punctuation",m=>m.Groups[1].Value+" "+m.Groups[2].Value);
  Add(list,text,@"\b(I) (is|are)\b","Use 'am' with I","Grammar",m=>"I am");
  Add(list,text,@"\b(he|she|it) (have|do)\b","Check subject–verb agreement","Grammar",m=>m.Groups[1].Value+(m.Groups[2].Value.Equals("have",StringComparison.OrdinalIgnoreCase)?" has":" does"));
  Add(list,text,@"\b(they|we|you) has\b","Use 'have' with this subject","Grammar",m=>m.Groups[1].Value+" have");
  Add(list,text,@"\b(your) (welcome|right|wrong|going|being)\b","Possible your/you're mix-up","Grammar",m=>(char.IsUpper(m.Value[0])?"You're ":"you're ")+m.Groups[2].Value);
  Add(list,text,@"\b(its) (a|an|the|not|been)\b","Possible missing apostrophe in it's","Grammar",m=>(char.IsUpper(m.Value[0])?"It's ":"it's ")+m.Groups[2].Value);
  Add(list,text,@"\b(doesn't|does not|didn't|did not) (has|goes|says|knows)\b","Use the base verb after does/did","Grammar",m=>m.Groups[1].Value+" "+(m.Groups[2].Value.Equals("has",StringComparison.OrdinalIgnoreCase)?"have":m.Groups[2].Value.Equals("goes",StringComparison.OrdinalIgnoreCase)?"go":m.Groups[2].Value.Substring(0,m.Groups[2].Value.Length-1)));
  foreach(Match m in Regex.Matches(text,@"\b(?:ive|im)\b",RegexOptions.IgnoreCase))if(!list.Any(x=>x.Start==m.Index))list.Add(new Issue{Start=m.Index,Length=m.Length,Kind="Punctuation",Message="Possible missing apostrophe",Suggestions=new List<string>{m.Value.Length==3?"I've":"I'm"}});
  Add(list,text,@"(?<!\.)\.\.(?!\.)","Two dots: use a period or an ellipsis","Punctuation",m=>"...");
  Add(list,text,@"[,;:]{2,}","Repeated punctuation","Punctuation",m=>m.Value.Substring(0,1));
  Add(list,text,@"[ \t]+(\.)(?!\.)","Space before a period","Punctuation",m=>".");
  foreach(Match m in Regex.Matches(text,@"[A-Za-z]\.(?=[A-Z][a-z])")){int pos=m.Index+1;list.Add(new Issue{Start=pos,Length=1,Kind="Punctuation",Message="Possible missing space after a period",Suggestions=new List<string>{". "}});}
  // Start-of-sentence capitalization; ellipses may be pauses within dialogue.
  var first=Regex.Match(text,@"^\s*(?:[""“(]*)([a-z])");if(first.Success&&!Regex.IsMatch(raw.Substring(0,first.Groups[1].Index),@"%|@[A-Za-z_]|\\"))Capital(list,first.Groups[1]);
  foreach(Match m in Regex.Matches(text,@"(?<![.])[.!?](?![.])(?:[""”)]*)\s+([a-z])")){string preceding=text.Substring(0,m.Index);if(Regex.IsMatch(preceding,@"\b(Mr|Mrs|Ms|Dr|Prof|St|vs|etc)$",RegexOptions.IgnoreCase))continue;Capital(list,m.Groups[1]);}
  if(prose){var end=Regex.Match(raw,@"([A-Za-z0-9])([""”’)]*)\s*$");if(end.Success){int pos=end.Groups[1].Index+1;list.Add(new Issue{Start=pos,Length=0,Kind="Punctuation",Message="Possible missing sentence-ending punctuation",Suggestions=new List<string>{".","?","!"}});}}
  return list.Where(x=>x.Length==0||raw.Substring(x.Start,x.Length)==text.Substring(x.Start,x.Length)).ToList();
 }
 static void Capital(List<Issue> list,Group g){if(list.Any(x=>x.Start==g.Index&&x.Length==g.Length))return;list.Add(new Issue{Start=g.Index,Length=g.Length,Kind="Capitalization",Message="Capitalize the beginning of the sentence or label",Suggestions=new List<string>{g.Value.ToUpperInvariant()}});}

 static void Add(List<Issue> list,string text,string pattern,string message,string kind,Func<Match,string> replacement){foreach(Match m in Regex.Matches(text,pattern,RegexOptions.IgnoreCase))list.Add(new Issue{Start=m.Index,Length=m.Length,Message=message+" — “"+m.Value+"”",Kind=kind,Suggestions=new List<string>{replacement(m)}});}
 public static string Mask(string text){return Regex.Replace(text,@"&\d+|%(?:\d+\$)?[-+0 #]*\d*(?:\.\d+)?[a-zA-Z%]|@[A-Za-z_][\w]*@|\\[A-Za-z]",m=>new string(' ',m.Length));}
 public static List<Issue> Check(TextBox box,string text,State state,bool prose=false){var list=Rules(text,prose);box.Text=Mask(text);box.UpdateLayout();int pos=0;while(pos<box.Text.Length){int start=box.GetNextSpellingErrorCharacterIndex(pos,System.Windows.Documents.LogicalDirection.Forward);if(start<0)break;int length=box.GetSpellingErrorLength(start);if(length<=0)break;string word=text.Substring(start,length);var error=box.GetSpellingError(start);if(error!=null&&!state.Words.Contains(word.ToLowerInvariant()))list.Add(new Issue{Start=start,Length=length,Kind="Spelling",Message="Check “"+word+"”",Suggestions=error.Suggestions.Take(8).ToList()});pos=start+length;}return list.OrderBy(x=>x.Start).ToList();}
}

}