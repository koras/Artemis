using System.Collections.Generic;
using _Project.Scripts.Presentation.Character;
using UnityEngine;

namespace _Project.Scripts.Systems.Character
{
    /// <summary>
    /// Updates runtime needs for a group of characters.
    /// </summary>
    public sealed class CharacterGroupService
    {
        private const float GAME_MINUTES_PER_REAL_SECOND = 2f;
        private const float MOOD_DECREASE_PER_GAME_HOUR = 1f;
        private readonly IReadOnlyList<CharacterActor> _characters;
        private readonly float _hungerIncreasePerGameHour;
        private readonly float _sleepDesireIncreasePerGameHour;
        private readonly Dictionary<EntityId, float> _hungerRemainderByCharacterId = new Dictionary<EntityId, float>();
        private readonly Dictionary<EntityId, float> _sleepRemainderByCharacterId = new Dictionary<EntityId, float>();
        private readonly Dictionary<EntityId, float> _moodRemainderByCharacterId = new Dictionary<EntityId, float>();

        public CharacterGroupService(
            IReadOnlyList<CharacterActor> characters,
            float hungerIncreasePerGameHour,
            float sleepDesireIncreasePerGameHour)
        {
            _characters = characters;
            _hungerIncreasePerGameHour = Mathf.Max(0f, hungerIncreasePerGameHour);
            _sleepDesireIncreasePerGameHour = Mathf.Max(0f, sleepDesireIncreasePerGameHour);
        }

        /// <summary>
        /// Performs one simulation step for all tracked characters.
        /// </summary>
        public void Tick(float realTickSeconds)
        {
            float gameHoursDelta = (realTickSeconds * GAME_MINUTES_PER_REAL_SECOND) / 60f;

            for (int i = 0; i < _characters.Count; i++)
            {
                var character = _characters[i];
                if (character == null) continue; // Allowed for inspector-driven character lists.

                // Unity 6000.5 deprecates GetInstanceID for object identity lookups.
                EntityId characterId = character.GetEntityId();
                float hungerAccumulated = gameHoursDelta * _hungerIncreasePerGameHour + GetRemainder(_hungerRemainderByCharacterId, characterId);
                int hungerIncrease = Mathf.FloorToInt(hungerAccumulated);
                _hungerRemainderByCharacterId[characterId] = hungerAccumulated - hungerIncrease;

                float sleepAccumulated = gameHoursDelta * _sleepDesireIncreasePerGameHour + GetRemainder(_sleepRemainderByCharacterId, characterId);
                int sleepDesireIncrease = Mathf.FloorToInt(sleepAccumulated);
                _sleepRemainderByCharacterId[characterId] = sleepAccumulated - sleepDesireIncrease;

                float moodAccumulated = gameHoursDelta * MOOD_DECREASE_PER_GAME_HOUR + GetRemainder(_moodRemainderByCharacterId, characterId);
                int moodDecrease = Mathf.FloorToInt(moodAccumulated);
                _moodRemainderByCharacterId[characterId] = moodAccumulated - moodDecrease;

                character.SetHunger(character.Hunger + hungerIncrease);
                character.SetSleepDesire(character.SleepDesire + sleepDesireIncrease);
                character.SetMood(character.Mood - moodDecrease);

            }
        }

        private static float GetRemainder(Dictionary<EntityId, float> remainderByCharacterId, EntityId characterId)
        {
            return remainderByCharacterId.TryGetValue(characterId, out float remainder)
                ? remainder
                : 0f;
        }

        public void Feed(CharacterActor character, int amount)
        {
            if (character == null) return;
            character.SetHunger(character.Hunger - Mathf.Abs(amount));
        }

        public void Rest(CharacterActor character, int amount)
        {
            if (character == null) return;
            character.SetSleepDesire(character.SleepDesire - Mathf.Abs(amount));
        }
    }
}
