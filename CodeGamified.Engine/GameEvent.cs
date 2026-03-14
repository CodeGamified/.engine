// CodeGamified.Engine — Shared code execution framework
// MIT License

namespace CodeGamified.Engine
{
    /// <summary>
    /// Base output event from program execution.
    /// Games define their own EventType enum values.
    /// </summary>
    public struct GameEvent
    {
        public int Type;
        public float Value;
        public int Channel;
        public double SimulationTime;
        public int Tag;

        public GameEvent(int type, float value, int channel, double simTime, int tag = 0)
        {
            Type = type;
            Value = value;
            Channel = channel;
            SimulationTime = simTime;
            Tag = tag;
        }
    }
}
