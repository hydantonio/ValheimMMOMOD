using System;
using HarmonyLib;
using System.Linq;
using TMPro;
using UnityEngine;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private string activeLanguage;
        internal static string Language => Localization.instance?.GetSelectedLanguage() ?? "English";
        internal static string L(string key, params object[] args) => ModText.Translate(Language, key, args);
        private static string ItemName(string prefab)
        {
            string native = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name;
            return native != null ? Localization.instance.Localize(native) : L(VillageCatalog.Find(prefab)?.Name ?? prefab);
        }
        private static string LocalizedMessage(string message) => ModText.Resolve(Language, message, ItemName);
        private static void AddModWord(string key, string value) => AccessTools.Method(typeof(Localization), "AddWord").Invoke(Localization.instance, new object[] { key, value });
        private void RefreshLanguage()
        {
            if (Localization.instance == null || activeLanguage == Language) return;
            activeLanguage = Language;
            AddModWord("mmomod_wanderer", L("Il viandante"));
            AddModWord("mmomod_guard", L("Guardia del viandante"));
            AddModWord("mmomod_buy", L("Acquista"));
            AddModWord("mmomod_food_vendor", L("Runa • Dispensa"));
            AddModWord("mmomod_equipment_vendor", L("Borin • Emporio"));
            feedback = ""; feedbackUntil = 0;
            DestroyNativeUI(); uiDirty = true;
            if (guideEntry != null)
            {
                foreach (var label in guideEntry.GetComponentsInChildren<TMP_Text>(true)) label.text = L("Guida MMO MOD");
                foreach (var label in guideEntry.GetComponentsInChildren<UnityEngine.UI.Text>(true)) label.text = L("Guida MMO MOD");
            }
            var owner = guideMenu; bool wasOpen = guideRoot != null && guideRoot.activeSelf; int page = guidePage;
            if (guideRoot != null) { guideRoot.SetActive(false); Destroy(guideRoot); guideRoot = null; }
            if (wasOpen && owner != null) { OpenMainGuide(owner); ShowGuidePage(page); }
            Logger.LogInfo("MMO MOD language: " + activeLanguage);
        }
    }
}
