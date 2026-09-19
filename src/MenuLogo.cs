using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private Sprite menuLogo;
        internal void ApplyMenuLogo(FejdStartup menu)
        {
            try
            {
                // Replace only the game's title artwork in the startup scene.
                // Keeping its RectTransform also preserves native menu animations.
                var titles = Resources.FindObjectsOfTypeAll<Image>().Where(i =>
                    i.gameObject.scene == menu.gameObject.scene && i.sprite != null &&
                    (i.sprite.name == "Logo2_menu_highres" || i.sprite.name == "valheim_logo_01_small" || i.sprite.name == "Valheim_DeepNorth_Logo")).ToArray();
                if (titles.Length == 0) { Logger.LogWarning("Logo menu: immagine originale non trovata."); return; }
                if (menuLogo == null)
                {
                    using (var stream = typeof(MMOMODPlugin).Assembly.GetManifestResourceStream("MMOMOD.MenuLogo.png"))
                    using (var bytes = new MemoryStream())
                    {
                        if (stream == null) throw new InvalidOperationException("Logo incorporato assente");
                        stream.CopyTo(bytes);
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        var decode = AccessTools.Method(Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true), "LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                        if (!(bool)decode.Invoke(null, new object[] { texture, bytes.ToArray(), true })) { Destroy(texture); throw new InvalidOperationException("PNG non valido"); }
                        texture.name = "MMOMOD_MenuLogo";
                        menuLogo = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                        menuLogo.name = "MMOMOD_MenuLogo";
                    }
                }
                foreach (var title in titles)
                {
                    title.sprite = menuLogo; title.overrideSprite = null;
                    title.type = Image.Type.Simple; title.preserveAspect = true; title.raycastTarget = false;
                    title.color = Color.white;
                }
                Logger.LogInfo("Logo MMO MOD applicato al menu principale (" + titles.Length + ").");
            }
            catch (Exception ex) { Logger.LogError("Logo menu: " + ex); }
        }
    }
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class MainMenuLogoPatch
    {
        [HarmonyPostfix] static void Postfix(FejdStartup __instance) { MMOMODPlugin.Instance?.ApplyMenuLogo(__instance); MMOMODPlugin.Instance?.InstallMainGuide(__instance); }
    }
}

