using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private readonly System.Random siteRandom = new System.Random();
        private bool EnsureWorldSite(CaravanState state, CaravanState other, params List<ZDO>[] oldObjects)
        {
            if (WorldGenerator.instance == null) return false;
            if (state.placementVersion == 0)
            {
                ZoneSystem.LocationInstance temple;
                if (!ZoneSystem.instance.FindClosestLocation("StartTemple", Vector3.zero, out temple)) return false;
                Vector3 spawn = temple.m_position;
                // Work in bounded batches; world terrain can be sampled without loading zones.
                for (int attempt = 0; attempt < 96; attempt++)
                {
                    double angle = siteRandom.NextDouble() * Math.PI * 2;
                    float radius = 1400 + (float)siteRandom.NextDouble() * 2400;
                    var p = new Vector3((float)Math.Cos(angle) * radius, 0, (float)Math.Sin(angle) * radius);
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(spawn.x, spawn.z)) < 1300) continue;
                    if (other.placementVersion > 0 && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(other.x, other.z)) < 700) continue;
                    float min = float.MaxValue, max = float.MinValue;
                    bool valid = true;
                    for (int x = -16; x <= 16 && valid; x += 8) for (int z = -16; z <= 16; z += 8)
                    {
                        var sample = p + new Vector3(x, 0, z);
                        float height = WorldGenerator.instance.GetHeight(sample);
                        if (WorldGenerator.instance.GetBiome(sample) != Heightmap.Biome.Meadows || height < ZoneSystem.instance.m_waterLevel + 3) { valid = false; break; }
                        min = Mathf.Min(min, height); max = Mathf.Max(max, height);
                        if (max - min > 2.5f) { valid = false; break; }
                    }
                    if (!valid) continue;
                    if (ZoneSystem.instance.GetLocationList().Any(l => Vector3.Distance(l.m_position, new Vector3(p.x, l.m_position.y, p.z)) < 120)) continue;
                    state.x = p.x; state.y = WorldGenerator.instance.GetHeight(p); state.z = p.z;
                    state.active = false; state.planned = true; state.placementVersion = 1;
                    SaveServer();
                    Logger.LogInfo("Nuova destinazione mercanti, lontana dallo spawn: " + CaravanPosition(state));
                    foreach (var player in service.Data.players.Where(v => IsOnline(v.id)).ToList()) PushState(player, "");
                    break;
                }
                if (state.placementVersion == 0) return false;
            }
            if (state.planned)
            {
                // Only mod-owned anchors/NPCs are migrated. Native world pieces are untouched.
                foreach (var list in oldObjects)
                {
                    foreach (var zdo in list.Where(z => z.IsValid()).ToArray())
                    {
                        zdo.SetOwner(ZNet.GetUID());
                        var instance = ZNetScene.instance.FindInstance(zdo);
                        if (instance != null) ZNetScene.instance.Destroy(instance.gameObject);
                        else ZDOMan.instance.DestroyZDO(zdo);
                    }
                    list.Clear();
                }
            }
            return true;
        }
    }
}
