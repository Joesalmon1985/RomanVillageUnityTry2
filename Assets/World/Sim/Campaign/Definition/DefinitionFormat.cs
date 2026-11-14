namespace World.Sim.Campaign
{
    // I2 additions to the definition classes (A.6 change log, I2 entry). Each class is partial so these fields live in
    // the format package's folder and the pinned surface file is not edited.

    public sealed partial class InteriorDef
    {
        /// <summary>
        /// The surface location this interior is filed under (Docs/CAMPAIGN_FORMAT.md, "Interiors"): every MarkerRef into
        /// this interior should use it as its Location part, so two references to one interior marker are always equal.
        /// "" = not declared (the validator then accepts any existing location in references). For an Ash altar's interior
        /// it is the location of the altar's way in, because the altar landmark (lm.altar.*) is placed at that location.
        /// </summary>
        public string Location = "";
    }
}
