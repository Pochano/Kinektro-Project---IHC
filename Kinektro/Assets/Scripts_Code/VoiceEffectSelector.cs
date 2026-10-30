#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
#define KINEKTRO_VOICE_AVAILABLE
#endif

using System;
using UnityEngine;
#if KINEKTRO_VOICE_AVAILABLE
using UnityEngine.Windows.Speech;
#endif

/// <summary>
/// Reconocedor de voz minimo para elegir efecto en la PC del Kinect (PC B), usando el
/// motor de reconocimiento de Windows (gratis, sin internet, sin paquetes extra).
/// Reconoce frases fijas en espanol por canal y efecto, y llama a
/// CrossPC_KinectBridge.SelectEffectByVoice(canal, slot) para que viajen a la PC A.
///
/// Solo funciona en Windows Standalone / Editor en Windows (UnityEngine.Windows.Speech).
/// Si se corre en otra plataforma, el componente se desactiva solo y avisa por log.
///
/// slot: 1=Reverb, 2=Delay, 3=Distortion, 4=LowPass, 5=Flanger (mismo orden que
/// FXSelector.fxButtons y que DJ_Manager.fxIntensityParams).
/// </summary>
public class VoiceEffectSelector : MonoBehaviour
{
    [Header("Referencias")]
    public CrossPC_KinectBridge bridge;

    [Header("Frases - Canal 1 (mano izquierda)")]
    public string[] ch1ReverbPhrases = { "reverb canal uno", "reverb izquierda" };
    public string[] ch1DelayPhrases = { "delay canal uno", "delay izquierda" };
    public string[] ch1DistortionPhrases = { "distorsion canal uno", "distorsion izquierda" };
    public string[] ch1LowPassPhrases = { "pasa bajos canal uno", "pasa bajos izquierda" };
    public string[] ch1FlangerPhrases = { "flanger canal uno", "flanger izquierda" };

    [Header("Frases - Canal 2 (mano derecha)")]
    public string[] ch2ReverbPhrases = { "reverb canal dos", "reverb derecha" };
    public string[] ch2DelayPhrases = { "delay canal dos", "delay derecha" };
    public string[] ch2DistortionPhrases = { "distorsion canal dos", "distorsion derecha" };
    public string[] ch2LowPassPhrases = { "pasa bajos canal dos", "pasa bajos derecha" };
    public string[] ch2FlangerPhrases = { "flanger canal dos", "flanger derecha" };

#if KINEKTRO_VOICE_AVAILABLE
    private KeywordRecognizer keywordRecognizer;
    private readonly System.Collections.Generic.Dictionary<string, (int channel, int slot)> phraseMap =
        new System.Collections.Generic.Dictionary<string, (int channel, int slot)>();
#endif

    void Start()
    {
#if KINEKTRO_VOICE_AVAILABLE
        BuildPhraseMap(ch1ReverbPhrases, 1, 1);
        BuildPhraseMap(ch1DelayPhrases, 1, 2);
        BuildPhraseMap(ch1DistortionPhrases, 1, 3);
        BuildPhraseMap(ch1LowPassPhrases, 1, 4);
        BuildPhraseMap(ch1FlangerPhrases, 1, 5);

        BuildPhraseMap(ch2ReverbPhrases, 2, 1);
        BuildPhraseMap(ch2DelayPhrases, 2, 2);
        BuildPhraseMap(ch2DistortionPhrases, 2, 3);
        BuildPhraseMap(ch2LowPassPhrases, 2, 4);
        BuildPhraseMap(ch2FlangerPhrases, 2, 5);

        if (phraseMap.Count == 0)
        {
            Debug.LogWarning("[VoiceEffectSelector] No hay frases configuradas, no se inicia el reconocedor.");
            return;
        }

        string[] allPhrases = new string[phraseMap.Count];
        phraseMap.Keys.CopyTo(allPhrases, 0);

        keywordRecognizer = new KeywordRecognizer(allPhrases);
        keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
        keywordRecognizer.Start();

        Debug.Log($"[VoiceEffectSelector] Reconocedor de voz iniciado con {allPhrases.Length} frases.");
#else
        Debug.LogWarning("[VoiceEffectSelector] UnityEngine.Windows.Speech no esta disponible en esta plataforma/build. Deshabilitando el reconocedor de voz (el resto del puente UDP sigue funcionando igual).");
        enabled = false;
#endif
    }

#if KINEKTRO_VOICE_AVAILABLE
    private void BuildPhraseMap(string[] phrases, int channel, int slot)
    {
        if (phrases == null) return;
        foreach (string phrase in phrases)
        {
            if (string.IsNullOrWhiteSpace(phrase)) continue;
            phraseMap[phrase.ToLowerInvariant()] = (channel, slot);
        }
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        string phrase = args.text.ToLowerInvariant();

        if (phraseMap.TryGetValue(phrase, out var selection))
        {
            Debug.Log($"[VoiceEffectSelector] Frase reconocida: \"{phrase}\" -> canal={selection.channel} slot={selection.slot} (confianza: {args.confidence})");

            if (bridge != null)
            {
                bridge.SelectEffectByVoice(selection.channel, selection.slot);
            }
            else
            {
                Debug.LogWarning("[VoiceEffectSelector] 'bridge' no esta asignado en el Inspector, no se puede enviar la seleccion a la PC A.");
            }
        }
    }

    void OnDestroy()
    {
        if (keywordRecognizer != null)
        {
            if (keywordRecognizer.IsRunning) keywordRecognizer.Stop();
            keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;
            keywordRecognizer.Dispose();
            keywordRecognizer = null;
        }
    }
#endif
}
