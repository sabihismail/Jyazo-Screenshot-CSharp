using System.Collections.Generic;
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace ScreenShot.src.tools.util
{
    public class ConcurrentLinkedList<T1>
    {
        private readonly object mutex = new();
        private readonly LinkedList<T1> lst = new();

        private readonly int maxCount;
        
        public ConcurrentLinkedList(int maxCount)
        {
            this.maxCount = maxCount;
        }

        public int Count
        {
            get
            {
                lock (mutex)
                {
                    return lst.Count;
                }
            }
        }

        public T1 Last
        {
            get
            {
                lock (mutex)
                {
                    return lst.Count == 0 ? default : lst.Last.Value;
                }
            }
        }

        public void AddFirst(T1 item)
        {
            lock (mutex)
            {
                lst.AddFirst(item);

                CheckSize();
            }
        }

        public void AddLast(T1 item)
        {
            lock (mutex)
            {
                lst.AddLast(item);
                
                CheckSize();
            }
        }

        public void RemoveFirst()
        {
            lock (mutex)
            {
                lst.RemoveFirst();
            }
        }

        public void RemoveLast()
        {
            lock (mutex)
            {
                lst.RemoveLast();
            }
        }

        private void CheckSize()
        {
            if (Count > maxCount)
            {
                RemoveFirst();
            }
        }
    }
}