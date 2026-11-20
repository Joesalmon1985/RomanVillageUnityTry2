using System;
using System.IO;
using System.Text;
using UnityEngine;
using World.Game;
using World.Sim;
using World.Sim.Plague;

/// <summary>
/// WU-3 capture helper (the plague at the healer's lodge). Reads commands from Temp/wu3_cmd.txt (separated by ';') and runs them
/// in play mode on a god board game (start it with WorldUiCaptures "godboard"; sizes and shots are WorldUiCaptures' too):
///   at outside|table|post|vessels|satchel|sign|charm | face DEG | infect N | outbreaks N | demons NODE KIND N | cure E | erad E | strong E |
///   storm | ledger table|post|stick|vessel E|satchel|charm|sign | cancel | info
/// "infect" and "outbreaks" write plague state by the sim's own rules (capture only). Run:
/// unity command script run --file AgentScripts/WU3Drive.cs --entry WU3Drive.Run
/// </summary>
public static class WU3Drive
{
    public static string Run()
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "wu3_cmd.txt");
        if (!File.Exists(path)) return "no Temp/wu3_cmd.txt";
        var sb = new StringBuilder();
        foreach (string raw in File.ReadAllText(path).Split(';'))
        {
            string cmd = raw.Trim();
            if (cmd.Length == 0) continue;
            try { sb.Append(Do(cmd)).Append(" | "); }
            catch (Exception e) { sb.Append(cmd + " FAILED: " + e.GetType().Name + " " + e.Message + " | "); }
        }
        return sb.ToString();
    }

    static void Stand(Vector3 feet, Vector3 look)
    {
        var w = PlayerWalker.Instance; w.StopAuto();
        feet.y = PlayerWalker.GroundHeight(feet);
        w.Teleport(feet, BoardSpace.YawOf(look - feet), 10f);
    }

    static string Do(string cmd)
    {
        var a = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var boot = WorldBootstrap.Instance; var s = boot.Sim.State;
        var lodge = HealersLodge.Instance; var t = lodge.Plague;
        var f = lodge.Frame;
        var ev = new EventBuffer();
        switch (a[0])
        {
            case "at":
                switch (a[1])
                {
                    case "outside": Stand(f.W(2f, 30f), f.O + Vector3.up * 2f); break;
                    case "table": Stand(t.Props.TransformPoint(new Vector3(0, 0, LodgeTable.TableSpot.y + 2.7f)), t.TableCentre + Vector3.down * 0.3f); break;
                    case "tablehigh": Stand(t.Props.TransformPoint(new Vector3(0.4f, 0, LodgeTable.TableSpot.y + 2.1f)), t.TableCentre); break;
                    case "post": Stand(t.Props.TransformPoint(new Vector3(-6f, 0, LodgeTable.PostSpot.y + 7f)), t.Props.TransformPoint(new Vector3(-7.1f, 2.4f, LodgeTable.PostSpot.y))); break;
                    case "vessels": Stand(t.Props.TransformPoint(new Vector3(6.6f, 0, LodgeTable.ShelfSpot.y + 3.3f)), t.Props.TransformPoint(new Vector3(7.4f, 1.1f, LodgeTable.ShelfSpot.y))); break;
                    case "satchel": Stand(t.Props.TransformPoint(new Vector3(3.6f, 0, LodgeTable.SatchelSpot.y + 3f)), t.Props.TransformPoint(new Vector3(3.6f, 1.1f, LodgeTable.SatchelSpot.y))); break;
                    case "charm": Stand(t.Props.TransformPoint(new Vector3(1.6f, 0, LodgeTable.CharmSpot.y + 3f)), t.Props.TransformPoint(new Vector3(1.0f, 1.2f, LodgeTable.CharmSpot.y))); break;
                    case "sign": Stand(t.Props.TransformPoint(new Vector3(-3.2f, 0, LodgeTable.SignSpot.y + 3f)), t.Props.TransformPoint(new Vector3(-3.2f, 1.5f, LodgeTable.SignSpot.y))); break;
                    default: return "unknown place " + a[1];
                }
                return "at " + a[1] + " " + PlayerWalker.Instance.Position;
            case "infect":
                for (int i = 0; i < int.Parse(a[1]); i++) PlagueRules.InfectRound(s, ev);
                return "demons " + PlagueApi.TotalDemons(s);
            case "outbreaks":
                {
                    int want = int.Parse(a[1]), g = 0;
                    while (s.Plague.Outbreaks < want && g++ < 3000) { PlagueRules.InfectRound(s, ev); PlagueRules.Epidemic(s, ev); }
                    return "outbreaks " + s.Plague.Outbreaks + " rate " + s.Plague.RateIndex + " fell " + s.Plague.IslandFell;
                }
            case "cure": s.Plague.Cured[int.Parse(a[1])] = true; return "cured";
            case "erad": s.Plague.Cured[int.Parse(a[1])] = true; s.Plague.Eradicated[int.Parse(a[1])] = true; return "eradicated";
            case "strong": s.Plague.Strengthened[int.Parse(a[1])] = true; return "strengthened";
            case "storm": PlagueViews.Instance.BeginStorm(); return "storm";
            case "runner": lodge.SendRunner("Sickness has broken out on the island!"); return "runner";
            case "ledger":
                {
                    LodgeInspect li = a[1] == "table" ? t.Table : a[1] == "post" ? t.Post : a[1] == "stick" ? t.Stick : a[1] == "satchel" ? t.Satchel
                        : a[1] == "charm" ? t.Charm : a[1] == "sign" ? t.Sign : a[1] == "vessel" ? t.Vessels[int.Parse(a[2])] : a[1] == "node" ? t.NodeTaps[int.Parse(a[2]) - 1] : null;
                    if (li == null) return "no such prop";
                    WorldActions.Cancel(); li.Interact();
                    return "ledger " + WorldActions.State + " " + (LedgerCard.IsOpen ? LedgerCard.Content.Title : "closed");
                }
            case "cancel": WorldActions.Cancel(); return "cancelled";
            case "info":
                return "props " + t.PropsShown + " lanterns " + t.LanternsLit + " peg " + t.PegStep + " demons " + PlagueApi.TotalDemons(s) + " outbreaks " + s.Plague.Outbreaks
                    + " table " + t.TableCentre + " player " + PlayerWalker.Instance.Position + " screen " + Screen.width + "x" + Screen.height;
            default: return "unknown " + a[0];
        }
    }
}
