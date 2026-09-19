using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ValheimMMOMOD
{
    public partial class MMOMODPlugin
    {
        private GameObject nativeRoot;
        private RectTransform frame, content, navigation;
        private TMP_Text statusText, balanceText;
        private ScrollRect scroll;
        private TMP_FontAsset bodyFont, titleFont;
        private Material bodyMaterial, titleMaterial;
        private Sprite wood, inset, buttonSprite, fieldSprite, ornament;
        private ColorBlock nativeButtonColors;
        private SpriteState nativeButtonSprites;
        private Selectable.Transition nativeButtonTransition;
        private bool uiDirty;
        private int tab, sub;
        private float rowY;
        private const float ContentWidth = 770;
        private readonly Dictionary<string, string> inputs = new Dictionary<string, string>();
        private readonly Color gold = new Color(1f, 0.64f, 0f), cream = new Color(0.94f, 0.90f, 0.78f);
        private readonly string[] tabs = { "Gilda", "Economia", "Missioni", "Amici", "Mappa" };
        private readonly string[][] pages = {
            new[] { "La tua gilda", "Membri e ruoli", "Inviti", "Impostazioni" },
            new[] { "Tesoreria", "Trasferisci", "Movimenti" },
            new[] { "Giornaliere", "Settimanali", "Completate" },
            new[] { "Lista amici", "Richieste", "Giocatori recenti", "Bloccati" },
            new[] { "Esplorazione", "Segnaposti", "Condivisione" }
        };
        private static string Hierarchy(Transform t) { return t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name; }
        private bool LoadNativeAssets()
        {
            var images = Resources.FindObjectsOfTypeAll<Image>().Where(i => i.sprite != null && !Hierarchy(i.transform).Contains("MMOMOD_Native")).ToArray();
            Func<string[], Sprite> find = names => names.Select(n => images.FirstOrDefault(i => i.sprite.name == n)?.sprite).FirstOrDefault(s => s != null);
            wood = find(new[] { "woodpanel_inventory", "woodpanel_512x512", "woodpanel_serverlist" });
            inset = find(new[] { "item_background", "Background" }); fieldSprite = find(new[] { "text_field" });
            ornament = find(new[] { "BraidLineHorisontalMedium", "panel_separator" });
            var b = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(x => x.image != null && x.image.sprite != null && x.image.sprite.name == "button" && !Hierarchy(x.transform).Contains("MMOMOD_Native"));
            if (b != null) { buttonSprite = b.image.sprite; nativeButtonColors = b.colors; nativeButtonSprites = b.spriteState; nativeButtonTransition = b.transition; }
            var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            bodyFont = fonts.FirstOrDefault(f => f.name == "Valheim-AveriaSansLibre"); titleFont = fonts.FirstOrDefault(f => f.name == "Valheim-AveriaSerifLibre") ?? bodyFont;
            var labels = Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t => t.font != null && !Hierarchy(t.transform).Contains("MMOMOD_Native")).ToArray();
            bodyMaterial = labels.FirstOrDefault(t => t.font == bodyFont && !Hierarchy(t.transform).Contains("_Console"))?.fontSharedMaterial;
            titleMaterial = labels.FirstOrDefault(t => t.font == titleFont && t.fontSize >= 25)?.fontSharedMaterial;
            return wood != null && buttonSprite != null && bodyFont != null;
        }
        private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }
        private void Stretch(RectTransform r, float padding = 0)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(padding, padding); r.offsetMax = new Vector2(-padding, -padding); }
        private Image Panel(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
        {
            var r = Rect(name, parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color; return image;
        }
        private TMP_Text Text(Transform parent, string text, float x, float y, float w, float h, float size = 20, bool heading = false, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var r = Rect("Text", parent, x, y, w, h); var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = heading ? titleFont : bodyFont;
            if ((heading ? titleMaterial : bodyMaterial) != null) t.fontSharedMaterial = heading ? titleMaterial : bodyMaterial;
            t.text = text; t.fontSize = size; t.color = heading ? gold : cream; t.alignment = align; t.richText = false; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Ellipsis; t.margin = new Vector4(1, 0, 1, 0); return t;
        }
        private Button Button(Transform parent, string text, float x, float y, float w, float h, Action action, bool enabled = true)
        {
            var image = Panel(parent, text, x, y, w, h, buttonSprite, Color.white); var b = image.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            b.colors = nativeButtonColors; b.spriteState = nativeButtonSprites; b.transition = nativeButtonTransition; b.interactable = enabled;
            b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var label = Text(b.transform, text, 8, 2, w - 16, h - 4, 21, true, TextAlignmentOptions.Center);
            label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 21;
            b.onClick.AddListener(() => { action(); uiDirty = true; }); return b;
        }
        private TMP_InputField Field(string key, string placeholder, float x, float y, float w, bool numeric = false)
        {
            var image = Panel(content, "Input_" + key, x, y, w, 44, fieldSprite ?? inset, Color.white);
            var viewport = Rect("Viewport", image.transform, 10, 4, w - 20, 36); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, w - 20, 36, 21); var hint = Text(viewport, placeholder, 0, 0, w - 20, 36, 19); hint.color = new Color(.7f, .68f, .62f);
            var field = image.gameObject.AddComponent<TMP_InputField>(); field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)label; field.placeholder = hint; field.targetGraphic = image;
            field.fontAsset = bodyFont; field.characterLimit = numeric ? 10 : 40; field.contentType = numeric ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.Standard;
            string value; field.text = inputs.TryGetValue(key, out value) ? value : ""; field.onValueChanged.AddListener(v => inputs[key] = v); return field;
        }
        private string Value(string key) { string value; return inputs.TryGetValue(key, out value) ? value.Trim() : ""; }
        private void BuildNativeUI()
        {
            if (nativeRoot != null) { nativeRoot.SetActive(true); uiDirty = true; return; }
            if (!LoadNativeAssets()) { Feedback(L("Caricamento interfaccia originale di Valheim...")); return; }
            nativeRoot = new GameObject("MMOMOD_Native", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = nativeRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scale = nativeRoot.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = 1;
            var shade = Panel(nativeRoot.transform, "Dimmer", 0, 0, 1920, 1080, null, new Color(0, 0, 0, .45f)); Stretch(shade.rectTransform);
            frame = Rect("GuildWindow", nativeRoot.transform, 0, 0, 1120, 770); frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            Panel(frame, "NativeWood", 0, 0, 1120, 770, wood, new Color(.62f, .58f, .53f));
            Text(frame, localShopOpen ? L("Bottega del mercante") : L("Gilde e avventure"), 55, 28, 780, 60, 38, true);
            balanceText = Text(frame, "", 800, 45, 190, 35, 24, true, TextAlignmentOptions.MidlineRight);
            Button(frame, L("X"), 1025, 33, 50, 46, CloseNativeMenu);
            if (ornament != null) { var o = Panel(frame, "Intreccio", 50, 99, 1020, 12, ornament, gold); o.type = Image.Type.Simple; o.raycastTarget = false; }
            for (int i = 0; !localShopOpen && i < tabs.Length; i++) { int n = i; Button(frame, L(tabs[i]), 52 + i * 204, 125, 196, 46, () => { tab = n; sub = 0; scroll.verticalNormalizedPosition = 1; RenderPage(); }); }
            if (!localShopOpen) Panel(frame, "Sidebar", 42, 190, 238, 500, inset, new Color(0, 0, 0, .50f));
            navigation = Rect("Navigation", frame, 53, 203, 216, 470);
            var viewport = Rect("PageViewport", frame, localShopOpen ? 175 : 308, localShopOpen ? 150 : 203, ContentWidth, 470); viewport.gameObject.AddComponent<RectMask2D>();
            var scrollBg = viewport.gameObject.AddComponent<Image>(); scrollBg.color = new Color(0, 0, 0, .01f);
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport;
            content = Rect("Content", viewport, 0, 0, ContentWidth, 470); scroll.content = content;
            statusText = Text(frame, "", 56, 698, 1008, 40, 18);
            Text(frame, localShopOpen ? L("ESC  Chiudi     •     Rotella  Scorri     •     Acquisti solo presso il venditore") : L("F10  Apri / chiudi     •     ESC  Chiudi     •     Rotella  Scorri"), 55, 736, 1000, 22, 16, false, TextAlignmentOptions.Center);
            RenderPage(); Logger.LogInfo("UI nativa: " + wood.name + ", " + buttonSprite.name + ", " + bodyFont.name);
        }
        private void DestroyNativeUI() { if (nativeRoot != null) Destroy(nativeRoot); nativeRoot = null; }
        private void UpdateNativeUI()
        {
            if (localShopOpen && !IsLocalShopAvailable()) CloseNativeMenu();
            if (!MenuOpen) { if (nativeRoot != null && nativeRoot.activeSelf) nativeRoot.SetActive(false); return; }
            if (nativeRoot == null) { BuildNativeUI(); return; }
            if (!nativeRoot.activeSelf) nativeRoot.SetActive(true);
            if (uiDirty && !nativeRoot.GetComponentsInChildren<TMP_InputField>().Any(f => f.isFocused)) RenderPage();
            if (balanceText != null) balanceText.text = ready ? L("{0} oro", snapshot.self.gold.ToString("N0")) : "MMOMOD " + PluginVersion;
            if (statusText != null) statusText.text = Time.unscaledTime < feedbackUntil ? feedback : !ready ? (Player.m_localPlayer == null ? L("Entra in un mondo per usare gilde, economia, missioni e amici.") : L("Sincronizzazione: installa la stessa versione della mod su host e giocatori.")) : L("{0}  •  Dati sincronizzati con il mondo", snapshot.self.name);
        }
        private void Clear(Transform parent) { for (int i = parent.childCount - 1; i >= 0; i--) { var child = parent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); } }
        private void RenderPage()
        {
            if (content == null) return; uiDirty = false; float scrollPosition = scroll.verticalNormalizedPosition;
            Clear(content); Clear(navigation); rowY = 0;
            for (int i = 0; !localShopOpen && i < pages[tab].Length; i++) { int n = i; var b = Button(navigation, L(pages[tab][i]), 0, i * 55, 216, 45, () => { sub = n; scroll.verticalNormalizedPosition = 1; RenderPage(); }); if (i == sub) b.image.color = new Color(1f, .82f, .55f); }
            if (!ready)
            {
                Heading(L(pages[tab][sub])); Paragraph(Player.m_localPlayer == null ? L("Le tue avventure continuano nel mondo di Valheim.\n\nScegli un personaggio e un mondo per accedere alle funzioni.") : L("In attesa del server.\nLa stessa versione della mod deve essere presente sull'host e sui giocatori."), 140);
            }
            else if (localShopOpen) LocalShopPage(); else if (tab == 0) GuildPage(); else if (tab == 1) EconomyPage(); else if (tab == 2) QuestPage(); else if (tab == 3) FriendsPage(); else MapPage();
            content.sizeDelta = new Vector2(ContentWidth, Math.Max(470, rowY + 24)); scroll.verticalNormalizedPosition = scrollPosition;
        }
        private void Heading(string text) { Text(content, text, 0, rowY, ContentWidth - 12, 45, 29, true); rowY += 56; }
        private void Paragraph(string text, float height = 48) { var label = Text(content, text, 0, rowY, ContentWidth - 18, height, 20); height = Mathf.Max(height, label.GetPreferredValues(text, ContentWidth - 18, 0).y + 8); label.rectTransform.sizeDelta = new Vector2(ContentWidth - 18, height); rowY += height + 12; }
        private void Act(string label, Action action, bool enabled = true) { Button(content, label, 0, rowY, 320, 44, action, enabled); rowY += 57; }
        private void Cmd(string action, string target = "", string text = "", int amount = 0) { Send(new Command { action = action, target = target, text = text, amount = amount }); }
        private void GuildPage()
        {
            var guild = snapshot.guild; var self = snapshot.self;
            if (sub == 0)
            {
                Heading(guild?.name ?? L("Fondare una gilda"));
                if (guild == null)
                {
                    Paragraph(L("Raduna i tuoi compagni sotto un unico nome. Gli inviti vengono accettati dai destinatari."), 62);
                    Field("guild", L("Nome della gilda"), 0, rowY, 420); Button(content, L("Crea gilda"), 438, rowY, 260, 44, () => Cmd("createGuild", text: Value("guild"))); rowY += 66;
                    if (snapshot.invitations.Count > 0) Paragraph(L("Hai {0} inviti: apri la sezione Inviti.", snapshot.invitations.Count));
                }
                else
                {
                    Paragraph(L("Tag: {0}    •    {1} membri\nIl tuo ruolo: {2}", (guild.tag == "" ? L("Non impostato") : "[" + guild.tag + "]"), snapshot.members.Count, L(self.role)), 70);
                    int level = 1 + Mathf.FloorToInt(Mathf.Sqrt(guild.xp / 1000f));
                    Paragraph(L("Livello gilda {0}    •    {1} / {2} esperienza", level, guild.xp, ((long)level * level * 1000)), 38);
                    Heading(L("Compagni della gilda"));
                    foreach (var m in snapshot.members) Person(m, m.role, null);
                }
            }
            if (sub == 1)
            {
                Heading(L("Membri e ruoli"));
                if (guild == null) { Paragraph(L("Crea una gilda o accetta un invito.")); return; }
                foreach (var m in snapshot.members)
                {
                    float y = rowY; Person(m, m.role, null);
                    if (self.role == "Capogilda" && m.id != self.id)
                    { Button(content, m.role == "Ufficiale" ? L("Retrocedi") : L("Promuovi"), 430, y + 8, 154, 38, () => Cmd(m.role == "Ufficiale" ? "demote" : "promote", m.id)); Button(content, L("Espelli"), 598, y + 8, 145, 38, () => Confirm(L("Espellere {0}?", m.name), () => Cmd("kick", m.id))); }
                }
            }
            if (sub == 2)
            {
                Heading(L("Inviti alla gilda"));
                foreach (var g in snapshot.invitations)
                { Paragraph(g.name, 30); Button(content, L("Accetta"), 0, rowY, 180, 42, () => Cmd("acceptGuild", g.id)); Button(content, L("Rifiuta"), 195, rowY, 180, 42, () => Cmd("rejectGuild", g.id)); rowY += 60; }
                if (guild == null) { if (snapshot.invitations.Count == 0) Paragraph(L("Nessun invito ricevuto.")); return; }
                Paragraph(L("Invita un personaggio conosciuto in questo mondo."));
                foreach (var p in snapshot.people.Where(p => p.id != self.id && !snapshot.members.Any(m => m.id == p.id))) Person(p, "", () => Cmd("inviteGuild", p.id), "Invita", self.role != "Membro");
            }
            if (sub == 3)
            {
                Heading(L("Impostazioni della gilda")); if (guild == null) { Paragraph(L("Non appartieni a una gilda.")); return; }
                Paragraph(L("Tag visibile sui nomi dei membri, massimo 5 caratteri."));
                Field("tag", guild.tag == "" ? L("TAG") : guild.tag, 0, rowY, 240); Button(content, L("Salva tag"), 257, rowY, 250, 44, () => Cmd("tag", text: Value("tag")), self.role == "Capogilda"); rowY += 73;
                Act(showTag.Value ? L("Tag giocatori: visibile") : L("Tag giocatori: nascosto"), () => showTag.Value = !showTag.Value);
                Paragraph(L("Se il capogilda esce, la guida passa a un ufficiale o al primo membro disponibile."), 65);
                Act(L("Lascia la gilda"), () => Confirm(L("Vuoi lasciare la gilda?"), () => Cmd("leaveGuild")));
            }
        }
        private void Person(PublicMember person, string detail, Action action, string actionLabel = "Aggiungi", bool enabled = true)
        {
            Panel(content, "Member", 0, rowY, ContentWidth - 20, 66, inset, new Color(0, 0, 0, .40f));
            var dot = Text(content, "•", 14, rowY + 7, 26, 42, 26); dot.color = person.online ? new Color(.5f, .82f, .38f) : new Color(.55f, .52f, .46f);
            Text(content, person.name, 47, rowY + 3, 365, 31, 23, true);
            Text(content, (person.online ? L("In questo mondo") : L("Offline")) + (detail == "" ? "" : "  •  " + L(detail)), 47, rowY + 33, 365, 25, 17);
            if (action != null) Button(content, L(actionLabel), 580, rowY + 12, 154, 40, action, enabled); rowY += 78;
        }
        private void EconomyPage()
        {
            Heading(sub == 0 ? L("La tua tesoreria") : sub == 1 ? L("Trasferisci oro") : L("Storico dei movimenti"));
            if (!snapshot.economy) { Paragraph(L("L'economia è disattivata nella configurazione del server.")); return; }
            if (sub == 0)
            {
                Paragraph(L("{0} oro nel conto\n{1} monete nell'inventario", snapshot.self.gold.ToString("N0"), CoinCount), 78);
                Act(L("Deposita tutte le monete"), Deposit, CoinCount > 0);
                Paragraph(L("Ritira monete fisiche da usare nel mondo."));
                Field("withdraw", L("Quantità"), 0, rowY, 230, true); Button(content, L("Ritira"), 248, rowY, 260, 44, () => Withdraw(Value("withdraw"))); rowY += 65;
                Paragraph(L("Il prelievo non riuscito viene rimborsato. I trasferimenti sono registrati dal server."), 70);
            }
            else if (sub == 1)
            {
                Paragraph(L("Invia oro a un personaggio conosciuto in questo mondo, anche se è offline."), 64);
                Field("transfer", L("Quantità da inviare"), 0, rowY, 350, true); rowY += 66;
                foreach (var person in snapshot.people.Where(p => p.id != snapshot.self.id)) Person(person, "", () => { int n; if (!int.TryParse(Value("transfer"), out n) || n <= 0) Feedback(L("Inserisci un importo valido.")); else Confirm(L("Inviare {0} oro a {1}?", n, person.name), () => Cmd("transfer", person.id, amount: n)); }, "Invia oro");
            }
            else
            {
                if (snapshot.self.ledger.Count == 0) Paragraph(L("Nessun movimento registrato."));
                foreach (var entry in snapshot.self.ledger)
                { Panel(content, "Transaction", 0, rowY, 750, 70, inset, new Color(0, 0, 0, .35f)); Text(content, LocalizedMessage(entry.description), 12, rowY + 4, 560, 31, 21); Text(content, L("{0} UTC", entry.date), 12, rowY + 36, 450, 24, 16); var t = Text(content, (entry.amount >= 0 ? "+" : "") + entry.amount, 590, rowY + 12, 140, 40, 24, true, TextAlignmentOptions.MidlineRight); t.color = entry.amount >= 0 ? new Color(.64f, .85f, .4f) : new Color(1, .55f, .4f); rowY += 81; }
            }
        }
        private void QuestPage()
        {
            Heading(L(pages[2][sub]));
            Paragraph(L("Livello avventuriero {0}  •  {1} EXP personali", GameService.PersonalLevel(snapshot.self.xp), snapshot.self.xp), 42);
            if (sub == 0) Paragraph(L("3 giornaliere • Cambi rimasti: {0}/3. Il cambio azzera i progressi della missione.", Math.Max(0, 3 - snapshot.self.dailyChanges)), 60);
            if (!snapshot.quests) { Paragraph(L("Le missioni sono disattivate nella configurazione del server.")); return; }
            Paragraph(sub == 2 ? L("Le ricompense già riscosse restano nel tuo diario.") : (sub == 1 ? L("Progressi automatici in gioco. Rinnovo il lunedì alle 00:00 UTC.") : L("Progressi automatici in gioco. Rinnovo alle 00:00 UTC.")), 56);
            var quests = snapshot.self.quests.Where(q => sub == 2 ? q.claimed : !q.claimed && q.period == (sub == 0 ? "Giornaliera" : "Settimanale")).ToList();
            if (quests.Count == 0) Paragraph(sub == 2 ? L("Nessuna ricompensa riscattata.") : L("Hai completato le missioni di questo periodo."));
            foreach (var q in quests)
            {
                Panel(content, "Quest", 0, rowY, 750, 195, inset, new Color(0, 0, 0, .42f));
                Text(content, L(q.title), 16, rowY + 10, 510, 36, 25, true); Text(content, q.progress + " / " + q.target, 566, rowY + 12, 165, 32, 20, false, TextAlignmentOptions.MidlineRight);
                Panel(content, "ProgressTrack", 18, rowY + 61, 711, 12, fieldSprite ?? inset, Color.white);
                Panel(content, "ProgressFill", 20, rowY + 63, 707 * Mathf.Clamp01((float)q.progress / q.target), 8, null, gold).raycastTarget = false;
                Text(content, L("{0}{1} EXP  •  {2}", (snapshot.economy ? L("{0} oro", q.reward) + "  •  " : ""), q.xpReward, L(q.period)), 18, rowY + 90, 450, 34, 20);
                Button(content, q.claimed ? L("Riscossa") : L("Riscatta"), 558, rowY + 90, 170, 40, () => Cmd("claim", q.id), !q.claimed && q.progress >= q.target); 
                if (!q.claimed && q.period == "Giornaliera") Button(content, L("Cambia attività"), 18, rowY + 140, 235, 38, () => ChooseQuest(q), q.progress < q.target && snapshot.self.dailyChanges < 3);
                rowY += 210;
            }
        }
        private void ChooseQuest(QuestRecord quest)
        {
            var cover = Panel(frame, "QuestChoice", 0, 0, 1120, 770, null, new Color(0, 0, 0, .8f));
            var box = Panel(cover.transform, "QuestChoicePanel", 260, 150, 600, 460, wood, new Color(.62f, .58f, .53f));
            Text(box.transform, L("Scegli una missione più facile"), 25, 20, 550, 45, 27, true);
            Text(box.transform, L("Perdi i progressi attuali. Consuma 1 cambio.\nL'EXP si riscuote al completamento."), 25, 70, 550, 70, 20);
            string[] types = { "kill", "gather", "explore", "level" };
            string[] labels = { "3 nemici • 60 EXP", "15 risorse • 30 EXP", "30 nuove aree • 30 EXP", "1 livello abilità • 50 EXP" };
            for (int i = 0; i < types.Length; i++)
            {
                string type = types[i];
                Button(box.transform, L(labels[i]), 35, 150 + i * 53, 530, 43, () => { Destroy(cover.gameObject); Cmd("replaceQuest", quest.id, type); }, type != quest.type);
            }
            Button(box.transform, L("Annulla"), 170, 375, 260, 43, () => Destroy(cover.gameObject));
        }
        private void FriendsPage()
        {
            Heading(L(pages[3][sub])); var self = snapshot.self;
            if (sub == 3) Paragraph(L("Il blocco impedisce le richieste di amicizia della mod."), 52);
            var ids = sub == 0 ? self.friends : sub == 1 ? self.requests : sub == 2 ? self.recent : self.blocked;
            var people = snapshot.people.Where(p => p.id != self.id && (ids.Contains(p.id) || (sub == 2 && p.online))).ToList();
            if (people.Count == 0) Paragraph(sub == 2 ? L("I giocatori incontrati qui appariranno in questa lista.") : L("La lista è vuota."));
            foreach (var p in people)
            {
                Person(p, "", () => Cmd(sub == 0 ? "unfriend" : sub == 1 ? "friendAccept" : sub == 2 ? "friendRequest" : "unblock", p.id), sub == 0 ? "Rimuovi" : sub == 1 ? "Accetta" : sub == 2 ? "Aggiungi" : "Sblocca");
                if (sub == 1 || sub == 2) { Button(content, sub == 1 ? L("Rifiuta") : L("Blocca richieste"), 475, rowY - 6, 259, 36, () => Cmd(sub == 1 ? "friendReject" : "block", p.id)); rowY += 43; }
            }
        }
        private void MapPage()
        {
            Heading(L(pages[4][sub]));
            if (sub == 0)
            {
                Paragraph(L("La mappa originale di Valheim conserva nebbia, biomi e segnaposti. Le aree ricevute dalla gilda vengono integrate nella stessa mappa."), 100);
                Act(L("Apri mappa del mondo"), () => OpenWorldMap());
                Act(L("Condividi con la gilda"), () => ShareMap(), snapshot.guild != null);
            }
            else if (sub == 1)
            {
                Paragraph(L("Aggiungi un segnaposto nella tua posizione attuale."), 42);
                Field("pin", L("Nome del segnaposto"), 0, rowY, 425); Button(content, L("Aggiungi"), 444, rowY, 270, 44, () => {
                    if (Value("pin") == "") { Feedback(L("Inserisci un nome per il segnaposto.")); return; }
                    Minimap.instance?.AddPin(Player.m_localPlayer.transform.position, Minimap.PinType.Icon0, Value("pin"), true, false, Player.m_localPlayer.GetPlayerID(), default(Splatform.PlatformUserID)); Feedback(L("Segnaposto aggiunto alla mappa."));
                }); rowY += 70;
                var pins = Minimap.instance == null ? null : AccessTools.Field(typeof(Minimap), "m_pins")?.GetValue(Minimap.instance) as List<Minimap.PinData>;
                if (pins != null) foreach (var pin in pins.Where(p => p.m_save && (p.m_ownerID == 0 || p.m_ownerID == Player.m_localPlayer.GetPlayerID())).ToList())
                { Text(content, pin.m_name + "  (" + Mathf.RoundToInt(pin.m_pos.x) + ", " + Mathf.RoundToInt(pin.m_pos.z) + ")", 0, rowY, 510, 45, 21); Button(content, L("Rimuovi"), 560, rowY, 170, 40, () => Confirm(L("Rimuovere il segnaposto {0}?", pin.m_name), () => Minimap.instance.RemovePin(pin))); rowY += 56; }
            }
            else
            {
                Paragraph(L("La mappa viene inviata solo ai membri della tua gilda collegati a questo mondo. Gli altri giocatori devono avere la mod."), 90);
                Act(L("Condividi adesso"), () => ShareMap(), snapshot.guild != null);
                Act(autoShare.Value ? L("Condivisione automatica: sì") : L("Condivisione automatica: no"), () => autoShare.Value = !autoShare.Value);
                Paragraph(L("Intervallo: {0} secondi.", Math.Max(30, shareInterval.Value)));
            }
        }
        private void Confirm(string question, Action action)
        {
            var cover = Panel(frame, "Confirmation", 0, 0, 1120, 770, null, new Color(0, 0, 0, .75f));
            var box = Panel(cover.transform, "NativeDialog", 270, 255, 580, 230, wood, Color.white);
            Text(box.transform, question, 35, 30, 510, 90, 26, true, TextAlignmentOptions.Center);
            Button(box.transform, L("Conferma"), 55, 149, 220, 45, () => { Destroy(cover.gameObject); action(); });
            Button(box.transform, L("Annulla"), 303, 149, 220, 45, () => Destroy(cover.gameObject));
        }
        public string TagFor(string name)
        { return showTag.Value && ready && snapshot.guild != null && snapshot.members.Any(p => p.name == name) ? snapshot.guild.tag : ""; }
    }
    [HarmonyPatch(typeof(Player), "GetHoverText")]
    internal static class GuildTagPatch
    { [HarmonyPostfix] static void Postfix(Player __instance, ref string __result) { string tag = MMOMODPlugin.Instance?.TagFor(__instance.GetPlayerName()); if (!string.IsNullOrEmpty(tag)) __result = "[" + tag + "] " + __result; } }
}
