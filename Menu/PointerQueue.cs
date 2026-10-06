using System.Collections.Generic;

namespace Poison.Menu
{
    internal sealed class PointerQueue<T>
    {
        private readonly Queue<T> items = new Queue<T>();
        private int lastFrame = -1;

        public int Count => items.Count;
        public void Enqueue(T item) => items.Enqueue(item);
        public void Clear()
        {
            items.Clear();
            lastFrame = -1;
        }

        public bool Take(int frame, bool repaint, out T item)
        {
            item = default;
            if (!repaint || frame == lastFrame || items.Count == 0) return false;
            lastFrame = frame;
            item = items.Dequeue();
            return true;
        }
    }
}
