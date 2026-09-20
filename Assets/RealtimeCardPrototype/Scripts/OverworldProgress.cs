using UnityEngine;

namespace RealtimeCardPrototype
{
    public static class OverworldProgress
    {
        private const string TrainingMissionCompleteKey = "RealtimeCardPrototype.Overworld.TrainingMissionComplete.v1";
        private const string StormMageUnlockedKey = "RealtimeCardPrototype.Overworld.StormMageUnlocked.v1";

        public static bool IsTrainingMissionComplete => PlayerPrefs.GetInt(TrainingMissionCompleteKey, 0) == 1;
        public static bool IsStormMageUnlocked => PlayerPrefs.GetInt(StormMageUnlockedKey, 0) == 1;

        public static void CompleteTrainingMission()
        {
            PlayerPrefs.SetInt(TrainingMissionCompleteKey, 1);
            PlayerPrefs.SetInt(StormMageUnlockedKey, 1);
            PlayerPrefs.Save();
        }
    }
}
