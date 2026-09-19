using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ValheimMMOMOD
{
    // Language-neutral wire messages keep user names and numeric values out of translation.
    public static class ModText
    {
        private const string Prefix = "@MMO_TEXT:";
        private static readonly Dictionary<string, Dictionary<string, string>> catalogs = new Dictionary<string, Dictionary<string, string>>();
        public sealed class Argument { public string kind, value; }
        private sealed class Message { public string key; public Argument[] args; }
        public static Argument Term(string key) => new Argument { kind = "term", value = key };
        public static Argument Item(string prefab) => new Argument { kind = "item", value = prefab };
        public static string Pack(string key, params object[] args)
        {
            return Prefix + JsonConvert.SerializeObject(new Message { key = key, args = args.Select(a => a as Argument ?? new Argument { kind = "literal", value = Convert.ToString(a, CultureInfo.InvariantCulture) }).ToArray() });
        }
        public static IReadOnlyDictionary<string, string> Catalog(string language)
        {
            lock (catalogs)
            {
                Dictionary<string, string> result;
                if (catalogs.TryGetValue(language, out result)) return result;
                using (var stream = typeof(ModText).Assembly.GetManifestResourceStream("MMOMOD.Localization." + language + ".json"))
                {
                    result = stream == null ? new Dictionary<string, string>() : JsonConvert.DeserializeObject<Dictionary<string, string>>(new StreamReader(stream).ReadToEnd());
                    catalogs[language] = result ?? new Dictionary<string, string>();
                    return catalogs[language];
                }
            }
        }
        public static string Translate(string language, string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            string text = key, translated;
            if (language != "Italian")
            {
                if (Catalog(language).TryGetValue(key, out translated) || Catalog("English").TryGetValue(key, out translated)) text = translated;
            }
            if (args.Length == 0) return text;
            try { return string.Format(CultureInfo.InvariantCulture, text, args); }
            catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, key, args); }
        }
        public static string Resolve(string language, string value, Func<string, string> itemName = null)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                try
                {
                    var message = JsonConvert.DeserializeObject<Message>(value.Substring(Prefix.Length));
                    if (message == null || message.key == null || message.args == null || message.args.Any(a => a == null)) return Translate(language, "Messaggio non disponibile.");
                    return Translate(language, message.key, message.args.Select(a => (object)(a.kind == "term" ? Translate(language, a.value) : a.kind == "item" ? (itemName?.Invoke(a.value) ?? a.value) : a.value)).ToArray());
                }
                catch (JsonException) { return Translate(language, "Messaggio non disponibile."); }
            }
            // Older ledger entries remain readable after changing language, without rewriting saves.
            foreach (string prefix in new[] { "Inviato a ", "Ricevuto da " })
                if (value.StartsWith(prefix, StringComparison.Ordinal)) return Translate(language, prefix + "{0}", value.Substring(prefix.Length));
            foreach (string period in new[] { "Giornaliera", "Settimanale" })
                if (value.StartsWith(period + ": ", StringComparison.Ordinal)) return Translate(language, "{0}: {1}", Translate(language, period), Translate(language, value.Substring(period.Length + 2)));
            var purchase = Regex.Match(value, @"^(Viandante|Borgo): (\d+) (.+)$");
            if (purchase.Success)
            {
                var offer = WandererCatalog.Offers.Concat(VillageCatalog.Food).Concat(VillageCatalog.Equipment).FirstOrDefault(o => o.Name == purchase.Groups[3].Value);
                return Translate(language, "{0}: {1} {2}", Translate(language, purchase.Groups[1].Value), purchase.Groups[2].Value,
                    offer != null && itemName != null ? itemName(offer.Prefab) : Translate(language, purchase.Groups[3].Value));
            }
            return Translate(language, value);
        }
    }
}
