using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scheduler.Components
{
    public class ObjectPool<T> where T : new()
    {
        private readonly Stack<T> _items = new Stack<T>();

        public T Get()
        {
            return _items.Count > 0 ? _items.Pop() : new T();
        }

        public void Release(T item)
        {
            _items.Push(item);
        }
    }
}
