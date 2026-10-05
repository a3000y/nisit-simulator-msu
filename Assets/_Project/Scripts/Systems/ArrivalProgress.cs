using NisitSimulator.SaveLoad;
namespace NisitSimulator.Systems
{
    public sealed class ArrivalProgress
    {
        public const int CompletedStep = 14;
        public int Step;
        public bool Done;
        public bool QuestRewarded;
        public static ArrivalProgress FromSave(SaveData data)
        {
            if (data == null) return new ArrivalProgress();
            if (data.arrivalVersion == 0 || data.arrivalIntroDone)
                return new ArrivalProgress { Done = true, Step = CompletedStep, QuestRewarded = data.arrivalQuestRewarded };
            return new ArrivalProgress { Step = System.Math.Max(1, System.Math.Min(13, data.tourStep)), QuestRewarded = data.arrivalQuestRewarded };
        }
        public void Collect(SaveData data)
        {
            data.arrivalVersion = 1; data.arrivalIntroDone = Done;
            data.tourStep = Step; data.arrivalQuestRewarded = QuestRewarded;
        }
    }
}
