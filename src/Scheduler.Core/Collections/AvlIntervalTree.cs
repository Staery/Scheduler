namespace Scheduler.Core.Collections;

/// <summary>
/// A self-balancing AVL tree of intervals, augmented with the maximum end of every subtree (an interval tree).
/// <list type="bullet">
/// <item>Insert is O(log n): the tree is rebalanced with single and double rotations after every insert.</item>
/// <item>Finding all intervals that overlap a window is O(log n + k), where k is the number of matches.</item>
/// <item>Equal start values are allowed: nodes are ordered by (start, insertion order), so nothing is ever dropped.</item>
/// </list>
/// </summary>
/// <typeparam name="T">Value stored with each interval.</typeparam>
public sealed class AvlIntervalTree<T>
{
    private Node? _root;
    private long _sequence;

    public int Count { get; private set; }

    /// <summary>Height of the tree (0 when empty). An AVL tree never exceeds ~1.44·log2(n + 2).</summary>
    public int Height => HeightOf(_root);

    /// <summary>Adds the half-open interval [start, end).</summary>
    public void Insert(double start, double end, T value)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The end of an interval cannot be before its start.");
        }

        _root = Insert(_root, new Node(start, end, _sequence++, value));
        Count++;
    }

    /// <summary>Values whose intervals overlap the half-open window [from, to), ordered by start.</summary>
    public IEnumerable<T> Query(double from, double to)
    {
        var results = new List<T>();
        if (to > from)
        {
            Collect(_root, from, to, results);
        }

        return results;
    }

    /// <summary>All values ordered by start (in-order traversal).</summary>
    public IEnumerable<T> InOrder()
    {
        var stack = new Stack<Node>();
        var node = _root;

        while (stack.Count > 0 || node is not null)
        {
            while (node is not null)
            {
                stack.Push(node);
                node = node.Left;
            }

            node = stack.Pop();
            yield return node.Value;
            node = node.Right;
        }
    }

    /// <summary>Checks the AVL, ordering and max-end invariants; used by tests.</summary>
    internal bool IsValid() => Validate(_root, out _, out _);

    private static Node Insert(Node? node, Node added)
    {
        if (node is null)
        {
            return added;
        }

        if (Compare(added, node) < 0)
        {
            node.Left = Insert(node.Left, added);
        }
        else
        {
            node.Right = Insert(node.Right, added);
        }

        return Rebalance(node);
    }

    private static Node Rebalance(Node node)
    {
        Update(node);
        var balance = BalanceOf(node);

        if (balance > 1)
        {
            // Left-right case becomes left-left after rotating the child.
            if (BalanceOf(node.Left!) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }

            return RotateRight(node);
        }

        if (balance < -1)
        {
            // Right-left case becomes right-right after rotating the child.
            if (BalanceOf(node.Right!) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateRight(Node node)
    {
        var pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;
        Update(node);
        Update(pivot);
        return pivot;
    }

    private static Node RotateLeft(Node node)
    {
        var pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;
        Update(node);
        Update(pivot);
        return pivot;
    }

    private static void Update(Node node)
    {
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));
        node.MaxEnd = Math.Max(node.End, Math.Max(node.Left?.MaxEnd ?? double.MinValue, node.Right?.MaxEnd ?? double.MinValue));
    }

    private static void Collect(Node? node, double from, double to, List<T> results)
    {
        // Nothing in this subtree ends after the window starts.
        if (node is null || node.MaxEnd <= from)
        {
            return;
        }

        Collect(node.Left, from, to, results);

        // Everything to the right starts at or after this node, so stop once we pass the window.
        if (node.Start >= to)
        {
            return;
        }

        if (node.End > from)
        {
            results.Add(node.Value);
        }

        Collect(node.Right, from, to, results);
    }

    private static int Compare(Node a, Node b)
    {
        var byStart = a.Start.CompareTo(b.Start);
        return byStart != 0 ? byStart : a.Sequence.CompareTo(b.Sequence);
    }

    private static int HeightOf(Node? node) => node?.Height ?? 0;

    private static int BalanceOf(Node node) => HeightOf(node.Left) - HeightOf(node.Right);

    private static bool Validate(Node? node, out int height, out double maxEnd)
    {
        height = 0;
        maxEnd = double.MinValue;
        if (node is null)
        {
            return true;
        }

        if (!Validate(node.Left, out var leftHeight, out var leftMax) || !Validate(node.Right, out var rightHeight, out var rightMax))
        {
            return false;
        }

        height = 1 + Math.Max(leftHeight, rightHeight);
        maxEnd = Math.Max(node.End, Math.Max(leftMax, rightMax));

        return Math.Abs(leftHeight - rightHeight) <= 1 &&
               height == node.Height &&
               maxEnd == node.MaxEnd &&
               (node.Left is null || Compare(node.Left, node) < 0) &&
               (node.Right is null || Compare(node.Right, node) > 0);
    }

    private sealed class Node(double start, double end, long sequence, T value)
    {
        public double Start { get; } = start;

        public double End { get; } = end;

        public long Sequence { get; } = sequence;

        public T Value { get; } = value;

        public double MaxEnd { get; set; } = end;

        public int Height { get; set; } = 1;

        public Node? Left { get; set; }

        public Node? Right { get; set; }
    }
}
