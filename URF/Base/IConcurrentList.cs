using System;
using System.Collections;
using System.Collections.Generic;

namespace URF.Base 
{
    /// <summary>
    /// Queue like methods
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public interface IQueue<TType> : IList<TType>
    {
        /// <summary>
        /// Adds an item to a FIFO queue
        /// </summary>
        /// <param name="item"></param>
        void Enqueue(TType item);

        /// <summary>
        /// Removes and returns the next out item in a FIFO queue
        /// </summary>
        /// <returns></returns>
        TType Dequeue();

        /// <summary>
        /// Gets a ref to the last item in a FIFO queue
        /// </summary>
        /// <returns></returns>
        bool Any();
    }

    public interface IConcurrentList<TType> : IList, IQueue<TType>
    {
        /// <summary>
        /// Similiar to Add, but only does the add if not already in the list.  Basically, it lets you use a
        /// thread safe list like a hashset so it will only have a unique set of entities.  This assumes you only
        /// call AddIfNew of course
        /// </summary>
        /// <param name="item">Item we may need to add</param>
        /// <returns>True if item added.  False if it was already in the list.</returns>
        bool AddIfNew(TType item);

        /// <summary>
        /// Same as Add except returns the index and locks around the checking of size and adding so it is safe.
        /// </summary>
        /// <returns>Index of the newly added item</returns>
        int AddAndGetIndex(TType item);

        void ReplaceWith(IEnumerable<TType> items);

        /// <summary>
        /// Remove a list of items from the internal list under a single lock.
        /// </summary>
        /// <returns>Number removed.</returns>
        int Remove(IEnumerable<TType> items);

        int RemoveAndGetIndex(TType item);
        void AddRange(IEnumerable<TType> source);
        void RemoveAll(TType item);

        /// <summary>
        /// Safely transfer all elements from a thread safe list to this one.
        /// Locks both sides throughout the copy and the clear.
        /// </summary>
        /// <param name="source">Source list that will be transferred (and emptied)</param>
        void TransferElements(IList<TType> source);

        /// <summary>
        /// Pull out all elements of the list.  Done efficiently so no copying is
        /// needed - just transfer the internal list and reset it with a new one.
        /// </summary>
        List<TType> ExtractElements();

        /// <summary>
        /// Same as extension method, but thread safe.
        /// </summary>
        TType FirstOrDefault();

        /// <summary>
        /// Same as extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        /// <param name="checker"></param>
        /// <returns></returns>
        bool Any(Func<TType, bool> checker);

        /// <summary>
        /// Same as extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        bool Any();

        TType First();
        TType Last();

        /// <summary>
        /// Swap if needed so that the given item ends up at the desired index.  
        /// Performed in a single function so we can maintain the lock on the internal list.
        /// </summary>
        /// <returns>True if item ends up at the requested index.  False if not.</returns>
        bool Move(TType item, int desiredIndex);
    }
}