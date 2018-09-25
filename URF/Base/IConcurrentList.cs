using System;
using System.Collections;
using System.Collections.Generic;

namespace URF.Base 
{
    /// <summary>
    /// Queue like methods
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public interface IQueue<TType>
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
        /// Will dequeue if any items in queue
        /// </summary>
        /// <param name="item"></param>
        /// <returns>True if an item was dequeued</returns>
        bool TryDequeue(out TType item);

        /// <summary>
        /// Check to see if any items in queue
        /// </summary>
        /// <returns>True if any items in queue</returns>
        bool Any();
    }

    /// <summary>
    /// Queue like methods
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public interface IStack<TType>
    {
        /// <summary>
        /// Adds an item to a FILO stack onto the top
        /// </summary>
        /// <param name="item"></param>
        void Push(TType item);

        /// <summary>
        /// Removes the top of the FILO stack
        /// </summary>
        /// <returns></returns>
        TType Pop();

        /// <summary>
        /// Tries to pop an item off the stack, if any
        /// </summary>
        /// <param name="item"></param>
        /// <returns>False if no items on stack</returns>
        bool TryPop(out TType item);

        /// <summary>
        /// True if any items on the FILO stack
        /// </summary>
        /// <returns></returns>
        bool Any();
    }

    public interface IConcurrentList<TType> : IList, IQueue<TType>, IList<TType>
    {
        /// <summary>
        /// Similiar to Add, but only does the add if not already in the list.  Basically, it lets you use a
        /// thread safe list like a hashset so it will only have a unique set of entities.  This assumes you only
        /// call AddIfNew() in lieu of Add() for all additions
        /// </summary>
        /// <param name="item">Item we may need to add</param>
        /// <returns>True if item added.  False if it was already in the list.</returns>
        bool AddIfNew(TType item);

        /// <summary>
        /// Same as Add except returns the index and locks around the checking of size and adding so it is safe.
        /// </summary>
        /// <returns>Index of the newly added item</returns>
        int AddAndGetIndex(TType item);

        /// <summary>
        /// Replaces the contents of this collection with another
        /// </summary>
        /// <param name="items"></param>
        void ReplaceWith(IEnumerable<TType> items);

        /// <summary>
        /// Remove a list of items from the internal list under a single lock.
        /// </summary>
        /// <returns>Number removed.</returns>
        int Remove(IEnumerable<TType> items);

        int RemoveAndGetIndex(TType item);
        void AddRange(IEnumerable<TType> source);
        void RemoveAllInstances(TType item);

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
        /// Same as LINQ extension method, but thread safe.
        /// </summary>
        TType FirstOrDefault();

        /// <summary>
        /// Same as LINQ extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        /// <param name="checker"></param>
        /// <returns></returns>
        bool Any(Func<TType, bool> checker);

        /// <summary>
        /// Same as LINQ extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        bool Any();

        /// <summary>
        /// Same as LINQ extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        TType First();

        /// <summary>
        /// Same as LINQ extension method, but thread safe, and efficient since it doesn't get an enumerator
        /// </summary>
        TType Last();

        /// <summary>
        /// Swap if needed so that the given item ends up at the desired index.  
        /// Performed in a single function so we can maintain the lock on the internal list.
        /// </summary>
        /// <returns>True if item ends up at the requested index.  False if not.</returns>
        bool Move(TType item, int desiredIndex);
    }
}