namespace CurveExtractor.Structures
{
    public sealed class CurvePointEntry
    {
        public float[] Coordinates;
        public ushort CurveID;
        public byte Index;

        public float X => Coordinates[0];
        public float Y => Coordinates[1];
    }

    public sealed class CurveEntry
    {
        public byte Type;
        public byte Unused;
    }
}
