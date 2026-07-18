using UnityEngine;

namespace _Project.Scripts.Data.ColonyEvents
{
    public enum ColonyEventEffectType
    {
        None = 0,
        SolarGenerationMultiplier = 1,
        RadiationRiskPlaceholder = 2,
        MoonquakeRiskPlaceholder = 3
    }

    public enum ColonyEventThresholdComparison
    {
        LessOrEqual = 0,
        GreaterOrEqual = 1,
        LessThan = 2,
        GreaterThan = 3
    }

    [System.Serializable]
    public sealed class ColonyEventPeriodicWindowCondition
    {
        public bool Enabled;
        [Min(1)] public int IntervalDays = 8;
        [Min(1)] public int LifetimeDays = 2;
        [Min(1)] public int StartSol = 1;
    }

    [System.Serializable]
    public sealed class ColonyEventResourceAmountCondition
    {
        public bool Enabled;
        public string ResourceId = "Iron";
        public ColonyEventThresholdComparison Comparison = ColonyEventThresholdComparison.LessThan;
        [Min(0)] public int Threshold = 10;
    }

    [System.Serializable]
    public sealed class ColonyEventConditionSet
    {
        [Header("Base")]
        [Min(0)] public int MinGameMinutesToAppear;

        [Header("Optional Conditions")]
        public ColonyEventPeriodicWindowCondition PeriodicWindow = new ColonyEventPeriodicWindowCondition();
        public ColonyEventResourceAmountCondition ResourceAmount = new ColonyEventResourceAmountCondition();
    }

    /// <summary>
    /// ScriptableObject definition for a single colony-wide daily event.
    /// </summary>
    [CreateAssetMenu(menuName = "Artemis/Colony Events/Definition", fileName = "ColonyEventDefinition")]
    public sealed class ColonyEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string EventId = "colony-event-id";
        public string Title;
        [TextArea] public string Description;

        [Header("Effect")]
        public ColonyEventEffectType EffectType;
        public float EffectMagnitude = 1f;
        public float SecondaryMagnitude;

        [Header("Conditions")]
        public ColonyEventConditionSet Conditions = new ColonyEventConditionSet();
    }
}
