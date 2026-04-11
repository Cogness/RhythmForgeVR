using System;
using System.Collections.Generic;

namespace RhythmForge.Core.Data
{
    [Serializable]
    public class AppState
    {
        public int version = 3;
        public float tempo = 85f;
        public string key = "A minor";
        public string activeGroupId = "lofi";
        public string drawMode = "RhythmLoop";
        public string activeSceneId = "scene-a";
        public string selectedInstanceId;
        public string selectedPatternId;
        public string queuedSceneId;
        public List<PatternDefinition> patterns = new List<PatternDefinition>();
        public List<PatternInstance> instances = new List<PatternInstance>();
        public List<SceneData> scenes = new List<SceneData>();
        public List<ArrangementSlot> arrangement = new List<ArrangementSlot>();
        public DraftCounters counters = new DraftCounters();
    }

    [Serializable]
    public class DraftCounters
    {
        public int rhythm = 1;
        public int melody = 1;
        public int harmony = 1;
    }

    public static class AppStateFactory
    {
        public const int BarSteps = 16;
        public const int MaxArrangementSlots = 8;

        public static AppState CreateEmpty()
        {
            var state = new AppState();
            state.scenes = new List<SceneData>
            {
                new SceneData("scene-a", "Scene A"),
                new SceneData("scene-b", "Scene B"),
                new SceneData("scene-c", "Scene C"),
                new SceneData("scene-d", "Scene D")
            };
            state.arrangement = new List<ArrangementSlot>();
            for (int i = 0; i < MaxArrangementSlots; i++)
            {
                state.arrangement.Add(new ArrangementSlot($"slot-{i + 1}"));
            }
            return state;
        }
    }
}
