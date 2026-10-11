using System;
using UnityEngine;

/// <summary>
/// Represents a single line of dialogue spoken by a character.
/// </summary>
[Serializable]
public struct DialogueLine
{
    [Tooltip("Name of the speaker shown in the dialogue box (e.g., Achilles, Odysseus, Cyclops).")]
    public string speakerName;

    [Tooltip("Dialogue text content.")]
    [TextArea(2, 5)]
    public string text;

    [Tooltip("Color of the speaker's name in the UI.")]
    public Color speakerColor;

    public DialogueLine(string name, string content, Color color)
    {
        speakerName = name;
        text = content;
        speakerColor = color;
    }
}
