using System;
using System.IO;
using UnityEngine;
using RhythmForge.Core.Data;

namespace RhythmForge.Core.Session
{
    public static class SessionPersistence
    {
        private const string FileName = "rhythmforge_session.json";

        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(AppState state)
        {
            try
            {
                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(FilePath, json);
                Debug.Log($"[RhythmForge] Session saved to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RhythmForge] Failed to save session: {e.Message}");
            }
        }

        public static AppState Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Debug.Log("[RhythmForge] No saved session found, starting fresh.");
                    return null;
                }

                string json = File.ReadAllText(FilePath);
                var state = JsonUtility.FromJson<AppState>(json);
                Debug.Log("[RhythmForge] Session loaded successfully.");
                return state;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RhythmForge] Failed to load session: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                    Debug.Log("[RhythmForge] Saved session deleted.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RhythmForge] Failed to delete session: {e.Message}");
            }
        }
    }
}
