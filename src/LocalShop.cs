using UnityEngine;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private CaravanActor localShopActor;
        private bool localShopOpen;
        private bool IsLocalShopAvailable()
        {
            var player = Player.m_localPlayer;
            return ready && player != null && !player.IsDead() && localShopActor != null &&
                localShopActor.isActiveAndEnabled && localShopActor.IsMerchant &&
                Vector3.Distance(player.transform.position, localShopActor.transform.position) <= 6;
        }
        internal void OpenLocalShop(CaravanActor actor)
        {
            if (Menu.IsActive()) return;
            localShopActor = actor;
            if (!IsLocalShopAvailable()) { localShopActor = null; return; }
            DestroyNativeUI(); localShopOpen = true;
            PrepareModPanel(); uiDirty = true;
        }
        private void LocalShopPage()
        {
            if (!IsLocalShopAvailable()) return;
            string shop = localShopActor.Shop;
            Heading(shop == "food" ? L("Runa • Dispensa") : shop == "equipment" ? L("Borin • Emporio") : L("Il viandante • Risorse"));
            Paragraph(shop == "food" ? L("Cibi semplici per il prossimo viaggio.") : shop == "equipment" ? L("Attrezzi e armature per iniziare l'avventura. Qualità 1.") : L("Legna, pietra e materiali raccolti lungo il cammino."), 48);
            if (CoinCount > 0) Act(L("Deposita le monete in oro"), Deposit);
            DrawMerchantOffers(shop == "food" ? VillageCatalog.Food : shop == "equipment" ? VillageCatalog.Equipment : WandererCatalog.Offers,
                Vector3.Distance(Player.m_localPlayer.transform.position, localShopActor.transform.position));
        }
    }
}
