namespace BetterScreenShot.Native
{
    internal abstract class SaveDialog
    {
        private volatile bool _done;

        public bool IsDone => _done;

        public string Result { get; protected set; }

        public string Error { get; protected set; }

        protected void Finish() => _done = true;
    }
}
