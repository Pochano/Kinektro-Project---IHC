using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ControlVisualEntry
{
    public DJControlTarget target;
    public string displayName; // Ej. "VOL 1", "EQ LOW", "DISCO 2"
    public Sprite icon;
    public Color badgeColor = Color.cyan;
}

[CreateAssetMenu(fileName = "DJControlVisualConfig", menuName = "Kinektro/Visual Config")]
public class DJControlVisualConfig : ScriptableObject
{
    public List<ControlVisualEntry> entries = new List<ControlVisualEntry>();

    public ControlVisualEntry GetConfig(DJControlTarget target)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].target == target) return entries[i];
        }
        return null;
    }
}