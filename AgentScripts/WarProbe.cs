using UnityEngine;
using World.Game;
public static class WarProbe
{
    public static string Near()
    {
        var p = Camera.main.transform.position; var sb = new System.Text.StringBuilder("cam " + p + " | ");
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if ((r.bounds.center - p).magnitude < 9f && r.bounds.size.x > 3f) { sb.Append(r.transform.root.name + "/" + r.transform.parent?.name + "/" + r.name + " " + r.bounds.size + "; "); }
        return sb.ToString();
    }
}
public static class WarProbe2
{
    public static string Huts()
    {
        var S = WorldBootstrap.Instance.Sim.State; var sb = new System.Text.StringBuilder();
        foreach (var st in S.War.Settlements) { sb.Append("corner " + st.Corner + " home=" + st.IsHome + " huts:"); foreach (var h in st.Huts) sb.Append(" g" + h.Good + " build=" + h.BuildLeft + " cyc=" + h.CycleLeft); sb.Append(" raw=" + string.Join(",", st.Raw) + " proc=" + string.Join(",", st.Proc) + " | "); }
        sb.Append("clock " + S.War.Clock + " lodge: ");
        foreach (var l in S.War.Lodges) sb.Append(l.People + ":" + l.Corner + " q=" + l.Queue.Count + " tr=" + l.Training.Count + "/" + l.TrainLeft + " ");
        return sb.ToString();
    }
}
