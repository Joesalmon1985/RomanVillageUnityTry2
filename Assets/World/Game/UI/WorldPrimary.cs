namespace World.Game
{
    /// <summary>
    /// Marker (WU-6): a small prop or tag that wins a tap over the person or the interior marker it hangs on or sits
    /// at (Mira's pouch and journal book over Mira, a companion's card over the walker interface, Mira's act over a
    /// pedestal's plain reading). <see cref="PointerInput.TargetOf"/> looks for it first. Draws nothing.
    /// </summary>
    public interface IWorldPrimary { }
}
