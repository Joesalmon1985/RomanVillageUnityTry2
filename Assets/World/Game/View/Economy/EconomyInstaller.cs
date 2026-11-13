using System.Collections.Generic;
using UnityEngine;
using World.Sim.Economy;

namespace World.Game
{
    /// <summary>
    /// Road economy presentation: one call from WorldBootstrap installs the economy views, the God View toolbar and
    /// tools, the economy HUD (toasts, banners, building actions), first-person access (quartermaster, bell, signs,
    /// objective), the live storehouse lines and the "commission" AutoPlay script.
    /// </summary>
    public static class EconomyInstaller
    {
        public static EconomyViews Install(WorldBootstrap boot)
        {
            var go = new GameObject("Economy");
            go.transform.SetParent(boot.transform, false);

            var views = go.AddComponent<EconomyViews>();
            views.Bind(boot);

            EconomyGodTools.RegisterDefaults();
            go.AddComponent<GodToolbar>().Bind(boot);
            go.AddComponent<EconomyHud>().Bind(boot);
            go.AddComponent<EconomyFirstPerson>().Bind(boot);
            AutoPlayScripts.Register("commission", () => CommissionDriver.Script());

            var store = VillageBuilder.Storehouse;
            if (store != null)
            {
                store.LinesProvider = () => StorehouseLines(boot);
                store.DisplayName = "Storehouse";
            }
            return views;
        }

        private static IEnumerable<string> StorehouseLines(WorldBootstrap boot)
        {
            var s = boot.Sim.State;
            int id = s.Economy.StorehouseId;
            yield return "Holds the village's goods; carriers start and end here.";
            var store = s.Economy.Buildings[id];
            var parts = new List<string>();
            foreach (var w in World.Sim.Wares.All)
                if (store.Stock[(int)w] > 0) parts.Add(EconomyLook.WareTag(w) + " " + store.Stock[(int)w]);
            yield return parts.Count == 0 ? "Empty." : string.Join(",  ", parts);
            int idle = 0;
            foreach (var c in s.Economy.Carriers) if (c.State == CarrierState.Idle && c.AtVertex == store.Vertex) idle++;
            yield return idle + " of " + s.Economy.Carriers.Count + " carriers idle here.";
            foreach (var l in EconomyText.JobLinesFor(s, id, 4)) yield return l;
        }
    }
}
