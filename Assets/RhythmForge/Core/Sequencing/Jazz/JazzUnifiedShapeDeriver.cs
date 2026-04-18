namespace RhythmForge.Core.Sequencing.Jazz
{
    /// <summary>
    /// Phase B: composes the three existing Jazz sub-derivers. All shared
    /// orchestration (role dispatch, fabric writeback, MusicalShape packaging)
    /// lives in <see cref="UnifiedShapeDeriverBase"/>.
    /// </summary>
    public sealed class JazzUnifiedShapeDeriver : UnifiedShapeDeriverBase
    {
        public JazzUnifiedShapeDeriver()
            : base(
                new JazzRhythmDeriver(),
                new JazzMelodyDeriver(),
                new JazzHarmonyDeriver())
        { }
    }
}
