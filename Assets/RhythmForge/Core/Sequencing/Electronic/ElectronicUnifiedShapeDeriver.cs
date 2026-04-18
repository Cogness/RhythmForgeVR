namespace RhythmForge.Core.Sequencing.Electronic
{
    /// <summary>
    /// Phase B: composes the three existing Electronic sub-derivers. All shared
    /// orchestration (role dispatch, fabric writeback, MusicalShape packaging)
    /// lives in <see cref="UnifiedShapeDeriverBase"/>.
    /// </summary>
    public sealed class ElectronicUnifiedShapeDeriver : UnifiedShapeDeriverBase
    {
        public ElectronicUnifiedShapeDeriver()
            : base(
                new ElectronicRhythmDeriver(),
                new ElectronicMelodyDeriver(),
                new ElectronicHarmonyDeriver())
        { }
    }
}
