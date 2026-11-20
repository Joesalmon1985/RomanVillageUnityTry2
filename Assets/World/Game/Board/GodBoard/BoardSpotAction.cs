using System.Collections.Generic;
using UnityEngine;
using World.Sim;
using World.Sim.Table;

namespace World.Game
{
    /// <summary>
    /// One legal board spot as a two-step world action (WU-2): the first tap stands a ghost piece in the people's colour on the
    /// spot (preview), the second tap on it, or on the builder, sends the command once; a tap anywhere else cancels. Illegal
    /// spots are never offered (<see cref="GodBoardDirector.NearestSpot"/> only finds legal ones, and <see cref="Check"/> asks
    /// again at confirm time). Reads state; only <see cref="Commit"/> sends a command, through the director's guard.
    /// </summary>
    public sealed class BoardSpotAction : IWorldAction, IScreenAnchored
    {
        private readonly GodBoardDirector _d;
        public readonly BuildKind Kind;
        public readonly int Spot;

        public BoardSpotAction(GodBoardDirector d, BuildKind kind, int spot) { _d = d; Kind = kind; Spot = spot; }

        public string Title => Kind == BuildKind.Road ? "A road" : Kind == BuildKind.City ? "A city" : "A camp";

        public IEnumerable<string> Lines
        {
            get
            {
                var vm = _d.Model;
                if (vm.Phase == TablePhase.Setup) { yield return "Your first camps are free."; yield break; }
                var kv = vm.KindOf(Kind == BuildKind.None ? BuildKind.Road : Kind);
                yield return "Costs " + GodBoardLedger.Goods(kv.Cost) + ".";
                yield return kv.Left + " of " + kv.Max + " pieces left.";
            }
        }

        public string PreviewLine => Kind == BuildKind.Road ? "Lay a road here?" : Kind == BuildKind.City ? "Make a city here?" : "Found a camp here?";

        public WorldActionCheck Check()
        {
            if (_d.View != BoardView.Board) return WorldActionCheck.Disable("The board is closed.");
            if (!_d.IsSpotOffered(Kind, Spot)) return WorldActionCheck.Refuse("That spot is not open to you now.");
            return WorldActionCheck.Allowed;
        }

        public void ShowPreview(bool on)
        {
            if (_d.Pieces == null) return;
            if (on) _d.Pieces.ShowGhost(Kind, Spot, _d.Human); else _d.Pieces.HideGhost();
        }

        public bool Commit()
        {
            CommandResult r;
            if (Kind == BuildKind.Road) r = _d.PlaceRoad(Spot);
            else if (Kind == BuildKind.City) r = _d.UpgradeCity(Spot);
            else r = _d.PlaceSettlement(Spot);
            return r.Ok;
        }

        public bool TryGetScreenRect(out Rect gui)
        {
            var p = Kind == BuildKind.Road ? _d.ScreenOfEdge(Spot) : _d.ScreenOfCorner(Spot);
            float m = WorldHitTarget.MinSide(Screen.height);
            gui = new Rect(p.x - m / 2, Screen.height - p.y - m / 2, m, m);
            return true;
        }
    }
}
