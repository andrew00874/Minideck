using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace MiniDeck {
 static class L10n {
#if ENGLISH
  public const bool English=true;
  static readonly Dictionary<string,string> Translations=Load();
  static Dictionary<string,string> Load() {
   using(Stream stream=typeof(L10n).Assembly.GetManifestResourceStream("MiniDeck.English"))
   using(StreamReader reader=new StreamReader(stream)) return new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(reader.ReadToEnd());
  }
  public static string T(string text) {string result;if(!Translations.TryGetValue(text,out result))throw new InvalidOperationException("Missing English translation: "+text);return result;}
#else
  public const bool English=false;
  public static string T(string text) {return text;}
#endif
 }
}
