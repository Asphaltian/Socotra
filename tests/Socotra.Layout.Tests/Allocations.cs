namespace Socotra.Layout.Tests;

internal static class Allocations
{
    private const int SettledPasses = 5;
    private const int MaxPasses = 100;

    public static void AssertSettles(string what, Action pass, Action? beforePass = null)
    {
        var bytes = new List<long>(MaxPasses);
        var settled = 0;
        while (bytes.Count < MaxPasses)
        {
            beforePass?.Invoke();
            var before = GC.GetAllocatedBytesForCurrentThread();
            pass();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            bytes.Add(allocated);
            settled = allocated == 0 ? settled + 1 : 0;
            if (settled == SettledPasses)
            {
                return;
            }
        }

        Assert.Fail($"{what} kept allocating: in {MaxPasses} passes, never {SettledPasses} in a row allocated nothing. Bytes per pass: {string.Join(", ", bytes)}");
    }
}
