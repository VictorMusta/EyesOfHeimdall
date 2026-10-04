using BepInEx.Configuration;
using UnityEngine;

namespace EyesOfHeimdall.ValheimMod;

// Lives in the game's own menus (title screen, pause menu): the cursor is already free there, so no input hooks are needed.
internal static class SettingsPanel
{
    private const float Width = 360f;
    private const float ResetConfirmSeconds = 4f;

    private static bool _open;
    private static bool _dirty;
    private static float _resetArmedUntil;
    private static GUIStyle? _titleStyle;

    public static bool IsOpen => _open;

    public static void Draw(ConfigFile config)
    {
        if (!GameMenuVisible())
        {
            _open = false;
            Flush(config);
            return;
        }

        // IMGUI is laid out in raw pixels: without this the panel is unreadably small above 1080p.
        var scale = Mathf.Max(1f, Screen.height / 1080f);
        var savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.color = Color.white;

        // Left edge, mid-height: clear of the game's own buttons, which a click here would otherwise also press.
        var top = Screen.height / scale * 0.5f - 200f;
        if (_open)
        {
            GUILayout.BeginArea(new Rect(20f, top, Width, 600f));
            GUILayout.BeginVertical(GUI.skin.box);
            DrawControls();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        else if (GUI.Button(new Rect(20f, top, 150f, 28f), "EyesOfHeimdall"))
        {
            _open = true;
        }

        GUI.matrix = savedMatrix;

        if (!_open || Event.current.rawType == EventType.MouseUp)
        {
            Flush(config);
        }
    }

    private static bool GameMenuVisible()
    {
        if (Menu.instance != null && Menu.instance.m_root.gameObject.activeSelf)
        {
            return true;
        }

        return FejdStartup.instance != null && FejdStartup.instance.m_mainMenu.activeInHierarchy;
    }

    private static void DrawControls()
    {
        _titleStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label($"EyesOfHeimdall {Plugin.Version}", _titleStyle);

        Toggle(ModSettings.Enabled, "Afficher les repères sonores");
        Toggle(ModSettings.HideOwnSounds, "Cacher mes propres sons (pas, coups...)");

        GUILayout.Space(6f);
        Slider(ModSettings.DetectionRange, 5f, v => $"Portée de détection : {v:0} m");
        Slider(ModSettings.RingRadius, 5f, v => $"Taille du cercle : {v:0} px");
        Slider(ModSettings.IconSize, 2f, v => $"Taille des icônes : {v:0} px");
        Slider(ModSettings.ArcOpacity, 0.05f, v => $"Opacité des arcs : {v * 100f:0} %");

        GUILayout.Space(6f);
        Toggle(ModSettings.HideUndiscovered, "Bestiaire : « ??? » avant le premier coup");
        GUILayout.Label($"Familles de créatures découvertes : {Discovery.Count}");

        var armed = Time.realtimeSinceStartup < _resetArmedUntil;
        if (GUILayout.Button(armed ? "Confirmer : tout repasser en « ??? »" : "Réinitialiser le bestiaire"))
        {
            if (armed)
            {
                Discovery.Reset();
                _resetArmedUntil = 0f;
            }
            else
            {
                _resetArmedUntil = Time.realtimeSinceStartup + ResetConfirmSeconds;
            }
        }

        GUILayout.Space(6f);
        Toggle(ModSettings.AutoUpdate, "Mise à jour automatique au lancement");

        GUILayout.Space(6f);
        if (GUILayout.Button("Fermer"))
        {
            _open = false;
        }
    }

    private static void Toggle(ConfigEntry<bool> entry, string label)
    {
        Set(entry, GUILayout.Toggle(entry.Value, " " + label));
    }

    private static void Slider(ConfigEntry<float> entry, float step, Func<float, string> label)
    {
        var range = (AcceptableValueRange<float>)entry.Description.AcceptableValues;
        GUILayout.Label(label(entry.Value));

        var raw = GUILayout.HorizontalSlider(entry.Value, range.MinValue, range.MaxValue);
        // Only snap to the step when the slider actually moved, so a hand-edited value isn't rewritten just by opening the panel.
        if (raw != entry.Value)
        {
            Set(entry, Mathf.Round(raw / step) * step);
        }
    }

    private static void Set<T>(ConfigEntry<T> entry, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(entry.Value, value))
        {
            entry.Value = value;
            _dirty = true;
        }
    }

    private static void Flush(ConfigFile config)
    {
        if (_dirty)
        {
            config.Save();
            _dirty = false;
        }
    }
}
