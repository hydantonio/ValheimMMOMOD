using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private GameObject guideRoot;
        private FejdStartup guideMenu;
        private Button guideEntry;
        private ScrollRect guideScroll;
        private TMP_Text guideHeading, guideBody;
        private Button[] guideSections;
        private int guidePage;
        private int guideClosedFrame = -10;
        internal bool GuideBlocksInput => guideRoot != null && guideRoot.activeSelf || Time.frameCount <= guideClosedFrame + 2;
        private static readonly string[] GuideTitles = { "Primi passi", "Missioni ed EXP", "Oro e scambi", "Mercanti e borgo", "Gilda e amici", "Mappa e partita" };
        private static readonly string[] GuidePages = {
            "BENVENUTO IN MMO MOD\nUn'avventura con missioni, oro, mercanti e funzioni cooperative, anche in giocatore singolo.\n\nF10 — Apri o chiudi il pannello della mod.\nESC — Chiudi il pannello o la bottega.\nM — Apri la mappa di Valheim.\nE — Interagisci con un venditore sul posto.\n\nDa F10 trovi Gilda, Economia, Missioni, Amici e Mappa. Il mouse diventa libero quando il pannello è aperto.\n\nPer iniziare, entra in un mondo e consulta Missioni → Giornaliere. I tasti indicati sono quelli predefiniti del gioco.",
            "SCEGLI LA TUA AVVENTURA\nHai tre missioni giornaliere e tre settimanali: combatti, raccogli materiali, esplora nuove aree o aumenta le abilità. Il progresso viene registrato mentre giochi.\n\nTroppo difficile? Usa «Cambia attività» su una giornaliera ancora incompleta: scegli un obiettivo diverso e più facile. Hai tre cambi complessivi al giorno. Il cambio azzera il progresso della missione e dimezza il premio in oro.\n\nAl completamento premi «Riscatta». L'EXP dipende dall'azione richiesta: 20 per nemico, 2 per risorsa, 1 per nuova area, 50 per livello abilità. Fa crescere il livello avventuriero della mod, distinto dalle abilità native.\n\nLe giornaliere si rinnovano alle 00:00 UTC; le settimanali il lunedì. L'EXP funziona anche senza gilda.",
            "IL TUO SALDO ORO\nGuadagni oro con combattimenti, avanzamenti di abilità e missioni riscattate, secondo le impostazioni dell'host.\n\nIn F10 → Economia puoi depositare le monete dell'inventario nel saldo della mod, prelevarle e trasferire oro a un personaggio conosciuto nel mondo. «Movimenti» mostra lo storico.\n\nLe botteghe usano il saldo oro della mod. Puoi depositare monete anche nella finestra del venditore. Ogni articolo mostra quantità e prezzo prima dell'acquisto.\n\nLascia spazio libero nell'inventario per gli oggetti acquistati. Se la consegna non riesce, l'oro relativo agli oggetti non consegnati viene rimborsato.",
            "INCONTRI LUNGO IL CAMMINO\nIl viandante, accompagnato da due guardie, scambia oro con legna, pietra e altri materiali.\n\nNel Borgo del Viandante trovi Runa, con cibi semplici pronti da mangiare, e Borin, con attrezzi, armi e armature iniziali di qualità 1.\n\nCarovana e borgo hanno posizioni casuali nei Prati, lontane dal tempio iniziale e separate fra loro. Cerca i loro segnaposti sulla mappa M: gli incontri vengono creati quando raggiungi la zona. Le posizioni restano salvate nel mondo.\n\nAvvicinati e premi E sul venditore. I negozi non sono in F10: ogni mercante apre solo il proprio catalogo. La bottega si chiude se ti allontani oltre sei metri.",
            "AVVENTURARSI INSIEME\nIn F10 → Gilda puoi creare una gilda, invitare compagni e gestire le richieste ricevute. Gli inviti devono essere accettati dal destinatario.\n\nIl capogilda gestisce ruoli, membri e tag. Il pannello mostra i compagni e l'esperienza della gilda. Puoi continuare le tue missioni anche giocando da solo.\n\nIn F10 → Amici trovi richieste, amici e giocatori incontrati nel mondo. L'amicizia è reciproca. Il blocco impedisce le richieste della mod.\n\nLa condivisione della mappa è riservata alla tua gilda. Puoi usarla manualmente o attivarla dalle relative opzioni.",
            "IL MONDO RICORDA I TUOI PROGRESSI\nLa mappa resta quella originale di Valheim. Puoi aggiungere segnaposti e condividere l'esplorazione con la gilda dal pannello Mappa di F10.\n\nOro, missioni e dati sociali sono separati per mondo e personaggio. Cambiare mondo può quindi mostrare progressi diversi.\n\nIn giocatore singolo la sincronizzazione è gestita dal tuo gioco. In multiplayer, host e giocatori devono avere la stessa versione di MMO MOD. Attendi la sincronizzazione prima di usare le funzioni.\n\nL'host può disattivare economia, missioni, viandante o villaggio. Questa guida descrive le funzioni disponibili con le impostazioni predefinite."
        };

        internal void InstallMainGuide(FejdStartup menu)
        {
            try
            {
                if (menu.m_menuList == null || menu.m_menuList.transform.Find("MMOMOD_GuideEntry") != null) return;
                var buttons = menu.m_menuList.GetComponentsInChildren<Button>(true).Where(b => b.gameObject.activeSelf).ToArray();
                if (buttons.Length == 0) return;
                var template = buttons[buttons.Length - 1];
                guideEntry = Instantiate(template, template.transform.parent, false);
                guideEntry.name = "MMOMOD_GuideEntry";
                guideEntry.onClick = new Button.ButtonClickedEvent();
                guideEntry.onClick.AddListener(() => OpenMainGuide(menu));
                foreach (var label in guideEntry.GetComponentsInChildren<TMP_Text>(true)) label.text = L("Guida MMO MOD");
                foreach (var label in guideEntry.GetComponentsInChildren<UnityEngine.UI.Text>(true)) label.text = L("Guida MMO MOD");
                guideEntry.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
                if (template.GetComponentInParent<LayoutGroup>() == null)
                {
                    float step = buttons.Length > 1 ? Mathf.Abs(((RectTransform)buttons[0].transform).anchoredPosition.y - ((RectTransform)buttons[1].transform).anchoredPosition.y) : 45;
                    if (step < 20) step = 45;
                    // Make room above Exit without pushing it beyond the screen.
                    foreach (var button in buttons.Where(b => b != template)) ((RectTransform)button.transform).anchoredPosition += Vector2.up * step;
                    ((RectTransform)guideEntry.transform).anchoredPosition = ((RectTransform)template.transform).anchoredPosition + Vector2.up * step;
                }
                var all = menu.m_menuList.GetComponentsInChildren<Button>();
                AccessTools.Field(typeof(FejdStartup), "m_menuButtons").SetValue(menu, all);
                foreach (var button in all) button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                Logger.LogInfo("Guida MMO MOD aggiunta al menu principale.");
            }
            catch (Exception ex) { Logger.LogError("Guida menu: " + ex); }
        }
        private void OpenMainGuide(FejdStartup menu)
        {
            if (!LoadNativeAssets()) { Logger.LogWarning("Guida: pannelli nativi non disponibili."); return; }
            guideMenu = menu;
            if (guideRoot == null)
            {
                guideRoot = new GameObject("MMOMOD_Guide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                guideRoot.transform.SetParent(menu.transform, false);
                var canvas = guideRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
                var scaler = guideRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
                Stretch(Panel(guideRoot.transform, "Velo", 0, 0, 1920, 1080, null, new Color(0, 0, 0, .72f)).rectTransform);
                var window = Rect("Guida", guideRoot.transform, 0, 0, 1160, 800);
                window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f, .5f); window.anchoredPosition = Vector2.zero;
                Panel(window, "Legno", 0, 0, 1160, 800, wood, new Color(.62f, .58f, .53f));
                Text(window, L("Guida MMO MOD"), 48, 28, 900, 60, 38, true);
                Text(window, L("Tutto il necessario per la tua prossima avventura"), 50, 87, 1000, 35, 21);
                Button(window, L("X"), 1060, 35, 50, 46, CloseMainGuide);
                Panel(window, "Indice", 40, 145, 248, 560, inset, new Color(0, 0, 0, .48f));
                guideSections = new Button[GuideTitles.Length];
                for (int i = 0; i < GuideTitles.Length; i++)
                { int page = i; guideSections[i] = Button(window, L(GuideTitles[i]), 52, 163 + i * 73, 224, 54, () => ShowGuidePage(page)); }
                guideHeading = Text(window, "", 322, 152, 775, 53, 30, true);
                var viewport = Rect("GuideViewport", window, 322, 215, 775, 480);
                viewport.gameObject.AddComponent<RectMask2D>();
                viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f);
                guideScroll = viewport.gameObject.AddComponent<ScrollRect>(); guideScroll.horizontal = false; guideScroll.scrollSensitivity = 35; guideScroll.viewport = viewport;
                guideBody = Text(viewport, "", 0, 0, 750, 470, 22, false, TextAlignmentOptions.TopLeft);
                guideScroll.content = guideBody.rectTransform;
                Text(window, L("MMO MOD {0}     •     ESC per tornare al menu", PluginVersion), 50, 736, 670, 35, 18);
                Button(window, L("Torna al menu"), 818, 726, 285, 46, CloseMainGuide);
            }
            guideRoot.SetActive(true); menu.m_menuList.SetActive(false); ShowGuidePage(0); ShowModCursor();
            guideSections[0].Select();
        }
        private void ShowGuidePage(int page)
        {
            guidePage = page; guideHeading.text = L(GuideTitles[page]); guideBody.text = L(GuidePages[page]);
            guideBody.rectTransform.sizeDelta = new Vector2(750, Math.Max(470, guideBody.GetPreferredValues(guideBody.text, 750, 0).y + 20));
            guideScroll.verticalNormalizedPosition = 1;
            for (int i = 0; i < guideSections.Length; i++) guideSections[i].image.color = i == page ? new Color(1f, .82f, .55f) : Color.white;
        }
        private void CloseMainGuide()
        {
            if (guideRoot == null) return;
            guideRoot.SetActive(false); guideClosedFrame = Time.frameCount;
            if (guideMenu != null) guideMenu.m_menuList.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (guideEntry != null) guideEntry.Select();
        }
        private bool TickMainGuide()
        {
            if (guideRoot == null || !guideRoot.activeSelf) return false;
            ShowModCursor();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseMainGuide();
            return true;
        }
    }
    [HarmonyPatch(typeof(FejdStartup), "UpdateKeyboard")]
    internal static class MainGuideKeyboardPatch
    { [HarmonyPrefix] static bool Prefix() => MMOMODPlugin.Instance?.GuideBlocksInput != true; }
    [HarmonyPatch(typeof(FejdStartup), "UpdateGamepad")]
    internal static class MainGuideGamepadPatch
    { [HarmonyPrefix] static bool Prefix() => MMOMODPlugin.Instance?.GuideBlocksInput != true; }
}
