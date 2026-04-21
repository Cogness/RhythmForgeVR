namespace RhythmForge.Core.Data
{
    public enum PatternType
    {
        Percussion = 0,
        Melody = 1,
        Harmony = 2,
        Bass = 3,
        Groove = 4,

        // Legacy aliases kept for Phase A compatibility.
        RhythmLoop = Percussion,
        MelodyLine = Melody,
        HarmonyPad = Harmony
    }
}
