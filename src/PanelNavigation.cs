using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private bool mapRequested;
        private Vector3? mapDestination;
        private int panelInputUntil = -1;
        internal bool BlocksNativePanels => MenuOpen || mapRequested || Time.frameCount <= panelInputUntil;

        private void PrepareModPanel()
        {
            if (Menu.IsActive()) return;
            mapRequested = false; mapDestination = null;
            if (Minimap.instance != null && Minimap.IsOpen()) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            InventoryGui.instance?.Hide();
            MenuOpen = true;
            ShowModCursor();
            BuildNativeUI();
        }
        private void OpenWorldMap(Vector3? destination = null)
        {
            if (Minimap.instance == null || Player.m_localPlayer == null) return;
            CloseNativeMenu();
            InventoryGui.instance?.Hide();
            mapDestination = destination; mapRequested = true;
            // Hide() animates the inventory; IsVisible() stays true for two frames.
            // Wait until that finishes before opening the large map.
        }
        private void HandlePanelNavigation()
        {
            if (Input.GetKeyDown(KeyCode.F10)) ToggleMenu();
            else if (Input.GetKeyDown(KeyCode.Escape) && (MenuOpen || mapRequested))
            {
                mapRequested = false; mapDestination = null;
                CloseNativeMenu();
            }
            if (MenuOpen)
            {
                InventoryGui.instance?.Hide();
                if (Minimap.instance != null && Minimap.IsOpen()) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            }
            // Recover a map/inventory overlap left by an earlier transition.
            else if (Minimap.IsOpen() && InventoryGui.IsVisible()) InventoryGui.instance?.Hide();

            if (!mapRequested) return;
            if (Player.m_localPlayer == null || Minimap.instance == null)
            { mapRequested = false; mapDestination = null; return; }
            InventoryGui.instance?.Hide();
            if (InventoryGui.IsVisible() || Time.frameCount <= panelInputUntil) return;
            var destination = mapDestination;
            mapRequested = false; mapDestination = null;
            if (destination.HasValue) Minimap.instance.ShowPointOnMap(destination.Value);
            else Minimap.instance.SetMapMode(Minimap.MapMode.Large);
        }
        private void ReleaseModPanel()
        {
            MenuOpen = false;
            if (localShopOpen) { localShopOpen = false; localShopActor = null; DestroyNativeUI(); }
            // Prevent the same Escape/click from opening another native panel.
            panelInputUntil = Time.frameCount + 2;
            if (EventSystem.current != null && nativeRoot != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(nativeRoot.transform)) EventSystem.current.SetSelectedGameObject(null);
            }
            if (nativeRoot != null) nativeRoot.SetActive(false);
            bool other = Player.m_localPlayer == null || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen();
            ZCursor.LockState = other ? CursorLockMode.None : CursorLockMode.Locked;
            if (other) ZCursor.Show(); else ZCursor.Hide();
        }
        internal static void ShowModCursor()
        {
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
        }
    }
    [HarmonyPatch(typeof(InventoryGui), "Show")]
    internal static class ExclusiveInventoryPatch
    {
        [HarmonyPrefix] static bool Prefix()
        { return MMOMODPlugin.Instance == null || (!MMOMODPlugin.Instance.BlocksNativePanels && !Minimap.IsOpen()); }
    }
    [HarmonyPatch(typeof(Minimap), "SetMapMode")]
    internal static class ExclusiveMapPatch
    {
        [HarmonyPrefix] static bool Prefix(Minimap.MapMode mode)
        { return mode != Minimap.MapMode.Large || MMOMODPlugin.Instance == null || (!MMOMODPlugin.Instance.BlocksNativePanels && !InventoryGui.IsVisible()); }
    }
    [HarmonyPatch(typeof(Menu), "Update")]
    internal static class ExclusivePauseMenuPatch
    {
        [HarmonyPrefix] static bool Prefix()
        { return MMOMODPlugin.Instance == null || !MMOMODPlugin.Instance.BlocksNativePanels; }
    }
    [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
    internal static class ModCursorCapturePatch
    {
        [HarmonyPrefix] static bool Prefix()
        {
            if (MMOMODPlugin.Instance?.MenuOpen != true) return true;
            MMOMODPlugin.ShowModCursor();
            return false;
        }
    }
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class ModControllerInputPatch
    {
        [HarmonyPostfix] static void Postfix(ref bool __result)
        { if (MMOMODPlugin.Instance?.BlocksNativePanels == true) __result = false; }
    }
}
