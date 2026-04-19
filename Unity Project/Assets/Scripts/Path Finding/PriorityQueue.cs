using System;
using System.Collections.Generic;

public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
{
    private List<(TElement Element, TPriority Priority)> _nodes = new List<(TElement, TPriority)>();

    public int Count => _nodes.Count;

    public void Enqueue(TElement element, TPriority priority)
    {
        _nodes.Add((element, priority));
        int i = _nodes.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_nodes[i].Priority.CompareTo(_nodes[parent].Priority) >= 0) break;
            Swap(i, parent);
            i = parent;
        }
    }

    public TElement Dequeue()
    {
        TElement result = _nodes[0].Element;
        _nodes[0] = _nodes[_nodes.Count - 1];
        _nodes.RemoveAt(_nodes.Count - 1);

        int i = 0;
        while (true)
        {
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            int smallest = i;

            if (left < _nodes.Count && _nodes[left].Priority.CompareTo(_nodes[smallest].Priority) < 0) smallest = left;
            if (right < _nodes.Count && _nodes[right].Priority.CompareTo(_nodes[smallest].Priority) < 0) smallest = right;

            if (smallest == i) break;
            Swap(i, smallest);
            i = smallest;
        }
        return result;
    }

    private void Swap(int i, int j)
    {
        var temp = _nodes[i];
        _nodes[i] = _nodes[j];
        _nodes[j] = temp;
    }
}