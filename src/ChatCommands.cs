using HarmonyLib;
using System;
using System.Linq;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        public bool HandleChatCommand(string text)
        {
            var parts = text.Trim().Split(new[] { ' ' }, 2);
            string command = parts[0].ToLowerInvariant(), arg = parts.Length > 1 ? parts[1].Trim() : "";
            string[] supported = { "/creagilda", "/invitagilda", "/escigilda", "/gildatag", "/condividimappa", "/saldo", "/dai", "/ritira", "/deposita", "/quest", "/completaquest", "/mmomod" };
            if (!supported.Contains(command)) return false;
            if (command == "/mmomod") { ToggleMenu(); return true; }
            if (!ready) { Feedback(L("Attendi la sincronizzazione con il server della mod.")); return true; }
            switch (command)
            {
                case "/creagilda": Cmd("createGuild", text: arg); break;
                case "/invitagilda": Cmd("inviteGuild", arg); break;
                case "/escigilda": Cmd("leaveGuild"); break;
                case "/gildatag": Cmd("tag", text: arg); break;
                case "/condividimappa": ShareMap(); break;
                case "/saldo": Feedback(L("Saldo: {0} oro.", snapshot.self.gold)); break;
                case "/ritira": Withdraw(arg); break;
                case "/deposita": Deposit(); break;
                case "/dai":
                    int last = arg.LastIndexOf(' '), amount;
                    if (last <= 0 || !int.TryParse(arg.Substring(last + 1), out amount)) Feedback(L("Uso: /dai nome giocatore quantità"));
                    else Cmd("transfer", arg.Substring(0, last).Trim(), amount: amount);
                    break;
                case "/quest": tab = 2; sub = 0; if (!MenuOpen) ToggleMenu(); else uiDirty = true; break;
                case "/completaquest":
                    var q = snapshot.self.quests.FirstOrDefault(x => !x.claimed && x.progress >= x.target);
                    if (q == null) Feedback(L("Nessuna missione pronta da riscuotere.")); else Cmd("claim", q.id);
                    break;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Chat), "InputText")]
    internal static class ChatCommandPatch
    {
        [HarmonyPrefix] static bool Prefix(Chat __instance)
        {
            var input = AccessTools.Field(__instance.GetType(), "m_input")?.GetValue(__instance);
            var property = input?.GetType().GetProperty("text"); string text = property?.GetValue(input, null) as string;
            if (string.IsNullOrEmpty(text) || MMOMODPlugin.Instance == null || !MMOMODPlugin.Instance.HandleChatCommand(text)) return true;
            property.SetValue(input, "", null); return false;
        }
    }
}
