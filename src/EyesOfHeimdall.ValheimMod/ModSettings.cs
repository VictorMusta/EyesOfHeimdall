using BepInEx.Configuration;

namespace EyesOfHeimdall.ValheimMod;

internal static class ModSettings
{
    public static ConfigEntry<bool> AutoUpdate { get; set; } = null!;
    public static ConfigEntry<bool> Enabled { get; private set; } = null!;
    public static ConfigEntry<bool> HideOwnSounds { get; private set; } = null!;
    public static ConfigEntry<bool> HideUndiscovered { get; private set; } = null!;
    public static ConfigEntry<float> DetectionRange { get; private set; } = null!;
    public static ConfigEntry<float> RingRadius { get; private set; } = null!;
    public static ConfigEntry<float> ArcOpacity { get; private set; } = null!;
    public static ConfigEntry<float> IconSize { get; private set; } = null!;

    public static void Bind(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Afficher les repères sonores. Désactivé : le mod reste installé mais n'affiche plus rien.");

        HideOwnSounds = config.Bind("Filtering", "HideOwnSounds", true,
            "Cacher les sons produits par le personnage du joueur lui-même (pas, coups, voix...).");

        DetectionRange = config.Bind("Detection", "DetectionRangeMeters", 80f, new ConfigDescription(
            "Distance max (en mètres) à laquelle un son est encore affiché.",
            new AcceptableValueRange<float>(20f, 200f)));

        RingRadius = config.Bind("Display", "RingRadius", 150f, new ConfigDescription(
            "Rayon (en pixels) du cercle autour du viseur sur lequel apparaissent les arcs.",
            new AcceptableValueRange<float>(80f, 400f)));

        ArcOpacity = config.Bind("Display", "ArcOpacity", 0.5f, new ConfigDescription(
            "Opacité maximale des arcs (son tout proche). Les sons lointains sont plus transparents.",
            new AcceptableValueRange<float>(0.1f, 1f)));

        IconSize = config.Bind("Display", "IconSize", 34f, new ConfigDescription(
            "Taille (en pixels) de l'icône affichée à côté de chaque arc.",
            new AcceptableValueRange<float>(20f, 72f)));

        HideUndiscovered = config.Bind("Bestiary", "HideUndiscovered", true,
            "Afficher « ??? » à la place de l'icône tant que tu n'as pas frappé ce type de créature.");
    }
}
