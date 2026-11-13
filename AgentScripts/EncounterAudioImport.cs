using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the rhythm stems so the 32-beat loops are gapless and start on the scheduled sample:
/// PCM, Decompress On Load, Preload Audio Data on, Load In Background off, no Force To Mono. Also reports frames and
/// a non-silence check for each stem.
///   unity command script run --file AgentScripts/EncounterAudioImport.cs --entry EncounterAudioImport.Apply
///   unity command script run --file AgentScripts/EncounterAudioImport.cs --entry EncounterAudioImport.Report
/// </summary>
public static class EncounterAudioImport
{
    static readonly string[] Paths =
    {
        "Assets/World/Game/Resources/Rhythm/lv1_warehouse/drums.wav",
        "Assets/World/Game/Resources/Rhythm/lv1_warehouse/bass.wav",
        "Assets/World/Game/Resources/Rhythm/lv1_warehouse/other.wav",
    };

    public static string Apply()
    {
        var sb = new StringBuilder();
        foreach (var p in Paths)
        {
            var imp = AssetImporter.GetAtPath(p) as AudioImporter;
            if (imp == null) { sb.AppendLine(p + ": missing"); continue; }
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = false;
            imp.forceToMono = false;
            imp.ambisonic = false;
            imp.SaveAndReimport();
            sb.AppendLine(p + ": PCM, DecompressOnLoad, preload, foreground, stereo kept");
        }
        return sb.ToString() + Report();
    }

    public static string Report()
    {
        var sb = new StringBuilder();
        foreach (var p in Paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (clip == null) { sb.AppendLine(p + ": no clip"); continue; }
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            double sumSq = 0; float peak = 0;
            foreach (var x in data) { sumSq += x * x; if (Mathf.Abs(x) > peak) peak = Mathf.Abs(x); }
            var imp = AssetImporter.GetAtPath(p) as AudioImporter;
            var s = imp.defaultSampleSettings;
            sb.AppendLine(System.IO.Path.GetFileName(p) + ": " + clip.samples + " frames, " + clip.channels + " ch, " + clip.frequency + " Hz, " +
                clip.length.ToString("0.0000") + " s, rms " + System.Math.Sqrt(sumSq / System.Math.Max(1, data.Length)).ToString("0.0000") + ", peak " + peak.ToString("0.000") +
                " | " + s.compressionFormat + ", " + s.loadType + ", preload " + s.preloadAudioData + ", background " + imp.loadInBackground + ", mono " + imp.forceToMono);
        }
        return sb.ToString();
    }
}
