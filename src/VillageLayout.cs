using System.Collections.Generic;

namespace ValheimMMOMOD
{
    public sealed class VillagePart
    {
        public string prefab;
        public float x, y, z, yaw;
        public VillagePart(string prefab, float x, float y, float z, float yaw = 0)
        { this.prefab = prefab; this.x = x; this.y = y; this.z = z; this.yaw = yaw; }
    }
    public static class VillageLayout
    {
        public static List<VillagePart> Create()
        {
            var parts = new List<VillagePart>();
            foreach (float center in new[] { -6f, 6f })
            {
                // Two open-front 4 x 4 metre timber shops, facing the square.
                foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { 2f, 4f })
                {
                    parts.Add(new VillagePart("wood_floor", center + x, .3f, z));
                    parts.Add(new VillagePart("wood_roof_45", center + x, 2.3f, z, x < 0 ? 90 : 270));
                }
                foreach (float z in new[] { 2f, 4f }) foreach (float side in new[] { -2f, 2f })
                    parts.Add(new VillagePart("woodwall", center + side, .3f, z, side < 0 ? 90 : 270));
                foreach (float x in new[] { -1f, 1f }) parts.Add(new VillagePart("woodwall", center + x, .3f, 5, 180));
                foreach (float x in new[] { -2f, 2f }) foreach (float z in new[] { 1f, 5f })
                {
                    parts.Add(new VillagePart("wood_pole2", center + x, -2.7f, z));
                    parts.Add(new VillagePart("wood_pole2", center + x, -.7f, z));
                    parts.Add(new VillagePart("wood_pole2", center + x, 1.3f, z));
                }
                // Finished ridge, carved gables and shop colors, using native pieces.
                foreach (float z in new[] { 2f, 4f }) parts.Add(new VillagePart("wood_roof_top_45", center, 4.3f, z));
                foreach (float z in new[] { 1f, 5f })
                    parts.Add(new VillagePart("wood_wall_roof_top_45", center, 2.3f, z, z == 1 ? 0 : 180));
                parts.Add(new VillagePart(center < 0 ? "piece_banner01" : "piece_banner02", center + 1.7f, 2.2f, 1.1f));
                parts.Add(new VillagePart("piece_chest_wood", center - 1, .3f, 4, 180));
                parts.Add(new VillagePart("wood_beam", center - 1, 2.3f, 1, 90));
                parts.Add(new VillagePart("wood_beam", center + 1, 2.3f, 1, 90));
                parts.Add(new VillagePart("piece_table", center, .3f, 2));
            }
            parts.Add(new VillagePart("piece_table", 0, 0, -3));
            parts.Add(new VillagePart("piece_bench01", -2, 0, -3, 90));
            parts.Add(new VillagePart("piece_bench01", 2, 0, -3, 270));
            // Low rails enclose the back/sides, leaving a wide southern entrance.
            for (int x = -8; x <= 8; x += 2)
            {
                parts.Add(new VillagePart("wood_pole", x, 0, 7));
                if (x < 8) parts.Add(new VillagePart("wood_beam", x + 1, .8f, 7, 90));
            }
            foreach (float side in new[] { -10f, 10f }) for (int z = -5; z <= 7; z += 2)
            {
                parts.Add(new VillagePart("wood_pole", side, 0, z));
                if (z < 7) parts.Add(new VillagePart("wood_beam", side, .8f, z + 1));
            }
            // Entrance lamps and a pair of colored standards frame the market square.
            foreach (float x in new[] { -9f, 9f })
            {
                parts.Add(new VillagePart("piece_groundtorch", x, 0, -4));
                parts.Add(new VillagePart("wood_pole2", x, 0, 6));
                parts.Add(new VillagePart("wood_pole2", x, 2, 6));
                parts.Add(new VillagePart(x < 0 ? "piece_banner01" : "piece_banner02", x, 3.6f, 6));
            }
            return parts;
        }
    }
}
