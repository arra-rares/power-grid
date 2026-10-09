namespace gui.Model.Persistence
{
    /// <summary>
    /// Drops remote and card input while a checkpoint is being written.
    /// </summary>
    public static class InputGate
    {
        public static bool Closed { get; set; }
    }
}
