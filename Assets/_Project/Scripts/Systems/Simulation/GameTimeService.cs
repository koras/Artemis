namespace _Project.Scripts.Systems.Simulation
{
    /// <summary>
    /// Хранит внутреннее игровое время лунной базы.
    /// 30 реальных секунд равны одному игровому часу.
    /// </summary>
    public sealed class GameTimeService
    {
        private const float REAL_SECONDS_PER_GAME_HOUR = 30f;
        private const int HOURS_PER_SOL = 24;
        private const int SOLS_PER_LIGHT_PHASE = 14;
        private const int SOLS_PER_LIGHT_CYCLE = SOLS_PER_LIGHT_PHASE * 2;

        private float _realSecondsAccumulator;
        private int _totalGameHours;

        public int Hour => _totalGameHours % HOURS_PER_SOL;

        public int Sol => (_totalGameHours / HOURS_PER_SOL) + 1;
        public int TotalGameHours => _totalGameHours;
        public int TotalGameMinutes => _totalGameHours * 60 + (int)((_realSecondsAccumulator / REAL_SECONDS_PER_GAME_HOUR) * 60f);

        public bool IsDay
        {
            get
            {
                int cycleSolIndex = (Sol - 1) % SOLS_PER_LIGHT_CYCLE;
                return cycleSolIndex < SOLS_PER_LIGHT_PHASE;
            }
        }

        public void Tick(float realDeltaSeconds)
        {
            _realSecondsAccumulator += realDeltaSeconds;

            // Накапливаем время, чтобы игровой час менялся ровно раз в 30 секунд.
            while (_realSecondsAccumulator >= REAL_SECONDS_PER_GAME_HOUR)
            {
                _realSecondsAccumulator -= REAL_SECONDS_PER_GAME_HOUR;
                _totalGameHours++;
            }
        }
    }
}
