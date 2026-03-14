// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;

namespace CodeGamified.Engine
{
    [Flags]
    public enum CpuFlags
    {
        None = 0,
        Zero = 1,
        Negative = 2,
        Carry = 4,
        Overflow = 8
    }
}
