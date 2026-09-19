using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace ValheimMMOMOD
{
    [BepInPlugin(PluginGUID, "ValheimMMOMOD", PluginVersion)]
    public partial class MMOMODPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "antonio.valheim.mmomod", PluginVersion = "2.5.0";
        public static MMOMODPlugin Instance;
        public bool MenuOpen;
        private Harmony harmony;
        private ZRoutedRpc router;
        private GameService service;
        private string serverFile, journalFile, character = "";
        private Snapshot snapshot = new Snapshot();
        private bool ready, grantsPending;
        private float nextLogin, nextRefresh, nextShare, progressAt;
        private readonly Dictionary<string, long> sessions = new Dictionary<string, long>();
        private readonly Dictionary<long, float> mapCooldown = new Dictionary<long, float>();
        private readonly Dictionary<string, int> progress = new Dictionary<string, int>();
        private ConfigEntry<bool> economyEnabled, questEnabled, autoShare, showTag;
        private ConfigEntry<int> maxMembers, killGold, levelGold, dailyGold, shareInterval;
        private ConfigEntry<float> killMultiplier;
        private ConfigEntry<string> guildTag;
        private string feedback = "";
        private float feedbackUntil;
        private const string RequestRpc = "MMO2_Request", StateRpc = "MMO2_State", MapRpc = "MMO2_Map";
        private LocalJournal journal = new LocalJournal();
        [Serializable] public class LocalJournal { public List<CoinGrant> receipts = new List<CoinGrant>(); public List<Command> pending = new List<Command>(); }
        [Serializable] private class LegacyData { public string playerGold = "", guildName = "", guildTag = "", isInGuild = ""; }

        private void Awake()
        {
            Instance = this;
            villageEnabled = Config.Bind("Village", "Enabled", true, "Crea un piccolo borgo mercantile nei Prati.");
            wandererEnabled = Config.Bind("Wanderer", "Enabled", true, "Abilita il viandante e le sue due guardie.");
            economyEnabled = Config.Bind("Economy", "Enabled", true, "Abilita l'economia sul server.");
            killGold = Config.Bind("Economy", "GoldBaseKill", 10, "Oro per nemico.");
            killMultiplier = Config.Bind("Economy", "GoldDifficultyMultiplier", 1.5f, "Moltiplicatore difficoltà.");
            levelGold = Config.Bind("Economy", "GoldLevelUp", 25, "Oro per livello abilità.");
            questEnabled = Config.Bind("DailyQuest", "Enabled", true, "Abilita missioni giornaliere e settimanali.");
            dailyGold = Config.Bind("DailyQuest", "GoldReward", 100, "Ricompensa giornaliera, settimanale x5.");
            maxMembers = Config.Bind("General", "MaxGuildMembers", 20, "Massimo membri.");
            autoShare = Config.Bind("General", "AutoMapShare", false, "Condividi automaticamente la mappa con la gilda.");
            shareInterval = Config.Bind("General", "AutoMapShareInterval", 60, "Intervallo condivisione.");
            showTag = Config.Bind("General", "ShowGuildTag", true, "Mostra il tag sui membri.");
            guildTag = Config.Bind("General", "GuildTag", "", "Tag sincronizzato con la gilda.");
            var probe = new WorldStore(); probe.players.Add(new MemberRecord { id = "probe", name = "probe", gold = 123 });
            if (MMOJson.FromJson<WorldStore>(MMOJson.ToJson(probe)).players[0].gold != 123) throw new InvalidOperationException("Serializzazione dati non valida.");
            Logger.LogInfo("Verifica serializzazione: saldo e liste riletti correttamente.");
            harmony = new Harmony(PluginGUID); harmony.PatchAll();
            Logger.LogInfo("ValheimMMOMOD " + PluginVersion + " - UI nativa e servizi attivi.");
        }
        private void Update()
        {
            RefreshLanguage();
            if (TickMainGuide()) return;
            HandlePanelNavigation();
            if (ZRoutedRpc.instance != null && router != ZRoutedRpc.instance)
            {
                router = ZRoutedRpc.instance; ResetCaravan(); service = null; sessions.Clear(); mapCooldown.Clear();
                router.Register<string>(RequestRpc, ReceiveRequest); router.Register<string>(StateRpc, ReceiveState); router.Register<ZPackage>(MapRpc, ReceiveMap);
                Logger.LogInfo("RPC v2 registrati.");
            }
            var player = Player.m_localPlayer;
            string current = player != null ? player.GetPlayerID().ToString() : "";
            if (current != character)
            {
                if (character != "") SaveJournal();
                character = current; ready = false; nextLogin = nextShare = 0; snapshot = new Snapshot(); progress.Clear();
                MenuOpen = false; localShopOpen = false; localShopActor = null; mapRequested = false; mapDestination = null; journal = new LocalJournal(); if (character != "") LoadJournal(); DestroyNativeUI();
            }
            if (character != "" && router != null)
            {
                if (!ready && Time.unscaledTime >= nextLogin)
                {
                    nextLogin = Time.unscaledTime + 5; var legacy = ReadLegacy(player.GetPlayerName());
                    Send(new Command { action = "hello", amount = legacy == null ? 0 : ParseGold(legacy.playerGold), text = legacy == null ? "" : MMOJson.ToJson(legacy) }, false);
                }
                if (ready && Time.unscaledTime >= progressAt)
                {
                    progressAt = Time.unscaledTime + 2; var pending = progress.ToArray(); progress.Clear();
                    foreach (var e in pending) Send(new Command { action = "progress", text = e.Key, amount = e.Value });
                }
                if (ready && MenuOpen && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 8; Send(new Command { action = "refresh" }, false); }
                if (ready && autoShare.Value && snapshot.guild != null && Time.unscaledTime >= nextShare) ShareMap(false);
                ProcessGrants();
            }
            TickCaravan();
            TickVillage();
            UpdateNativeUI();
        }
        private void LateUpdate() { if (MenuOpen) ShowModCursor(); }
        private void ToggleMenu()
        {
            if (MenuOpen) { CloseNativeMenu(); return; }
            PrepareModPanel();
        }
        private void CloseNativeMenu()
        {
            ReleaseModPanel();
        }
        private void OnApplicationQuit() { SaveJournal(); SaveServer(); }
        private void OnDestroy() { SaveJournal(); SaveServer(); DestroyNativeUI(); harmony?.UnpatchSelf(); Instance = null; }
        private bool Server => ZNet.instance != null && ZNet.instance.IsServer();
        private long ServerPeer => Server ? ZNet.GetUID() : ZNet.instance?.GetServerPeer()?.m_uid ?? 0;
        private void LoadJournal() { journalFile = Path.Combine(Paths.ConfigPath, "MMOMOD", "journal_" + ZNet.instance.GetWorldUID() + "_" + character + ".json"); try { if (File.Exists(journalFile)) journal = MMOJson.FromJson<LocalJournal>(File.ReadAllText(journalFile)) ?? new LocalJournal(); } catch (Exception ex) { Logger.LogError("Registro locale: " + ex.Message); } }
        private void SaveJournal() { if (character != "" && journalFile != null) AtomicWrite(journalFile, MMOJson.ToJson(journal, true)); }
        private void AtomicWrite(string path, string data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path + ".tmp", data);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
        }
        private static int ParseGold(string value) { int n; return int.TryParse(value, out n) ? Math.Max(0, n) : 0; }
        private LegacyData ReadLegacy(string name)
        {
            try { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "_"); string path = Path.Combine(Paths.ConfigPath, "MMOMOD", "mmomod_" + name + ".json"); return File.Exists(path) ? MMOJson.FromJson<LegacyData>(File.ReadAllText(path)) : null; } catch { return null; }
        }
        private void EnsureServer()
        {
            if (service != null) return;
            serverFile = Path.Combine(Paths.ConfigPath, "MMOMOD", "world_" + ZNet.instance.GetWorldUID() + "_v2.json");
            var store = File.Exists(serverFile) ? MMOJson.FromJson<WorldStore>(File.ReadAllText(serverFile)) : null;
            service = new GameService(store) { Economy = economyEnabled.Value, Quests = questEnabled.Value, MaxMembers = Math.Max(1, maxMembers.Value), BaseKill = Mathf.Clamp(killGold.Value, 0, 100000), LevelReward = Mathf.Clamp(levelGold.Value, 0, 100000), DailyReward = Mathf.Clamp(dailyGold.Value, 0, 1000000), KillMultiplier = Mathf.Clamp(killMultiplier.Value, 0, 100) };
        }
        private void SaveServer() { if (Server && service != null && serverFile != null) AtomicWrite(serverFile, MMOJson.ToJson(service.Data, true)); }
        private void Send(Command command, bool durable = true)
        {
            if (router == null || ServerPeer == 0) { Feedback(L("Entra in un mondo con la mod attiva sul server.")); return; }
            if (command.action != "hello" && !ready) { Feedback(L("Attendi la sincronizzazione. La mod deve essere installata anche sul server.")); return; }
            if (string.IsNullOrEmpty(command.id)) command.id = Guid.NewGuid().ToString("N");
            if (durable && !journal.pending.Any(c => c.id == command.id)) { journal.pending.Add(command); SaveJournal(); }
            string json = MMOJson.ToJson(command);
            if (Server) ReceiveRequest(ZNet.GetUID(), json); else router.InvokeRoutedRPC(ServerPeer, RequestRpc, json);
        }
        private string ResolveActor(long sender, out string name)
        {
            if (Server && sender == ZNet.GetUID() && Player.m_localPlayer != null) { name = Player.m_localPlayer.GetPlayerName(); return Player.m_localPlayer.GetPlayerID().ToString(); }
            var peer = ZNet.instance.GetPeer(sender); name = peer?.m_playerName;
            return peer != null && peer.m_playerID != 0 ? peer.m_playerID.ToString() : null;
        }
        private void ReceiveRequest(long sender, string json)
        {
            if (!Server || json == null || json.Length > 16000) return;
            try
            {
                EnsureServer(); string name; string id = ResolveActor(sender, out name); if (id == null) return;
                var c = MMOJson.FromJson<Command>(json); if (c == null || string.IsNullOrEmpty(c.id) || c.id.Length > 64) return;
                var p = service.Player(id);
                if (c.action == "hello")
                {
                    bool first = p == null; p = service.Join(id, name, c.amount, DateTime.UtcNow); sessions[id] = sender;
                    if (first && !string.IsNullOrEmpty(c.text))
                    {
                        var legacy = MMOJson.FromJson<LegacyData>(c.text);
                        if (legacy != null && legacy.isInGuild == "True" && !string.IsNullOrWhiteSpace(legacy.guildName) && !service.Data.guilds.Any(g => g.name == legacy.guildName))
                        { service.Execute(id, new Command { action = "createGuild", text = legacy.guildName }, DateTime.UtcNow); service.Execute(id, new Command { action = "tag", text = legacy.guildTag ?? "" }, DateTime.UtcNow); }
                    }
                    foreach (var other in service.Data.players.Where(x => x.id != id && IsOnline(x.id)))
                    { if (!p.recent.Contains(other.id)) p.recent.Insert(0, other.id); if (!other.recent.Contains(id)) other.recent.Insert(0, id); if (p.recent.Count > 100) p.recent.RemoveAt(100); }
                }
                if (p == null) return; sessions[id] = sender;
                bool duplicate = p.processed.Contains(c.id);
                string vendor = VillageCatalog.Find(c.target)?.Shop ?? "wanderer";
                string message = duplicate || c.action == "hello" ? "" : service.Execute(id, c, DateTime.UtcNow, c.action == "trade" && NearMerchant(sender, vendor), vendor);
                if (!duplicate) { p.processed.Add(c.id); if (p.processed.Count > 4096) p.processed.RemoveAt(0); }
                SaveServer();
                foreach (var member in service.Data.players.Where(x => IsOnline(x.id)).ToList()) PushState(member, member.id == id ? message : "");
            }
            catch (Exception ex) { Logger.LogError("Richiesta mod: " + ex); }
        }
        private bool IsOnline(string id) { long peer; return sessions.TryGetValue(id, out peer) && ((Server && peer == ZNet.GetUID() && Player.m_localPlayer != null) || ZNet.instance.GetPeer(peer) != null); }
        private PublicMember Public(MemberRecord p) { return new PublicMember { id = p.id, name = p.name, role = p.role, online = IsOnline(p.id) }; }
        private void PushState(MemberRecord p, string message)
        {
            var s = new Snapshot { villageStatus = villageStatus, village = villageEnabled.Value ? service.Data.village : null, caravan = wandererEnabled.Value ? service.Data.caravan : null, self = p, guild = service.Guild(p), message = message, people = service.Data.players.Select(Public).ToList(), economy = service.Economy, quests = service.Quests };
            s.members = p.guild == "" ? new List<PublicMember>() : service.Data.players.Where(x => x.guild == p.guild).Select(Public).ToList();
            s.invitations = service.Data.guilds.Where(g => p.invitations.Contains(g.id)).ToList();
            string json = MMOJson.ToJson(s);
            if (sessions[p.id] == ZNet.GetUID()) ReceiveState(sessions[p.id], json); else router.InvokeRoutedRPC(sessions[p.id], StateRpc, json);
        }
        private void ReceiveState(long sender, string json)
        {
            if (sender != ServerPeer || character == "" || json == null || json.Length > 2000000) return;
            var next = MMOJson.FromJson<Snapshot>(json); if (next?.self == null || next.self.id != character) return;
            bool initial = !ready; snapshot = next; ready = true;
            journal.pending.RemoveAll(c => snapshot.self.processed.Contains(c.id)); guildTag.Value = snapshot.guild?.tag ?? "";
            if (!string.IsNullOrEmpty(next.message)) Feedback(next.message);
            SaveJournal(); uiDirty = true; grantsPending = true;
            if (initial) foreach (var c in journal.pending.ToArray()) Send(c);
        }
        private void ProcessGrants()
        {
            if (!grantsPending || !ready || Player.m_localPlayer == null) return; grantsPending = false;
            foreach (var resource in snapshot.self.resourceGrants.ToArray())
            {
                var receipt = journal.receipts.Find(r => r.id == resource.id);
                if (receipt == null) { receipt = new CoinGrant { id = resource.id, amount = GiveResource(resource) }; journal.receipts.Add(receipt); SaveJournal(); }
                Send(new Command { action = "tradeAck", target = resource.id, amount = receipt.amount });
            }
            foreach (var grant in snapshot.self.grants.ToArray())
            {
                var receipt = journal.receipts.Find(r => r.id == grant.id);
                if (receipt == null) { receipt = new CoinGrant { id = grant.id, amount = GiveCoins(grant.amount) }; journal.receipts.Add(receipt); SaveJournal(); }
                Send(new Command { action = "withdrawAck", target = grant.id, amount = receipt.amount });
            }
        }
        private GameObject Coins => ObjectDB.instance?.GetItemPrefab("Coins");
        private int CoinCount => Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory().GetAllItems().Where(i => i.m_shared.m_name == "$item_coins").Sum(i => i.m_stack) : 0;
        private int GiveCoins(int amount)
        {
            var prefab = Coins; var inventory = Player.m_localPlayer?.GetInventory(); if (prefab == null || inventory == null) return 0;
            var coin = prefab.GetComponent<ItemDrop>().m_itemData.Clone(); coin.m_worldLevel = (byte)Game.m_worldLevel;
            if (!inventory.CanAddItem(coin, amount)) return 0;
            int before = CoinCount;
            for (int left = amount; left > 0;) { int stack = Math.Min(left, coin.m_shared.m_maxStackSize); if (!inventory.AddItem(prefab, stack)) break; left -= stack; }
            return Math.Max(0, CoinCount - before);
        }
        private void Deposit()
        {
            if (!ready || Player.m_localPlayer == null || !snapshot.economy) return;
            int amount = CoinCount; if (amount == 0) { Feedback(L("Non hai monete nell'inventario.")); return; }
            if ((long)snapshot.self.gold + amount > int.MaxValue) { Feedback(L("Saldo massimo raggiunto.")); return; }
            var inventory = Player.m_localPlayer.GetInventory();
            foreach (var item in inventory.GetAllItems().Where(i => i.m_shared.m_name == "$item_coins").ToList()) inventory.RemoveItem(item);
            Send(new Command { action = "deposit", amount = amount });
        }
        private void Withdraw(string text) { int n; if (!int.TryParse(text, out n) || n <= 0) { Feedback(L("Inserisci un importo intero positivo.")); return; } Send(new Command { action = "withdraw", amount = n }); }
        public void Progress(string type, int amount)
        {
            if (!ready || amount <= 0) return;
            if (type == "kill") { Send(new Command { action = "progress", text = type, amount = amount }); return; }
            int old; progress.TryGetValue(type, out old); progress[type] = Math.Min(10000, old + amount);
        }
        private void Feedback(string text) { text = LocalizedMessage(text); feedback = text; feedbackUntil = Time.unscaledTime + 8; Logger.LogInfo(text); uiDirty = true; if (!MenuOpen && MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text); }
        private void ShareMap(bool notify = true)
        {
            if (!ready || snapshot.guild == null || Minimap.instance == null) { Feedback(L("Entra in una gilda per condividere la mappa.")); return; }
            if (Time.unscaledTime < nextShare) { Feedback(L("Attendi prima di condividere di nuovo.")); return; }
            nextShare = Time.unscaledTime + Math.Max(30, shareInterval.Value);
            byte[] raw = Minimap.instance.GetSharedMapData(null);
            using (var output = new MemoryStream())
            { using (var zip = new DeflateStream(output, CompressionMode.Compress, true)) zip.Write(raw, 0, raw.Length); var pkg = new ZPackage(); pkg.Write(output.ToArray()); if (Server) ReceiveMap(ZNet.GetUID(), pkg); else router.InvokeRoutedRPC(ServerPeer, MapRpc, pkg); }
            if (notify) Feedback(L("Mappa inviata ai membri della gilda collegati."));
        }
        private void ReceiveMap(long sender, ZPackage pkg)
        {
            try
            {
                byte[] data = pkg.ReadByteArray(); if (data == null || data.Length > 2000000) return;
                if (Server)
                {
                    EnsureServer(); string name; string id = ResolveActor(sender, out name); var p = service.Player(id); if (p == null || p.guild == "") return;
                    float last; if (mapCooldown.TryGetValue(sender, out last) && Time.unscaledTime - last < 10) return; mapCooldown[sender] = Time.unscaledTime;
                    foreach (var member in service.Data.players.Where(x => x.guild == p.guild && x.id != id && IsOnline(x.id)).ToList())
                    { if (sessions[member.id] == ZNet.GetUID()) ApplyMap(data); else { var outgoing = new ZPackage(); outgoing.Write(data); router.InvokeRoutedRPC(sessions[member.id], MapRpc, outgoing); } }
                }
                else if (sender == ServerPeer && ready && snapshot.guild != null) ApplyMap(data);
            }
            catch (Exception ex) { Logger.LogWarning("Condivisione mappa: " + ex.Message); }
        }
        private void ApplyMap(byte[] data)
        {
            if (Minimap.instance == null) return;
            using (var input = new MemoryStream(data)) using (var zip = new DeflateStream(input, CompressionMode.Decompress)) using (var output = new MemoryStream())
            { byte[] buffer = new byte[8192]; int read; while ((read = zip.Read(buffer, 0, buffer.Length)) > 0) { if (output.Length + read > 16000000) return; output.Write(buffer, 0, read); } Minimap.instance.AddSharedMapData(output.ToArray()); }
            Feedback(L("Mappa della gilda aggiornata."));
        }
    }
    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class InputPatch { [HarmonyPostfix] static void Postfix(ref bool __result) { if (MMOMODPlugin.Instance?.BlocksNativePanels == true) __result = false; } }
    [HarmonyPatch(typeof(Character), "OnDeath")]
    internal static class KillPatch
    {
        [HarmonyPrefix] static void Prefix(Character __instance)
        { if (__instance.IsPlayer() || __instance.GetComponent<CaravanActor>() != null || Player.m_localPlayer == null) return; var hit = AccessTools.Field(typeof(Character), "m_lastHit")?.GetValue(__instance) as HitData; if (hit?.GetAttacker() == Player.m_localPlayer) MMOMODPlugin.Instance?.Progress("kill", Math.Max(1, __instance.GetLevel())); }
    }
    [HarmonyPatch(typeof(Skills), "RaiseSkill")]
    internal static class LevelPatch
    {
        [HarmonyPrefix] static void Prefix(Skills __instance, Skills.SkillType skillType, out int __state) { __state = Mathf.FloorToInt(__instance.GetSkillLevel(skillType)); }
        [HarmonyPostfix] static void Postfix(Skills __instance, Skills.SkillType skillType, int __state) { if (Player.m_localPlayer != null && Player.m_localPlayer.GetSkills() == __instance) MMOMODPlugin.Instance?.Progress("level", Mathf.FloorToInt(__instance.GetSkillLevel(skillType)) - __state); }
    }
    [HarmonyPatch(typeof(Minimap), "Explore", new Type[] { typeof(int), typeof(int) })]
    internal static class ExplorePatch { [HarmonyPostfix] static void Postfix(bool __result) { if (__result) MMOMODPlugin.Instance?.Progress("explore", 1); } }
    [HarmonyPatch(typeof(Humanoid), "Pickup")]
    internal static class GatherPatch
    {
        [HarmonyPrefix] static void Prefix(Humanoid __instance, GameObject go, out int __state)
        { __state = 0; if (__instance != Player.m_localPlayer || go == null) return; var item = go.GetComponent<ItemDrop>()?.m_itemData; if (item?.m_shared == null) return; string name = item.m_shared.m_name; if (name.Contains("wood") || name.Contains("stone") || name.Contains("ore")) __state = item.m_stack; }
        [HarmonyPostfix] static void Postfix(bool __result, int __state) { if (__result && __state > 0) MMOMODPlugin.Instance?.Progress("gather", __state); }
    }
}

