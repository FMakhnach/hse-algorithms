using System;

namespace GrahamAlgorithm
{
    /// <summary>
    /// Stack with size restriction.
    /// </summary>
    /// <remarks> Implemented with nodes. </remarks>
    class Stack<T>
    {
        private Node top;
        private int maxSize;

        public const int MaxSize = 1000000;

        public int Size { get; private set; }

        public Stack(int maxSize)
        {
            // I don't like it and I don't understand the point of MaxSize. 
            if (maxSize > MaxSize)
            {
                throw new ArgumentException($"Max size cannot be bigger than {MaxSize}!");
            }

            top = null;
            Size = 0;
            this.maxSize = maxSize;
        }

        /// <summary>
        /// Indicates whether the stack is empty.
        /// </summary>
        public bool IsEmpty() => Size == 0;

        /// <summary>
        /// Returns the top element of the stack. 
        /// Throws exception if the stack is there is no next-to-top element.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public T NextToTop()
        {
            if (Size < 2)
            {
                throw new InvalidOperationException("There is no next-to-top element in the stack!");
            }
            return top.Next.Value;
        }

        /// <summary>
        /// Adds an element at the top of the stack. Throws exception if the stack is full.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Push(T item)
        {
            if (Size == maxSize)
            {
                throw new InvalidOperationException("Stack is full, cannot push a new element!");
            }
            top = new Node(item, top);
            ++Size;
        }

        /// <summary>
        /// Removes an element from top of the stack. 
        /// Throws exception if the stack is empty.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Pop()
        {
            if (Size == 0)
            {
                throw new InvalidOperationException("Stack is empty, cannot pop an element!");
            }
            top = top.Next;
            --Size;
        }

        /// <summary>
        /// Returns the top element of the stack. Throws exception if the stack is empty.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public T Top()
        {
            if (IsEmpty())
            {
                throw new InvalidOperationException("Stack is empty, cannot get top element!");
            }
            return top.Value;
        }

        /// <summary>
        /// Converts the stack to an array (from top to the bottom).
        /// </summary>
        public T[] ToArray()
        {
            T[] result = new T[Size];
            if (Size == 0)
            {
                return result;
            }
            Node it = top;
            for (int i = 0; i < Size; ++i)
            {
                result[i] = it.Value;
                it = it.Next;
            }
            return result;
        }


        private class Node
        {
            public T Value { get; }
            public Node Next { get; }

            public Node(T value, Node next)
            {
                Value = value;
                Next = next;
            }
        }
    }
}
