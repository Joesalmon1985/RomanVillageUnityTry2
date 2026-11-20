using System.Linq;
using System.Text;
using UnityEngine;
using World.Sim.Encounters.Rhythm;
public static class Wu5Probe
{
    public static string Chart()
    {
        var pack = Resources.Load<TextAsset>("Rhythm/lv1_warehouse/pack");
        var chart = PackJson.Parse(pack.text);
        var s = new PhraseSession(new RhythmTuning(), chart);
        var sb = new StringBuilder();
        var cues = s.AllCues.ToList();
        sb.AppendLine("cues block0: " + cues.Count);
        foreach (var c in cues.Take(40)) sb.AppendLine(c.Index + " t=" + c.Time.ToString("0.000") + " pad" + c.Pad + " " + c.Family + (c.IsHold ? " hold " + (c.EndTime - c.Time).ToString("0.00") : ""));
        var sim = cues.GroupBy(c => c.Tick).Where(g => g.Count() > 1).Select(g => g.Key + ":" + string.Join(",", g.Select(x => x.Pad))).ToList();
        sb.AppendLine("simultaneous: " + string.Join(" ", sim));
        return sb.ToString();
    }
}
