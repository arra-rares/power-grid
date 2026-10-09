namespace gui.Model.Persistence
{
    /// <summary>
    /// Process-wide session flags. Not serialized; the checkpoint file holds the session id.
    /// </summary>
    public static class GameSession
    {
        public static bool Active { get; private set; }

        public static string SessionId { get; private set; } = "";

        private static bool _startNextWithoutDelay;

        public static void Begin(string sessionId)
        {
            Active = true;
            SessionId = sessionId;
            _startNextWithoutDelay = false;
        }

        public static void ArmResumeClock() => _startNextWithoutDelay = true;

        public static bool TakeStartWithoutDelay()
        {
            if (!_startNextWithoutDelay)
                return false;

            _startNextWithoutDelay = false;
            return true;
        }
    }
}
