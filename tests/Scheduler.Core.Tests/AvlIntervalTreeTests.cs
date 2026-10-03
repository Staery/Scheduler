using Scheduler.Core.Collections;

namespace Scheduler.Core.Tests;

public class AvlIntervalTreeTests
{
    [Fact]
    public void EmptyTree()
    {
        var tree = new AvlIntervalTree<int>();

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Height);
        Assert.Empty(tree.Query(0, 100));
        Assert.True(tree.IsValid());
    }

    [Fact]
    public void SortedInserts_StayBalanced()
    {
        // Sorted input is the worst case for an unbalanced BST: it would degrade into a list of height n.
        var tree = new AvlIntervalTree<int>();
        for (var i = 0; i < 100_000; i++)
        {
            tree.Insert(i, i + 1, i);
        }

        Assert.Equal(100_000, tree.Count);
        Assert.True(tree.IsValid());
        Assert.InRange(tree.Height, 17, MaxAvlHeight(100_000));
        Assert.Equal(Enumerable.Range(0, 100_000), tree.InOrder());
    }

    [Fact]
    public void RandomInserts_KeepInvariantsAndMatchBruteForce()
    {
        var random = new Random(42);
        var tree = new AvlIntervalTree<int>();
        var intervals = new List<(double Start, double End, int Id)>();

        for (var i = 0; i < 5_000; i++)
        {
            double start = random.Next(0, 10_000);
            var end = start + random.Next(0, 300);
            intervals.Add((start, end, i));
            tree.Insert(start, end, i);
        }

        Assert.True(tree.IsValid());
        Assert.InRange(tree.Height, 1, MaxAvlHeight(5_000));

        for (var i = 0; i < 500; i++)
        {
            double from = random.Next(-100, 10_400);
            var to = from + random.Next(1, 500);

            var expected = intervals
                .Where(x => x.Start < to && x.End > from)
                .OrderBy(x => x.Start).ThenBy(x => x.Id)
                .Select(x => x.Id);

            Assert.Equal(expected, tree.Query(from, to));
        }
    }

    [Fact]
    public void DuplicateStarts_AreAllKept()
    {
        var tree = new AvlIntervalTree<string>();
        tree.Insert(5, 10, "a");
        tree.Insert(5, 20, "b");
        tree.Insert(5, 6, "c");

        Assert.Equal(3, tree.Count);
        Assert.Equal(["a", "b", "c"], tree.Query(5, 6));
        Assert.Equal(["b"], tree.Query(15, 16));
    }

    [Fact]
    public void Query_IsHalfOpen()
    {
        var tree = new AvlIntervalTree<int>();
        tree.Insert(0, 10, 1);
        tree.Insert(10, 20, 2);

        Assert.Equal([1], tree.Query(9, 10));
        Assert.Equal([2], tree.Query(10, 11));
        Assert.Empty(tree.Query(20, 30));
        Assert.Empty(tree.Query(5, 5));
    }

    [Fact]
    public void Insert_RejectsReversedInterval() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AvlIntervalTree<int>().Insert(10, 5, 0));

    /// <summary>Upper bound on the height of an AVL tree with n nodes: 1.4405·log2(n + 2) − 0.3277.</summary>
    private static int MaxAvlHeight(int n) => (int)Math.Floor(1.4405 * Math.Log2(n + 2) - 0.3277);
}
