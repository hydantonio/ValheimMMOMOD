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
        private const string VillagePrefab = "MMOMOD_StarterVillage", FoodVendorPrefab = "MMOMOD_FoodVendor", EquipmentVendorPrefab = "MMOMOD_EquipmentVendor";
        private ConfigEntry<bool> villageEnabled;
        private readonly List<ZDO> villageZdos = new List<ZDO>(), foodVendorZdos = new List<ZDO>(), equipmentVendorZdos = new List<ZDO>();
        private int villageScan, foodScan, equipmentScan;
        private bool villageScanned, foodScanned, equipmentScanned;
        private float nextVillage;
        private int villageSearchRound;
        private string siteRejection = "";
        private Minimap villagePinMap;
        private Minimap.PinData villagePin;
        private string villageStatus = "Ricerca di una radura pianeggiante nei Prati...";

        private void RegisterVillage(ZNetScene scene, GameObject source)
        {
            var food = RegisterCaravanPrefab(scene, source, FoodVendorPrefab, true);
            food.GetComponent<CaravanActor>().Shop = "food";
            food.GetComponent<Humanoid>().m_name = "$mmomod_food_vendor";
            food.GetComponent<MonsterAI>().m_randomMoveRange = 0;
            var gear = RegisterCaravanPrefab(scene, source, EquipmentVendorPrefab, true);
            gear.GetComponent<CaravanActor>().Shop = "equipment";
            gear.GetComponent<Humanoid>().m_name = "$mmomod_equipment_vendor";
            gear.GetComponent<MonsterAI>().m_randomMoveRange = 0;

            var layout = VillageLayout.Create();
            foreach (string name in layout.Select(p => p.prefab).Distinct())
                if (scene.GetPrefab(name) == null) { villageStatus = "Villaggio non disponibile: elemento di costruzione mancante (" + name + ")."; Logger.LogError(villageStatus); return; }
            var root = new GameObject(VillagePrefab); root.SetActive(false); root.transform.SetParent(caravanTemplates.transform, false);
            root.AddComponent<ZNetView>().m_persistent = true;
            root.AddComponent<VillageFoundation>();
            foreach (var part in layout)
            {
                var go = Instantiate(scene.GetPrefab(part.prefab), root.transform, false);
                go.transform.localPosition = new Vector3(part.x, part.y, part.z);
                go.transform.localRotation = Quaternion.Euler(0, part.yaw, 0);
                if (part.prefab == "piece_groundtorch")
                {
                    var fire = go.GetComponent<Fireplace>();
                    if (fire != null)
                    {
                        if (fire.m_enabledObject != null) fire.m_enabledObject.SetActive(true);
                        if (fire.m_enabledObjectHigh != null) fire.m_enabledObjectHigh.SetActive(true);
                        if (fire.m_enabledObjectLow != null) fire.m_enabledObjectLow.SetActive(false);
                    }
                }
                // One persistent network anchor owns the entire static layout.
                // Keep native meshes/colliders, without independent drops, decay or ZDOs.
                foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is ZNetView)).Reverse()) DestroyImmediate(behaviour);
                foreach (var networkView in go.GetComponentsInChildren<ZNetView>(true).Reverse()) DestroyImmediate(networkView);
                if (go.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Componenti attivi rimasti nel decoro: " + part.prefab);
                foreach (var body in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
                if (part.prefab == "piece_groundtorch")
                {
                    foreach (var light in go.GetComponentsInChildren<Light>(true))
                    { light.enabled = true; light.color = new Color(1f, .65f, .3f); light.range = 9; light.intensity = 1.5f; light.shadows = LightShadows.None; }
                }
                go.SetActive(true);
            }
            root.SetActive(true);
            scene.m_prefabs.Add(root);
            ((Dictionary<int, GameObject>)AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(scene)).Add(VillagePrefab.GetStableHashCode(), root);
            villageStatus = "Ricerca di una radura pianeggiante nei Prati, libera da ostacoli e costruzioni.";
            Logger.LogInfo("Borgo del Viandante: " + layout.Count + " elementi nativi e due botteghe registrati.");
        }
        private void ResetVillage()
        {
            if (villagePinMap != null && villagePin != null) villagePinMap.RemovePin(villagePin);
            villagePin = null; villagePinMap = null;
            villageZdos.Clear(); foodVendorZdos.Clear(); equipmentVendorZdos.Clear();
            villageScan = foodScan = equipmentScan = 0; villageScanned = foodScanned = equipmentScanned = false; nextVillage = 0;
        }
        private void TickVillage()
        {
            UpdateVillagePin();
            if (!Server || !villageEnabled.Value || ZDOMan.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null) return;
            if (!villageScanned) { villageScanned = ZDOMan.instance.GetAllZDOsWithPrefabIterative(VillagePrefab, villageZdos, ref villageScan); return; }
            if (!foodScanned) { foodScanned = ZDOMan.instance.GetAllZDOsWithPrefabIterative(FoodVendorPrefab, foodVendorZdos, ref foodScan); return; }
            if (!equipmentScanned) { equipmentScanned = ZDOMan.instance.GetAllZDOsWithPrefabIterative(EquipmentVendorPrefab, equipmentVendorZdos, ref equipmentScan); return; }
            if (Time.unscaledTime < nextVillage) return; nextVillage = Time.unscaledTime + 10;
            try
            {
                EnsureServer();
                foreach (var list in new[] { villageZdos, foodVendorZdos, equipmentVendorZdos })
                { var seen = new HashSet<ZDOID>(); list.RemoveAll(z => !z.IsValid() || !seen.Add(z.m_uid)); }
                if (!EnsureWorldSite(service.Data.village, service.Data.caravan, villageZdos, foodVendorZdos, equipmentVendorZdos)) return;
                if (villageZdos.Count == 0)
                {
                    var prefab = ZNetScene.instance.GetPrefab(VillagePrefab); if (prefab == null) return;
                    Vector3 origin = CaravanPosition(service.Data.village), position;
                    if (!ZNetScene.instance.IsAreaReady(origin)) return;
                    if (!VillageSiteIsClear(origin, out position) && !FindVillageSite(origin, out position))
                    { villageStatus = "Cerco una radura libera attorno al segnaposto del borgo."; return; }
                    var instance = Instantiate(prefab, position, Quaternion.identity);
                    var view = instance.GetComponent<ZNetView>();
                    if (!view.IsValid()) { Destroy(instance); return; }
                    villageZdos.Add(view.GetZDO());
                    Logger.LogInfo("Borgo del Viandante creato in " + position);
                }
                var anchor = villageZdos[0]; var pos = anchor.GetPosition();
                var state = service.Data.village; bool wasActive = state.active;
                state.active = true; state.planned = false; villageStatus = "Borgo creato: cerca il segnaposto sulla mappa."; state.x = pos.x; state.y = pos.y; state.z = pos.z;
                bool spawned = false;
                if (ZNetScene.instance.FindInstance(anchor) != null)
                {
                    spawned |= EnsureVillageVendor(foodVendorZdos, FoodVendorPrefab, pos + new Vector3(-6, 0, -1));
                    spawned |= EnsureVillageVendor(equipmentVendorZdos, EquipmentVendorPrefab, pos + new Vector3(6, 0, -1));
                }
                if (!wasActive || spawned)
                {
                    SaveServer();
                    foreach (var player in service.Data.players.Where(p => IsOnline(p.id)).ToList()) PushState(player, "");
                    Logger.LogInfo("Borgo: dispensa " + foodVendorZdos.Count + ", emporio " + equipmentVendorZdos.Count);
                }
            }
            catch (Exception ex) { nextVillage = Time.unscaledTime + 60; villageStatus = "Errore di creazione del borgo: " + ex.Message; Logger.LogError("Villaggio: " + ex); }
        }
        private bool FindVillageSite(Vector3 origin, out Vector3 position)
        {
            position = origin;
            var rejected = new Dictionary<string, int>();
            for (int i = 0; i < 144; i++)
            {
                var candidate = origin + Quaternion.Euler(0, i * 137.5f + villageSearchRound * 17, 0) * Vector3.forward * (24 + i % 7 * 10);
                if (VillageSiteIsClear(candidate, out position)) { villageSearchRound++; return true; }
                if (!rejected.ContainsKey(siteRejection)) rejected[siteRejection] = 0;
                rejected[siteRejection]++;
            }
            villageSearchRound++;
            if (villageSearchRound == 1 || villageSearchRound % 6 == 0)
                Logger.LogInfo("Ricerca borgo: " + string.Join(", ", rejected.Select(r => r.Key + "=" + r.Value)));
            return false;
        }
        private bool RejectVillageSite(string reason) { siteRejection = reason; return false; }
        private bool VillageSiteIsClear(Vector3 center, out Vector3 position)
        {
            position = center;
            float min = float.MaxValue, max = float.MinValue;
            for (int x = -12; x <= 12; x += 4) for (int z = -8; z <= 12; z += 4)
            {
                var p = center + new Vector3(x, 0, z);
                if (!ZNetScene.instance.IsAreaReady(p)) return RejectVillageSite("zona non caricata");
                float h; if (!ZoneSystem.instance.GetGroundHeight(p, out h) || h < ZoneSystem.instance.m_waterLevel + 1.5f) return RejectVillageSite("acqua");
                Vector3 normal; Heightmap.Biome biome; Heightmap.BiomeArea area; Heightmap map;
                ZoneSystem.instance.GetGroundData(ref p, out normal, out biome, out area, out map);
                if (biome != Heightmap.Biome.Meadows) return RejectVillageSite("fuori dai Prati");
                if (normal.y < .82f) return RejectVillageSite("pendenza");
                min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                if (max - min > 4) return RejectVillageSite("dislivello generale");
            }
            float left, right;
            if (!VillageFoundation.ShopHeight(center, -6, out left) || !VillageFoundation.ShopHeight(center, 6, out right)) return RejectVillageSite("dislivello botteghe");
            // Keep existing buildings protected over the entire site.
            var boundsCenter = new Vector3(center.x, (max + min) * .5f + 3, center.z + 2);
            if (Physics.CheckBox(boundsCenter, new Vector3(12, 3 + (max - min) * .5f, 10), Quaternion.identity, LayerMask.GetMask("piece"))) return RejectVillageSite("costruzioni presenti");
            // Natural obstacles are checked along buildings, vendors and the central square.
            int mask = LayerMask.GetMask("Default", "static_solid");
            foreach (float side in new[] { -6f, 0f, 6f })
            {
                var p = center + new Vector3(side, 0, side == 0 ? -2 : 2);
                float height; if (!ZoneSystem.instance.GetGroundHeight(p, out height)) return RejectVillageSite("terreno assente");
                p.y = height + 3;
                if (Physics.CheckBox(p, new Vector3(side == 0 ? 3 : 2.8f, 3, side == 0 ? 4 : 4.5f), Quaternion.identity, mask)) return RejectVillageSite("alberi o rocce");
            }
            position.y = (left + right) * .5f; return true;
        }
        private bool EnsureVillageVendor(List<ZDO> list, string prefab, Vector3 point)
        {
            if (list.Count != 0) return false;
            float ground; if (!ZoneSystem.instance.GetGroundHeight(point, out ground)) return false;
            point.y = ground + .15f;
            var zdo = SpawnCaravanActor(ZNetScene.instance, prefab, point); if (zdo == null) return false;
            var facing = Quaternion.Euler(0, 180, 0);
            var instance = ZNetScene.instance.FindInstance(zdo);
            if (instance != null) instance.transform.rotation = facing;
            zdo.SetRotation(facing);
            list.Add(zdo); return true;
        }
        private bool NearVillageVendor(long sender, string shop)
        {
            if (!villageEnabled.Value || (shop != "food" && shop != "equipment")) return false;
            var merchant = (shop == "food" ? foodVendorZdos : equipmentVendorZdos).FirstOrDefault(z => z.IsValid());
            if (merchant == null) return false;
            Vector3 playerPos;
            if (sender == ZNet.GetUID() && Player.m_localPlayer != null) playerPos = Player.m_localPlayer.transform.position;
            else
            {
                var peer = ZNet.instance.GetPeer(sender); var zdo = peer == null ? null : ZDOMan.instance.GetZDO(peer.m_characterID);
                if (zdo == null) return false; playerPos = zdo.GetPosition();
            }
            return Vector3.Distance(playerPos, merchant.GetPosition()) <= 6;
        }
        private void UpdateVillagePin()
        {
            var map = Minimap.instance;
            if (!ready || Player.m_localPlayer == null || map == null || (snapshot.village == null || (!snapshot.village.active && !snapshot.village.planned)))
            { if (villagePinMap != null && villagePin != null) villagePinMap.RemovePin(villagePin); villagePin = null; villagePinMap = null; return; }
            if (map != villagePinMap) { villagePin = null; villagePinMap = map; }
            if (villagePin == null) villagePin = map.AddPin(CaravanPosition(snapshot.village), Minimap.PinType.Icon2, L("Borgo del Viandante"), false, false, 0);
            villagePin.m_name = L("Borgo del Viandante");
            villagePin.m_pos = CaravanPosition(snapshot.village);
        }
    }
}
