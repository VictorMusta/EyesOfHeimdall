using UnityEngine;

namespace EyesOfHeimdall.ValheimMod;

/// <summary>
/// Valheim-specific: turns a Character's prefab name into a stable category key + French label.
/// Matched by prefix against the GameObject name (e.g. "Greydwarf_Shaman(Clone)") so every
/// variant of a family (Skeleton_NoArcher, Skeleton_Mountains, ...) lands in the same bucket
/// without needing an exhaustive list. Built from a full scan of ZNetScene.instance.m_prefabs
/// (TrophyIcons.DumpAllCreatures, F10) covering every creature in the installed game version —
/// base game plus Mistlands/Ashlands/Deep North content — not just the ones encountered so far.
///
/// Order matters: first prefix match wins, so a family's specific sub-variant (e.g.
/// "Fenring_Cultist") must be listed before its more generic relative ("Fenring") when one is a
/// prefix of the other.
/// </summary>
internal static class CreatureCatalog
{
    private static readonly (string Prefix, string Category, string Label)[] Families =
    {
        // --- Bosses & boss-adjacent (checked first: some share a prefix with a regular family) ---
        ("GoblinKing", "boss", "Yagluth"),
        ("Eikthyr", "boss", "Eikthyr"),
        ("Bonemass", "boss", "Bonemass"),
        ("Moder", "boss", "Moder"),
        ("gd_king", "boss", "L'Ancien"),
        ("Dragon", "boss", "Reine dragon"),
        ("FrozenKing", "boss", "Roi gelé"),
        ("Writhan", "boss", "Writhan"),
        ("Aspect_", "boss", "Aspect de boss"),

        // --- Meadows / farm / wildlife ---
        ("Boar", "boar", "Sanglier"),
        ("Deer", "deer", "Cerf"),
        ("Neck", "neck", "Neck"),
        ("Chicken", "farm", "Poule"),
        ("Hen", "farm", "Poule"),
        ("Hare", "hare", "Lièvre"),
        ("Seagull", "seagull", "Mouette"),

        // --- Black Forest ---
        ("Greydwarf", "goblin", "Gobelin"),
        ("Greyling", "goblin", "Bourgeon nain-gris"),
        ("Skeleton", "skeleton", "Squelette"),
        ("Troll", "troll", "Troll"),

        // --- Swamp ---
        ("Draugr", "draugr", "Draugr"),
        ("Leech", "leech", "Sangsue"),
        ("Blob", "blob", "Gelée"),
        ("Surtling", "surtling", "Surtling"),

        // --- Mountain ---
        ("Wolf", "wolf", "Loup"),
        ("Ulv", "wolf", "Ulv"),
        ("StoneGolem", "golem", "Golem de pierre"),
        ("Hatchling", "drake", "Chauve-dragon"),
        ("Fenring_Cultist", "cultist", "Culte de Fenring"),
        ("Fenring", "fenring", "Fenring"),
        ("Moose", "moose", "Élan"),
        ("Bat", "bat", "Chauve-souris"),

        // --- Plains ---
        ("Goblin", "fuling", "Fuling"),
        ("Deathsquito", "deathsquito", "Moustique"),
        ("Lox", "lox", "Lox"),

        // --- Ocean ---
        ("BonemawSerpent", "serpent", "Serpent des cendres"),
        ("Serpent", "serpent", "Serpent"),
        ("Seal", "seal", "Phoque"),

        // --- Mistlands ---
        ("Seeker", "seeker", "Seeker"),
        ("Hive", "seeker", "Ruche"),
        ("TheHive", "seeker", "Ruche"),
        ("Tick", "tick", "Tique"),
        ("Dverger", "dvergr", "Dvergr"),
        ("Gjall", "gjall", "Gjall"),
        ("Tendril", "tentaroot", "Tentacule"),
        ("TentaRoot", "tentaroot", "Racine tentaculaire"),
        ("staff_greenroots_tentaroot", "tentaroot", "Racine tentaculaire"),
        ("Fader", "fader", "Fader"),

        // --- Deep North ---
        ("FrostWisp", "wisp", "Feu follet glacé"),
        ("Frysling", "frysling", "Frysling"),
        ("ElakingMole", "mole", "Taupe"),
        ("Elaking", "elaking", "Elaking"),

        // --- Ashlands ---
        ("Asksvin", "asksvin", "Asksvin"),
        ("Barka", "barka", "Barka"),
        ("Bjorn", "bjorn", "Bjorn"),
        ("Unbjorn", "bjorn", "Bjorn mort-vivant"),
        ("Abomination", "abomination", "Abomination"),
        ("Charred", "charred", "Légion carbonisée"),
        ("Volture", "volture", "Vautour"),
        ("Morgen", "morgen", "Morgen"),
        ("Jotun", "jotun", "Jotun"),
        ("BogWitchKvastur", "kvastur", "Sorcière des tourbières"),
        ("Ghost", "ghost", "Fantôme"),
        ("ShadowPerson", "shadow", "Ombre"),
        ("Wraith", "wraith", "Wraith"),
        ("Fallen", "fallen", "Déchu"),
        ("Mistile", "mistile", "Mistile"),

        // --- Misc / non-hostile utility ---
        ("TrainingDummy", "dummy", "Mannequin"),
        ("piece_TrainingDummy", "dummy", "Mannequin"),
        ("Player", "player", "Joueur"),
    };

