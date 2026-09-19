using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimMMOMOD
{
    [Serializable] public class LedgerEntry { public string date, description; public int amount; }
    [Serializable] public class QuestRecord
    {
        public string id, title, type, period;
        public int target, progress, reward, xpReward;
        public bool claimed;
    }
    [Serializable] public class MemberRecord
    {
        public string id, name, guild = "", role = "Membro";
        public int gold, xp, dailyChanges;
        public string changeDay = "";
        public List<string> invitations = new List<string>(), friends = new List<string>(), requests = new List<string>(), blocked = new List<string>(), recent = new List<string>();
        public List<QuestRecord> quests = new List<QuestRecord>();
        public List<LedgerEntry> ledger = new List<LedgerEntry>();
        public List<string> processed = new List<string>();
        public List<CoinGrant> grants = new List<CoinGrant>();
        public List<ResourceGrant> resourceGrants = new List<ResourceGrant>();
    }
    [Serializable] public class CoinGrant { public string id; public int amount; }
    [Serializable] public class GuildRecord { public string id, name, tag = ""; public int xp; }
    [Serializable] public class WorldStore
    {
        public CaravanState caravan = new CaravanState();
        public CaravanState village = new CaravanState();
        public List<MemberRecord> players = new List<MemberRecord>();
        public List<GuildRecord> guilds = new List<GuildRecord>();
    }
    [Serializable] public class Command
    {
        public string action, target = "", text = "", id = "";
        public int amount;
    }
    [Serializable] public class PublicMember { public string id, name, role; public bool online; }
    [Serializable] public class Snapshot
    {
        public CaravanState caravan;
        public CaravanState village;
        public string villageStatus;
        public bool economy, quests;
        public MemberRecord self;
        public GuildRecord guild;
        public List<PublicMember> people = new List<PublicMember>();
        public List<PublicMember> members = new List<PublicMember>();
        public List<GuildRecord> invitations = new List<GuildRecord>();
        public string message;
    }

    // Pure game rules: the host resolves the authenticated character before calling Execute.
    public sealed class GameService
    {
        public WorldStore Data;
        public int MaxMembers = 20, DailyReward = 100, LevelReward = 25, BaseKill = 10;
        public float KillMultiplier = 1.5f;
        public bool Economy = true, Quests = true;
        public GameService(WorldStore data) { Data = data ?? new WorldStore(); }
        public MemberRecord Player(string id) { return Data.players.Find(p => p.id == id); }
        public GuildRecord Guild(MemberRecord p) { return Data.guilds.Find(g => g.id == p.guild); }
        public MemberRecord Join(string id, string name, int initialGold, DateTime now)
        {
            var p = Player(id);
            if (p == null) { p = new MemberRecord { id = id, name = name, gold = Math.Max(0, initialGold) }; Data.players.Add(p); }
            p.name = name;
            EnsureQuests(p, now);
            return p;
        }
        public void Record(MemberRecord p, int amount, string description, DateTime now)
        {
            long next = (long)p.gold + amount;
            if (next < 0 || next > int.MaxValue) throw new InvalidOperationException("Saldo fuori limite.");
            p.gold = (int)next;
            p.ledger.Insert(0, new LedgerEntry { amount = amount, description = description, date = now.ToString("yyyy-MM-dd HH:mm") });
            if (p.ledger.Count > 100) p.ledger.RemoveRange(100, p.ledger.Count - 100);
        }
        public void EnsureQuests(MemberRecord p, DateTime now)
        {
            if (!Quests) return;
            var date = now.Date;
            string day = date.ToString("yyyy-MM-dd");
            string week = date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd");
            bool CurrentDaily(QuestRecord q) => q.id == "d:" + day || q.id.StartsWith("d:" + day + ":");
            if (p.changeDay != day) { p.changeDay = day; p.dailyChanges = 0; }
            p.quests.RemoveAll(q => !q.claimed && !CurrentDaily(q) && !q.id.StartsWith("w:" + week + ":"));
            for (int slot = 0; slot < 3; slot++)
            {
                string dailyId = "d:" + day + (slot == 0 ? "" : ":" + slot);
                if (p.quests.Any(q => q.id == dailyId)) continue;
                string[] types = { "kill", "gather", "explore", "level" };
                int n = (int)((date.Ticks / TimeSpan.TicksPerDay + slot) % 4);
                AddQuest(p, dailyId, types[n], new[] { 5, 30, 60, 2 }[n], DailyReward, "Giornaliera");
            }
            string[] weekly = { "kill", "gather", "explore" };
            for (int i = 0; i < weekly.Length; i++)
                if (!p.quests.Any(q => q.id == "w:" + week + ":" + i)) AddQuest(p, "w:" + week + ":" + i, weekly[i], new[] { 40, 250, 500 }[i], DailyReward * 5, "Settimanale");
            foreach (var q in p.quests.Where(q => !q.claimed && q.xpReward <= 0)) q.xpReward = QuestXP(q.type, q.target);
            var old = p.quests.Where(q => q.claimed && !CurrentDaily(q) && !q.id.StartsWith("w:" + week + ":")).ToList();
            foreach (var q in old.Take(Math.Max(0, old.Count - 50))) p.quests.Remove(q);
        }
        public static int QuestXP(string type, int target)
        { return (int)Math.Min(int.MaxValue, (long)target * (type == "kill" ? 20 : type == "gather" ? 2 : type == "explore" ? 1 : type == "level" ? 50 : 0)); }
        public static int PersonalLevel(int xp) { return 1 + (int)Math.Sqrt(Math.Max(0, xp) / 100.0); }
        private void AddQuest(MemberRecord p, string id, string type, int target, int reward, string period)
        {
            string verb = type == "kill" ? "Sconfiggi nemici" : type == "gather" ? "Raccogli risorse" : type == "explore" ? "Esplora nuove aree" : "Aumenta le abilità";
            p.quests.Add(new QuestRecord { id = id, title = verb, type = type, target = target, reward = reward, xpReward = QuestXP(type, target), period = period });
        }
        public string Execute(string actor, Command c, DateTime now, bool nearMerchant = false, string vendor = "wanderer")
        {
            var p = Player(actor);
            if (p == null) return "Attendi la sincronizzazione del personaggio.";
            EnsureQuests(p, now);
            var target = Player(c.target) ?? Data.players.SingleOrDefaultSafe(x => string.Equals(x.name, c.target, StringComparison.OrdinalIgnoreCase));
            var guild = Guild(p);
            switch (c.action)
            {
                case "trade":
                    if (!Economy) return "Economia disattivata sul server.";
                    if (!nearMerchant) return "Avvicinati al venditore di questo articolo (massimo 6 metri).";
                    var offer = VillageCatalog.Find(c.target);
                    if (offer == null || offer.Shop != vendor || string.IsNullOrEmpty(c.id)) return "Articolo non disponibile presso questo venditore.";
                    if (p.resourceGrants.Count != 0) return "Attendi la consegna dell'acquisto precedente.";
                    if (p.processed.Contains(c.id)) return "Acquisto già elaborato.";
                    if (p.gold < offer.Price) return "Oro insufficiente.";
                    Record(p, -offer.Price, ModText.Pack("{0}: {1} {2}", ModText.Term(offer.Shop == "wanderer" ? "Viandante" : "Borgo"), offer.Amount, ModText.Item(offer.Prefab)), now);
                    p.resourceGrants.Add(new ResourceGrant { id = c.id, prefab = offer.Prefab, label = offer.Name, amount = offer.Amount, unitPrice = offer.UnitPrice });
                    return "Acquisto registrato. Consegna in corso...";
                case "tradeAck":
                    var delivery = p.resourceGrants.Find(g => g.id == c.target);
                    if (delivery == null) return "";
                    if (c.amount < 0 || c.amount > delivery.amount) return "Consegna non valida.";
                    int refund = (delivery.amount - c.amount) * delivery.unitPrice;
                    if (refund > 0) Record(p, refund, "Rimborso mercante: articoli non consegnati", now);
                    p.resourceGrants.Remove(delivery);
                    return c.amount == 0 ? "Spazio insufficiente: oro rimborsato." : refund > 0 ? ModText.Pack("Ricevuti {0} {1}. Rimborsati {2} oro.", c.amount, ModText.Item(delivery.prefab), refund) : ModText.Pack("Ricevuti {0} {1}.", c.amount, ModText.Item(delivery.prefab));
                case "refresh": return "";
                case "createGuild":
                    if (guild != null) return "Appartieni già a una gilda.";
                    string name = (c.text ?? "").Trim();
                    if (name.Length < 3 || name.Length > 32) return "Nome gilda: da 3 a 32 caratteri.";
                    if (Data.guilds.Any(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase))) return "Nome gilda già utilizzato.";
                    guild = new GuildRecord { id = Guid.NewGuid().ToString("N"), name = name };
                    Data.guilds.Add(guild); p.guild = guild.id; p.role = "Capogilda";
                    return "Gilda creata.";
                case "inviteGuild":
                    if (guild == null || p.role == "Membro") return "Solo capogilda e ufficiali possono invitare.";
                    if (target == null || target == p) return "Seleziona un giocatore valido.";
                    if (target.guild != "") return "Il giocatore appartiene già a una gilda.";
                    if (Data.players.Count(x => x.guild == guild.id) >= MaxMembers) return "Gilda al completo.";
                    if (!target.invitations.Contains(guild.id)) target.invitations.Add(guild.id);
                    return "Invito alla gilda inviato.";
                case "acceptGuild":
                    var invited = Data.guilds.Find(g => g.id == c.target);
                    if (guild != null || invited == null || !p.invitations.Contains(c.target)) return "Invito non valido.";
                    if (Data.players.Count(x => x.guild == invited.id) >= MaxMembers) return "Gilda al completo.";
                    p.guild = invited.id; p.role = "Membro"; p.invitations.Clear(); return "Benvenuto nella gilda!";
                case "rejectGuild": p.invitations.Remove(c.target); return "Invito rifiutato.";
                case "leaveGuild":
                    if (guild == null) return "Non appartieni a una gilda.";
                    RemoveFromGuild(p); return "Hai lasciato la gilda.";
                case "promote": case "demote": case "kick":
                    if (guild == null || p.role != "Capogilda" || target == null || target == p || target.guild != p.guild) return "Solo il capogilda può gestire gli altri membri.";
                    if (c.action == "kick") RemoveFromGuild(target);
                    else target.role = c.action == "promote" ? "Ufficiale" : "Membro";
                    return "Membro aggiornato.";
                case "tag":
                    if (guild == null || p.role != "Capogilda") return "Solo il capogilda può cambiare il tag.";
                    if ((c.text ?? "").Length > 5) return "Il tag può contenere al massimo 5 caratteri.";
                    guild.tag = (c.text ?? "").Trim(); return "Tag aggiornato.";
                case "friendRequest":
                    if (target == null || target == p || p.blocked.Contains(target.id) || target.blocked.Contains(p.id)) return "Richiesta non disponibile.";
                    if (p.friends.Contains(target.id)) return "Siete già amici.";
                    if (!target.requests.Contains(p.id)) target.requests.Add(p.id); return "Richiesta di amicizia inviata.";
                case "friendAccept":
                    if (target == null || !p.requests.Contains(target.id) || p.blocked.Contains(target.id) || target.blocked.Contains(p.id)) return "Richiesta non valida.";
                    p.requests.Remove(target.id); target.requests.Remove(p.id);
                    if (!p.friends.Contains(target.id)) p.friends.Add(target.id);
                    if (!target.friends.Contains(p.id)) target.friends.Add(p.id); return "Amicizia accettata.";
                case "friendReject": p.requests.Remove(c.target); return "Richiesta rifiutata.";
                case "unfriend": case "block":
                    if (target == null || target == p) return "Giocatore non valido.";
                    p.friends.Remove(target.id); target.friends.Remove(p.id); p.requests.Remove(target.id); target.requests.Remove(p.id);
                    if (c.action == "block" && !p.blocked.Contains(target.id)) p.blocked.Add(target.id);
                    return c.action == "block" ? "Richieste della mod bloccate per questo giocatore." : "Amicizia rimossa.";
                case "unblock": p.blocked.Remove(c.target); return "Giocatore sbloccato.";
                case "transfer":
                    if (!Economy) return "Economia disattivata sul server.";
                    if (target == null || target == p) return "Destinatario non valido o nome ambiguo.";
                    if (c.amount <= 0 || c.amount > p.gold || (long)target.gold + c.amount > int.MaxValue) return "Importo non valido o saldo insufficiente.";
                    Record(p, -c.amount, ModText.Pack("Inviato a {0}", target.name), now); Record(target, c.amount, ModText.Pack("Ricevuto da {0}", p.name), now); return "Trasferimento completato.";
                case "deposit": case "withdraw":
                    if (!Economy || c.amount <= 0) return "Operazione non disponibile.";
                    if (c.action == "withdraw" && c.amount > p.gold) return "Saldo insufficiente.";
                    Record(p, c.action == "deposit" ? c.amount : -c.amount, c.action == "deposit" ? "Deposito monete" : "Prelievo monete", now);
                    if (c.action == "withdraw") p.grants.Add(new CoinGrant { id = c.id, amount = c.amount });
                    return "Saldo aggiornato.";
                case "withdrawAck":
                    var grant = p.grants.Find(x => x.id == c.target);
                    if (grant == null) return "";
                    int delivered = Math.Max(0, Math.Min(c.amount, grant.amount));
                    if (delivered < grant.amount) Record(p, grant.amount - delivered, "Rimborso: inventario pieno", now);
                    p.grants.Remove(grant); return delivered > 0 ? "Monete aggiunte all'inventario." : "Inventario pieno: oro rimborsato.";
                case "progress":
                    if (c.amount <= 0 || c.amount > 10000) return "Evento non valido.";
                    if (Economy && c.text == "kill") Record(p, (int)Math.Min(1000000, BaseKill * KillMultiplier * Math.Min(10, c.amount)), "Nemico sconfitto", now);
                    if (Economy && c.text == "level") Record(p, Math.Min(100, c.amount) * LevelReward, "Abilità migliorata", now);
                    if (guild != null && (c.text == "kill" || c.text == "level")) guild.xp = (int)Math.Min(int.MaxValue, (long)guild.xp + (c.text == "kill" ? 10 : 25));
                    if (Quests) foreach (var q in p.quests.Where(q => !q.claimed && q.type == c.text)) q.progress = Math.Min(q.target, q.progress + (c.text == "kill" ? 1 : c.amount));
                    return "";
                case "replaceQuest":
                    if (!Quests) return "Missioni disattivate sul server.";
                    var replaced = p.quests.Find(q => q.id == c.target);
                    string[] alternatives = { "kill", "gather", "explore", "level" };
                    int alternative = Array.IndexOf(alternatives, c.text);
                    if (replaced == null || replaced.period != "Giornaliera" || replaced.claimed || replaced.progress >= replaced.target)
                        return "Puoi cambiare solo una giornaliera ancora da completare.";
                    if (alternative < 0 || c.text == replaced.type) return "Scegli un'attività diversa.";
                    if (p.dailyChanges >= 3) return "Hai usato i tre cambi di oggi. Rinnovo alle 00:00 UTC.";
                    p.quests.Remove(replaced);
                    AddQuest(p, replaced.id, c.text, new[] { 3, 15, 30, 1 }[alternative], Math.Max(1, DailyReward / 2), "Giornaliera");
                    p.dailyChanges++;
                    return "Missione cambiata: obiettivo facile, progressi azzerati e ricompensa adeguata.";
                case "claim":
                    if (!Quests) return "Missioni disattivate sul server.";
                    var quest = p.quests.Find(q => q.id == c.target);
                    if (quest == null || quest.claimed || quest.progress < quest.target) return "Ricompensa non disponibile.";
                    if (Economy) Record(p, quest.reward, ModText.Pack("{0}: {1}", ModText.Term(quest.period), ModText.Term(quest.title)), now);
                    p.xp = (int)Math.Min(int.MaxValue, (long)p.xp + quest.xpReward); quest.claimed = true;
                    if (guild != null) guild.xp = (int)Math.Min(int.MaxValue, (long)guild.xp + 100);
                    return Economy ? ModText.Pack("Ricompensa riscattata: {0} EXP e {1} oro.", quest.xpReward, quest.reward) : ModText.Pack("Ricompensa riscattata: {0} EXP.", quest.xpReward);
                default: return "Comando sconosciuto.";
            }
        }
        private void RemoveFromGuild(MemberRecord p)
        {
            string id = p.guild; bool leader = p.role == "Capogilda";
            p.guild = ""; p.role = "Membro";
            var remaining = Data.players.Where(x => x.guild == id).ToList();
            if (remaining.Count == 0) { Data.guilds.RemoveAll(g => g.id == id); foreach (var x in Data.players) x.invitations.Remove(id); }
            else if (leader) (remaining.Find(x => x.role == "Ufficiale") ?? remaining[0]).role = "Capogilda";
        }
    }
    public static class EnumerableHelpers
    {
        public static T SingleOrDefaultSafe<T>(this IEnumerable<T> source, Func<T, bool> predicate) where T : class
        { var matches = source.Where(predicate).Take(2).ToList(); return matches.Count == 1 ? matches[0] : null; }
    }
}
