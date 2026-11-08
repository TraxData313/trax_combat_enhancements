using System;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100f: the no-allocation checks, measured robustly. <c>GC.GetAllocatedBytesForCurrentThread</c> flaked now and then under the
/// parallel test run (Step87 / 91 / 95 / 97 each failed once in ~15 full runs - none of them touches step 100's code; the same test alone
/// never failed): a garbage collection started by another test's thread between the two reads can leave the count off. A real
/// allocation in the measured loop allocates on EVERY run, so the fewest bytes over three runs still catches it while the noise goes.
/// </summary>
internal static class NoAllocation
{
    /// <summary>The fewest bytes <paramref name="run"/> allocated on this thread over up to three runs (0 ends early).</summary>
    public static long Bytes(Action run)
    {
        long best = long.MaxValue;
        for (int attempt = 0; attempt < 3 && best != 0; attempt++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            run();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            if (bytes < best) best = bytes;
        }
        return best;
    }
}