    /// <summary>Null category/label mean "not a recognized creature" — caller should fall back to a generic label.</summary>
    public static (string? Category, string? Label) Identify(Character? character)
    {
        if (character == null)
        {
            return (null, null);
        }

        var name = character.gameObject.name;
        foreach (var (prefix, category, label) in Families)
        {
            if (name.StartsWith(prefix))
            {
                return (category, label);
            }
        }

        return (null, null);
    }
}

/// <summary>Improvised palette on top of the four colors requested: brown for deer/boar, green for
/// goblins (Greydwarf), gray for skeletons, blue for trolls — everything else picked to feel
/// thematically right, grouped roughly by biome/threat family.</summary>
internal static class CreatureColors
{
    private static readonly Color Environment = new(0.85f, 0.85f, 0.85f);

    public static Color For(string? category)
    {
        return category switch
        {
            "boar" or "deer" => new Color(0.55f, 0.35f, 0.15f),           // marron
            "goblin" => new Color(0.25f, 0.75f, 0.25f),                    // vert
            "skeleton" => new Color(0.65f, 0.65f, 0.65f),                  // gris
            "troll" => new Color(0.25f, 0.45f, 0.9f),                      // bleu
            "wolf" => new Color(0.85f, 0.9f, 0.95f),                       // blanc-glace
            "draugr" => new Color(0.55f, 0.6f, 0.25f),                     // vert marécage malsain
            "fuling" => new Color(0.9f, 0.5f, 0.1f),                       // orange tribal
            "lox" => new Color(0.6f, 0.45f, 0.3f),                         // brun clair
            "serpent" => new Color(0.1f, 0.55f, 0.5f),                     // sarcelle profond
            "golem" => new Color(0.45f, 0.5f, 0.6f),                       // ardoise
            "neck" => new Color(0.3f, 0.55f, 0.35f),                       // vert d'eau trouble
            "leech" => new Color(0.4f, 0.15f, 0.2f),                       // rouge sombre
            "blob" => new Color(0.45f, 0.8f, 0.3f),                        // vert acide
            "surtling" => new Color(0.95f, 0.4f, 0.1f),                    // orange feu
            "drake" => new Color(0.8f, 0.8f, 0.85f),                       // gris clair montagne
            "deathsquito" => new Color(0.9f, 0.85f, 0.2f),                 // jaune vif (danger)
            "seeker" => new Color(0.55f, 0.3f, 0.5f),                      // violet mistland
            "tick" => new Color(0.5f, 0.2f, 0.3f),                         // bordeaux
            "boss" => new Color(0.9f, 0.2f, 0.75f),                        // magenta, se démarque nettement
            "player" => new Color(0.3f, 0.6f, 0.95f),                      // bleu joueur (autres joueurs en multi)
            "seagull" => new Color(0.95f, 0.95f, 0.9f),                    // blanc cassé, côte
            "farm" => new Color(0.9f, 0.8f, 0.5f),                         // jaune paille
            "hare" => new Color(0.75f, 0.6f, 0.45f),                       // brun clair
            "moose" => new Color(0.45f, 0.35f, 0.2f),                      // brun olive foncé
            "seal" => new Color(0.55f, 0.55f, 0.6f),                       // gris argenté phoque
            "mole" => new Color(0.5f, 0.4f, 0.3f),                         // brun-gris
            "elaking" => new Color(0.3f, 0.25f, 0.5f),                     // indigo profond
            "bat" => new Color(0.35f, 0.3f, 0.4f),                         // violet-gris sombre
            "dvergr" => new Color(0.85f, 0.65f, 0.25f),                    // ambre
            "gjall" => new Color(0.8f, 0.55f, 0.7f),                       // rose-lavande
            "tentaroot" => new Color(0.4f, 0.5f, 0.25f),                   // olive malsain
            "fader" => new Color(0.7f, 0.9f, 0.9f),                        // cyan pâle spectral
            "wisp" => new Color(0.7f, 0.85f, 1f),                          // bleu glacé pâle
            "frysling" => new Color(0.75f, 0.9f, 0.95f),                   // presque blanc glacé
            "asksvin" => new Color(0.5f, 0.3f, 0.2f),                      // brun cendré
            "barka" => new Color(0.8f, 0.4f, 0.2f),                        // orange chaud
            "bjorn" => new Color(0.35f, 0.2f, 0.1f),                       // brun ours foncé
            "abomination" => new Color(0.85f, 0.3f, 0.1f),                 // orange magma
            "charred" => new Color(0.3f, 0.15f, 0.1f),                     // braise/charbon
            "volture" => new Color(0.5f, 0.25f, 0.15f),                    // brun-rouge rapace
            "morgen" => new Color(0.5f, 0.55f, 0.4f),                      // gris-vert pierre
            "jotun" => new Color(0.6f, 0.15f, 0.15f),                      // rouge profond
            "kvastur" => new Color(0.45f, 0.35f, 0.5f),                    // violet trouble
            "ghost" => new Color(0.75f, 0.7f, 0.85f),                      // lavande translucide
            "shadow" => new Color(0.25f, 0.15f, 0.3f),                     // violet très sombre
            "wraith" => new Color(0.15f, 0.35f, 0.35f),                    // sarcelle très sombre
            "fallen" => new Color(0.7f, 0.55f, 0.2f),                      // bronze/or
            "mistile" => new Color(0.5f, 0.45f, 0.55f),                    // gris violacé
            "fenring" => new Color(0.55f, 0.5f, 0.55f),                    // gris bête sauvage
            "cultist" => new Color(0.5f, 0.25f, 0.55f),                    // violet robe de culte
            "dummy" => new Color(0.6f, 0.6f, 0.6f),                        // gris neutre
            _ => Environment,
        };
    }
}
