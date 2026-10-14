using System;using System.Linq;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace AGSProof {
public class Issue {public int Start,Length;public bool DictionaryEligible,Ignored;public string RuleId;public Issue Copy(){return (Issue)MemberwiseClone();}public string Message,Kind;public List<string> Suggestions=new List<string>();public override string ToString(){return (Ignored?"Ignored — ":"")+Kind+": "+Message;}}
public static class Proof {
public static string Mask(string text){return Regex.Replace(text,@"&\d+|%(?:\d+\$)?[-+0 #]*\d*(?:\.\d+)?[a-zA-Z%]|@[A-Za-z_][\w]*@|\\[A-Za-z]",m=>new string(' ',m.Length));}
}
}


