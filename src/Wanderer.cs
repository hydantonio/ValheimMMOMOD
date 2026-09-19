using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        internal const string WandererPrefab = "MMOMOD_Wanderer", GuardPrefab = "MMOMOD_CaravanGuard";
        private GameObject caravanTemplates;
        private ConfigEntry<bool> wandererEnabled;
        private readonly List<ZDO> wandererZdos = new List<ZDO>(), guardZdos = new List<ZDO>();
        private int wandererScan, guardScan;
        private bool wandererScanned, guardsScanned;
        private float nextCaravan, nextCaravanSave, nextGuardRespawn;
        private Minimap pinMap;
        private Minimap.PinData wandererPin;

        internal void RegisterCaravan(ZNetScene scene)
        {
            ResetCaravan();
            if (caravanTemplates != null) Destroy(caravanTemplates);
            caravanTemplates = new GameObject("MMOMOD_CaravanTemplates");
            caravanTemplates.SetActive(false); DontDestroyOnLoad(caravanTemplates);
            var guard = scene.GetPrefab("Dverger");
            var trader = scene.GetPrefab("DvergerMage") ?? guard;
            if (guard == null || trader == null) { Logger.LogError("Viandante: prefab Dverger non disponibile."); return; }
            RegisterCaravanPrefab(scene, trader, WandererPrefab, true);
            RegisterCaravanPrefab(scene, guard, GuardPrefab, false);
            try { RegisterVillage(scene, trader); }
            catch (Exception ex) { villageStatus = "Errore di caricamento del borgo: " + ex.Message; Logger.LogError("Registrazione villaggio: " + ex); }
            Logger.LogInfo("Viandante: prefab di mercante e guardia registrati.");
        }
        private GameObject RegisterCaravanPrefab(ZNetScene scene, GameObject source, string name, bool merchant)
        {
            // An inactive parent prevents Awake from creating a ZDO for the template.
            var prefab = Instantiate(source, caravanTemplates.transform, false); prefab.name = name;
            var npc = prefab.GetComponent<Humanoid>(); var ai = prefab.GetComponent<MonsterAI>();
            var view = prefab.GetComponent<ZNetView>();
            if (npc == null || ai == null || view == null) throw new InvalidOperationException("Prefab carovana incompleto: " + source.name);
            npc.m_name = merchant ? "$mmomod_wanderer" : "$mmomod_guard";
            npc.m_faction = Character.Faction.Players; npc.m_group = "MMOMOD_Caravan"; npc.m_health = merchant ? 1000 : 500;
            ai.m_aggravatable = false; ai.m_attackPlayerObjects = false; ai.m_enableHuntPlayer = false;
            ai.m_randomMoveRange = merchant ? 28 : 3; ai.m_randomMoveInterval = merchant ? 12 : 5;
            ai.m_alertRange = 20; ai.m_maxChaseDistance = 30; ai.m_avoidWater = true;
            view.m_persistent = true;
            var drops = prefab.GetComponent<CharacterDrop>(); if (drops != null) drops.m_drops = new List<CharacterDrop.Drop>();
            var tame = prefab.GetComponent<Tameable>(); if (tame != null) DestroyImmediate(tame);
            var actor = prefab.AddComponent<CaravanActor>(); actor.IsMerchant = merchant;
            prefab.SetActive(true);
            scene.m_prefabs.Add(prefab);
            var prefabs = (Dictionary<int, GameObject>)AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene);
            prefabs.Add(name.GetStableHashCode(), prefab);
            return prefab;
        }
        private void ResetCaravan()
        {
            ResetVillage();
            if (pinMap != null && wandererPin != null) pinMap.RemovePin(wandererPin);
            wandererPin = null; pinMap = null;
            wandererZdos.Clear(); guardZdos.Clear(); wandererScan = guardScan = 0;
            wandererScanned = guardsScanned = false; nextCaravan = nextCaravanSave = nextGuardRespawn = 0;
        }
        private void TickCaravan()
        {
            UpdateWandererPin();
            if (!Server || ZDOMan.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null || !wandererEnabled.Value) return;
            if (!wandererScanned) { wandererScanned = ZDOMan.instance.GetAllZDOsWithPrefabIterative(WandererPrefab, wandererZdos, ref wandererScan); return; }
            if (!guardsScanned) { guardsScanned = ZDOMan.instance.GetAllZDOsWithPrefabIterative(GuardPrefab, guardZdos, ref guardScan); return; }
            if (Time.unscaledTime < nextCaravan) return; nextCaravan = Time.unscaledTime + 3;
            try
            {
                EnsureServer();
                var seen = new HashSet<ZDOID>();
                wandererZdos.RemoveAll(z => !z.IsValid() || !seen.Add(z.m_uid));
                seen.Clear(); guardZdos.RemoveAll(z => !z.IsValid() || !seen.Add(z.m_uid));
                if (!EnsureWorldSite(service.Data.caravan, service.Data.village, wandererZdos, guardZdos)) return;
                var scene = ZNetScene.instance;
                if (wandererZdos.Count == 0)
                {
                    Vector3 origin = CaravanPosition(service.Data.caravan);
                    Vector3 spot; if (!TryCaravanGround(origin, 20, out spot)) return;
                    var spawned = SpawnCaravanActor(scene, WandererPrefab, spot);
                    if (spawned == null) return; wandererZdos.Add(spawned);
                    Logger.LogInfo("Viandante creato in " + spot + ". Segnaposto disponibile sulla mappa.");
                }
                var merchant = wandererZdos[0]; Vector3 position = merchant.GetPosition();
                var state = service.Data.caravan; state.active = true; state.planned = false; state.x = position.x; state.y = position.y; state.z = position.z;
                if (scene.FindInstance(merchant) != null && guardZdos.Count < 2 && Time.unscaledTime >= nextGuardRespawn)
                {
                    nextGuardRespawn = Time.unscaledTime + 60;
                    for (int i = guardZdos.Count; i < 2; i++)
                    {
                        Vector3 spot; if (!TryCaravanGround(position, 3 + i * 2, out spot)) continue;
                        var guard = SpawnCaravanActor(scene, GuardPrefab, spot); if (guard != null) guardZdos.Add(guard);
                    }
                    Logger.LogInfo("Scorta del viandante: " + guardZdos.Count + "/2 guardie.");
                }
                if (Time.unscaledTime >= nextCaravanSave) { SaveServer(); nextCaravanSave = Time.unscaledTime + 30; }
                foreach (var p in service.Data.players.Where(p => IsOnline(p.id)).ToList()) PushState(p, "");
            }
            catch (Exception ex) { nextCaravan = Time.unscaledTime + 30; Logger.LogError("Viandante: " + ex); }
        }
        private ZDO SpawnCaravanActor(ZNetScene scene, string prefab, Vector3 spot)
        {
            var template = scene.GetPrefab(prefab); if (template == null) return null;
            var instance = Instantiate(template, spot, Quaternion.identity);
            var view = instance.GetComponent<ZNetView>();
            if (!view.IsValid()) { Destroy(instance); return null; }
            instance.GetComponent<MonsterAI>().SetPatrolPoint();
            instance.GetComponent<Character>().SetTamed(true);
            return view.GetZDO();
        }
        private bool TryCaravanGround(Vector3 center, float radius, out Vector3 point)
        {
            point = center;
            for (int i = 0; i < 20; i++)
            {
                var p = center + Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * (radius + i % 4 * 2);
                if (!ZNetScene.instance.IsAreaReady(p)) continue;
                float ground; if (!ZoneSystem.instance.GetGroundHeight(p, out ground) || ground < ZoneSystem.instance.m_waterLevel + 1) continue;
                p.y = ground + .15f;
                Vector3 normal; Heightmap.Biome biome; Heightmap.BiomeArea area; Heightmap map;
                var sample = p; ZoneSystem.instance.GetGroundData(ref sample, out normal, out biome, out area, out map);
                if (normal.y < .85f) continue;
                if (Physics.CheckSphere(p + Vector3.up, .75f, LayerMask.GetMask("Default", "static_solid", "piece"))) continue;
                point = p; return true;
            }
            return false;
        }
        private static Vector3 CaravanPosition(CaravanState state) { return new Vector3(state.x, state.y, state.z); }
        private bool NearMerchant(long sender, string shop = "wanderer")
        {
            if (shop != "wanderer") return NearVillageVendor(sender, shop);
            if (!wandererEnabled.Value) return false;
            var merchant = wandererZdos.FirstOrDefault(z => z.IsValid()); if (merchant == null) return false;
            Vector3 playerPos;
            if (sender == ZNet.GetUID() && Player.m_localPlayer != null) playerPos = Player.m_localPlayer.transform.position;
            else { var peer = ZNet.instance.GetPeer(sender); var zdo = peer == null ? null : ZDOMan.instance.GetZDO(peer.m_characterID); if (zdo == null) return false; playerPos = zdo.GetPosition(); }
            return Vector3.Distance(playerPos, merchant.GetPosition()) <= 6;
        }
        private void UpdateWandererPin()
        {
            var map = Minimap.instance;
            if (!ready || Player.m_localPlayer == null || map == null || (snapshot.caravan == null || (!snapshot.caravan.active && !snapshot.caravan.planned)))
            { if (pinMap != null && wandererPin != null) pinMap.RemovePin(wandererPin); wandererPin = null; pinMap = null; return; }
            if (pinMap != map) { pinMap = map; wandererPin = null; }
            if (wandererPin == null)
            {
                wandererPin = map.AddPin(CaravanPosition(snapshot.caravan), Minimap.PinType.None, L("Il viandante"), false, false, 0);
                var icon = map.m_locationIcons.FirstOrDefault(i => (i.m_name ?? "").IndexOf("Vendor", StringComparison.OrdinalIgnoreCase) >= 0 || (i.m_name ?? "").IndexOf("Haldor", StringComparison.OrdinalIgnoreCase) >= 0).m_icon;
                wandererPin.m_icon = icon != null ? icon : ObjectDB.instance?.GetItemPrefab("Coins")?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
                wandererPin.m_doubleSize = true;
            }
            var actor = CaravanActor.Active.FirstOrDefault(a => a != null && a.IsMerchant && a.Shop == "wanderer");
            wandererPin.m_name = L("Il viandante");
            wandererPin.m_pos = actor != null ? actor.transform.position : CaravanPosition(snapshot.caravan);
        }
        private bool CanReceiveResource(ResourceOffer offer)
        {
            var item = ObjectDB.instance?.GetItemPrefab(offer.Prefab)?.GetComponent<ItemDrop>()?.m_itemData.Clone();
            if (item == null || Player.m_localPlayer == null) return false;
            item.m_worldLevel = (byte)Game.m_worldLevel;
            return Player.m_localPlayer.GetInventory().CanAddItem(item, offer.Amount);
        }
        private void BuyResource(ResourceOffer offer)
        {
            if (!localShopOpen || !IsLocalShopAvailable() || localShopActor.Shop != offer.Shop) { CloseNativeMenu(); return; }
            if (!CanReceiveResource(offer)) { Feedback(L("Libera spazio nell'inventario prima di acquistare.")); return; }
            Cmd("trade", offer.Prefab);
        }
        private int GiveResource(ResourceGrant grant)
        {
            var offer = VillageCatalog.Find(grant.prefab);
            var prefab = ObjectDB.instance?.GetItemPrefab(grant.prefab); var inventory = Player.m_localPlayer?.GetInventory();
            if (offer == null || prefab == null || inventory == null || grant.amount != offer.Amount || !CanReceiveResource(offer)) return 0;
            var data = prefab.GetComponent<ItemDrop>().m_itemData;
            Func<int> count = () => inventory.GetAllItems().Where(i => i.m_shared.m_name == data.m_shared.m_name).Sum(i => i.m_stack);
            int before = count();
            for (int left = grant.amount; left > 0;)
            { int stack = Math.Min(left, data.m_shared.m_maxStackSize); if (!inventory.AddItem(prefab, stack)) break; left -= stack; }
            return Math.Max(0, Math.Min(grant.amount, count() - before));
        }
        private void DrawMerchantOffers(ResourceOffer[] offers, float distance)
        {
            foreach (var offer in offers)
            {
                Panel(content, "ResourceOffer", 0, rowY, 750, 78, inset, new Color(0, 0, 0, .42f));
                var icon = ObjectDB.instance?.GetItemPrefab(offer.Prefab)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon();
                if (icon != null) { var img = Panel(content, "ResourceIcon", 12, rowY + 10, 54, 54, icon, Color.white); img.type = UnityEngine.UI.Image.Type.Simple; img.preserveAspect = true; }
                Text(content, offer.Amount + " × " + ItemName(offer.Prefab), 80, rowY + 8, 390, 30, 24, true);
                Text(content, L("{0} oro", offer.Price), 80, rowY + 40, 390, 26, 19);
                Button(content, L("Acquista"), 552, rowY + 18, 175, 42, () => BuyResource(offer), icon != null && snapshot.economy && distance <= 6 && snapshot.self.gold >= offer.Price && snapshot.self.resourceGrants.Count == 0);
                rowY += 90;
            }
        }
    }

    public sealed class CaravanActor : MonoBehaviour, Interactable
    {
        public bool IsMerchant;
        public string Shop = "wanderer";
        internal static readonly List<CaravanActor> Active = new List<CaravanActor>();
        private MonsterAI ai;
        private ZNetView view;
        private float nextFollow;
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }
        private void Start() { ai = GetComponent<MonsterAI>(); view = GetComponent<ZNetView>(); GetComponent<CharacterDrop>()?.SetDropsEnabled(false); }
        private void Update()
        {
            if (IsMerchant || ai == null || view == null || !view.IsValid() || !view.IsOwner() || Time.time < nextFollow) return;
            nextFollow = Time.time + 1;
            var merchant = Active.FirstOrDefault(a => a != null && a.IsMerchant && a.Shop == "wanderer");
            if (merchant != null) ai.SetFollowTarget(merchant.gameObject);
        }
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!IsMerchant || hold || user != Player.m_localPlayer || Vector3.Distance(user.transform.position, transform.position) > 6) return false;
            MMOMODPlugin.Instance?.OpenLocalShop(this); return true;
        }
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }
    }
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class CaravanPrefabsPatch
    { [HarmonyPostfix] static void Postfix(ZNetScene __instance) { MMOMODPlugin.Instance?.RegisterCaravan(__instance); } }
    [HarmonyPatch(typeof(Character), "GetHoverText")]
    internal static class CaravanHoverPatch
    {
        [HarmonyPostfix] static void Postfix(Character __instance, ref string __result)
        {
            var actor = __instance.GetComponent<CaravanActor>(); if (actor == null) return;
            __result = actor.IsMerchant ? Localization.instance.Localize(__instance.m_name + "\n[<color=yellow><b>$KEY_Use</b></color>] $mmomod_buy") : MMOMODPlugin.L("Guardia del viandante");
        }
    }
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class CaravanProtectionPatch
    { [HarmonyPrefix] static bool Prefix(Character __instance) { return __instance.GetComponent<CaravanActor>()?.IsMerchant != true; } }
    [HarmonyPatch(typeof(MonsterAI), "UpdateAI")]
    internal static class CaravanTradingPausePatch
    {
        [HarmonyPrefix] static bool Prefix(MonsterAI __instance, ref bool __result)
        {
            if (__instance.GetComponent<CaravanActor>()?.IsMerchant != true) return true;
            var view = __instance.GetComponent<ZNetView>(); if (view == null || !view.IsValid() || !view.IsOwner()) return true;
            if (__instance.GetComponent<CaravanActor>().Shop != "wanderer")
            { __instance.StopMoving(); __result = true; return false; }
            var player = Player.GetClosestPlayer(__instance.transform.position, 5);
            if (player == null) return true;
            __instance.StopMoving(); __instance.LookTowards(player.transform.position - __instance.transform.position); __result = true; return false;
        }
    }
}
