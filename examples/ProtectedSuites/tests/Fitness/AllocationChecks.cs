using Microsoft.VisualStudio.TestTools.UnitTesting;
using EchoApplication = Legacy.Echo;

[TestClass]
public sealed class AllocationChecks
{
    [TestMethod]
    public void ScoreStaysWithinAllocationBudget()
    {
        const int operations = 512;
        const long budget = 2_048;
        // Measure one synchronous batch on a warmed .NET 10 thread. Report managed bytes, not elapsed time.
        for (int warmup = 0; warmup < operations; warmup++) EchoApplication.Score(warmup);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < operations; i++) sum += EchoApplication.Score(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.AreEqual(operations * (operations - 1) / 2 + 3 * operations, sum);
        Assert.IsLessThanOrEqualTo(budget, allocated, $"Allocated {allocated} bytes in {operations} operations.");
    }
}
