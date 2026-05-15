namespace Project.Scripts
{
    public sealed class QrManualAnchorOptions
    {
        public bool CreateAnchors { get; }

        public QrManualAnchorOptions(bool createAnchors)
        {
            CreateAnchors = createAnchors;
        }
    }
}
