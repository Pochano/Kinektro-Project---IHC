using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "NewVinylMetadata", menuName = "DJ System/Vinyl Metadata")]
public class VinylMetadata : ScriptableObject
{
    [Header("Metadata")]
    public string songTitle;
    public string artistName;

    [Header("Audio & BPM")]
    public float baseBPM = 120f;
    public EventReference fmodAudioEvent;

    [Header("Visuals")]
    public Texture2D coverArt;
}