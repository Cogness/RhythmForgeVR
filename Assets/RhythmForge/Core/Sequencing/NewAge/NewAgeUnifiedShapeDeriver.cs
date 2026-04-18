namespace RhythmForge.Core.Sequencing.NewAge
{
    /// <summary>
    /// Phase B: composes the three existing NewAge sub-derivers. All shared
    /// orchestration (role dispatch, fabric writeback, MusicalShape packaging)
    /// lives in <see cref="UnifiedShapeDeriverBase"/>.
    /// </summary>
    public sealed class NewAgeUnifiedShapeDeriver : UnifiedShapeDeriverBase
    {
        public NewAgeUnifiedShapeDeriver()
            : base(
                new NewAgeRhythmDeriver(),
                new NewAgeMelodyDeriver(),
                new NewAgeHarmonyDeriver())
        { }
    }
}
