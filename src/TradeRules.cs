using System;
using System.Linq;

namespace ValheimMMOMOD
{
    [Serializable] public class CaravanState
    {
        public bool active, planned;
        public int placementVersion;
        public float x, y, z;
    }
    [Serializable] public class ResourceGrant
    {
        public string id, prefab, label;
        public int amount, unitPrice;
    }
    public sealed class ResourceOffer
    {
        public readonly string Prefab, Name, Shop;
        public readonly int Amount, UnitPrice;
        public int Price => Amount * UnitPrice;
        public ResourceOffer(string prefab, string name, int amount, int unitPrice, string shop = "wanderer")
        { Prefab = prefab; Name = name; Amount = amount; UnitPrice = unitPrice; Shop = shop; }
    }
    public static class WandererCatalog
    {
        public static readonly ResourceOffer[] Offers = {
            new ResourceOffer("Wood", "Legno", 10, 2),
            new ResourceOffer("Stone", "Pietra", 10, 2),
            new ResourceOffer("Flint", "Selce", 5, 5),
            new ResourceOffer("Resin", "Resina", 10, 3),
            new ResourceOffer("LeatherScraps", "Ritagli di cuoio", 5, 8),
            new ResourceOffer("DeerHide", "Pelle di cervo", 5, 12),
            new ResourceOffer("Coal", "Carbone", 10, 4),
            new ResourceOffer("RoundLog", "Legno massiccio", 10, 6),
            new ResourceOffer("FineWood", "Legno pregiato", 5, 10)
        };
        public static ResourceOffer Find(string prefab) { return Offers.FirstOrDefault(o => o.Prefab == prefab); }
    }
    public static class VillageCatalog
    {
        public static readonly ResourceOffer[] Food = {
            new ResourceOffer("Raspberry", "Lamponi", 5, 3, "food"),
            new ResourceOffer("Mushroom", "Funghi", 5, 4, "food"),
            new ResourceOffer("Honey", "Miele", 5, 6, "food"),
            new ResourceOffer("CookedMeat", "Carne di cinghiale cotta", 5, 10, "food"),
            new ResourceOffer("CookedDeerMeat", "Carne di cervo cotta", 5, 12, "food"),
            new ResourceOffer("NeckTailGrilled", "Coda di Neck grigliata", 5, 8, "food")
        };
        public static readonly ResourceOffer[] Equipment = {
            new ResourceOffer("Hammer", "Martello", 1, 35, "equipment"),
            new ResourceOffer("Hoe", "Zappa", 1, 45, "equipment"),
            new ResourceOffer("AxeStone", "Ascia di pietra", 1, 50, "equipment"),
            new ResourceOffer("AxeFlint", "Ascia di selce", 1, 90, "equipment"),
            new ResourceOffer("Club", "Clava", 1, 35, "equipment"),
            new ResourceOffer("SpearFlint", "Lancia di selce", 1, 80, "equipment"),
            new ResourceOffer("Bow", "Arco grezzo", 1, 120, "equipment"),
            new ResourceOffer("ShieldWood", "Scudo di legno", 1, 65, "equipment"),
            new ResourceOffer("ArrowWood", "Frecce di legno", 20, 1, "equipment"),
            new ResourceOffer("ArmorRagsChest", "Tunica di stracci", 1, 40, "equipment"),
            new ResourceOffer("ArmorRagsLegs", "Pantaloni di stracci", 1, 40, "equipment"),
            new ResourceOffer("ArmorLeatherChest", "Tunica di cuoio", 1, 110, "equipment"),
            new ResourceOffer("ArmorLeatherLegs", "Pantaloni di cuoio", 1, 90, "equipment"),
            new ResourceOffer("HelmetLeather", "Elmo di cuoio", 1, 75, "equipment"),
            new ResourceOffer("CapeDeerHide", "Mantello di pelle di cervo", 1, 70, "equipment")
        };
        public static ResourceOffer Find(string prefab)
        { return WandererCatalog.Find(prefab) ?? Food.Concat(Equipment).FirstOrDefault(o => o.Prefab == prefab); }
    }
}
