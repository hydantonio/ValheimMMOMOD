using UnityEngine;

namespace ValheimMMOMOD
{
    // Fit each shop as a rigid group; fit outdoor decorations to the ground.
    // Terrain and player buildings are never modified.
    public sealed class VillageFoundation : MonoBehaviour
    {
        private bool fitted;
        private float retry;
        internal static bool ShopHeight(Vector3 origin, float x, out float height)
        {
            height = 0; float min = float.MaxValue, max = float.MinValue;
            foreach (float dx in new[] { -2f, 0f, 2f }) foreach (float z in new[] { 1f, 3f, 5f })
            {
                float h;
                if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGroundHeight(origin + new Vector3(x + dx, 0, z), out h)) return false;
                min = Mathf.Min(min, h); max = Mathf.Max(max, h);
            }
            height = max + .05f; return max - min <= 1.5f;
        }
        private void Update()
        {
            if (fitted || Time.unscaledTime < retry || ZoneSystem.instance == null) return;
            retry = Time.unscaledTime + 1;
            float left, right;
            if (!ShopHeight(transform.position, -6, out left) || !ShopHeight(transform.position, 6, out right)) return;
            var layout = VillageLayout.Create();
            if (transform.childCount != layout.Count) { enabled = false; return; }
            var heights = new float[layout.Count];
            for (int i = 0; i < layout.Count; i++)
            {
                var part = layout[i];
                bool shop = part.z >= 1 && part.z <= 5 && Mathf.Abs(part.x) >= 4 && Mathf.Abs(part.x) <= 8;
                if (shop) heights[i] = part.x < 0 ? left : right;
                else if (!ZoneSystem.instance.GetGroundHeight(transform.position + new Vector3(part.x, 0, part.z), out heights[i])) return;
            }
            for (int i = 0; i < layout.Count; i++)
            {
                var part = layout[i]; var child = transform.GetChild(i);
                child.localPosition = new Vector3(part.x, heights[i] - transform.position.y + part.y, part.z);
                // Omit decorative rails intersecting natural obstacles; do not cut the obstacle.
                if (part.prefab == "wood_pole" || part.prefab == "wood_beam")
                {
                    foreach (var obstacle in Physics.OverlapSphere(child.position + Vector3.up * .3f, .45f, LayerMask.GetMask("Default", "static_solid")))
                        if (!obstacle.transform.IsChildOf(transform)) { child.gameObject.SetActive(false); break; }
                }
            }
            fitted = true; enabled = false;
        }
    }
}
