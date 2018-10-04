using System;
using System.Threading.Tasks;

namespace DURF.Interfaces
{
    public interface IDispatcher
    {
        /// <summary>
        /// Awaitable method to async hop on UI thread and do something
        /// 
        /// If already on the UI thread, just executes action
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        Task BeginInvoke(Action action);

        /// <summary>
        /// Synchronous, blocking call to get on the UI thread
        /// 
        /// If already on the UI thread, action is executed immediately
        /// </summary>
        /// <param name="action"></param>
        void Invoke(Action action);

        /// <summary>
        /// Awaitable method to hop on UI thread and do something, awaiting it to possibly hop off in the middle
        /// and do something else
        /// 
        /// If already on the UI thread, awaits <paramref name="awaitable"/> immediately
        /// </summary>
        /// <param name="awaitable"></param>
        /// <returns></returns>
        Task BeginInvoke(Func<Task> awaitable);

        /// <summary>
        /// Awaitable method to hop on UI thread and do something, returning it's taks that possibly hops off in the middle
        /// and does something else.  This is a contradictory case and probably shoudl not ever happen...but you know.
        /// 
        /// If already on the UI thread, awaits <paramref name="awaitable"/> immediately
        /// </summary>
        /// <param name="awaitable"></param>
        /// <returns></returns>
        Task Invoke(Func<Task> awaitable);

        /// <summary>
        /// Call when you want the UI thread to settle down (process pending UI actions)
        ///
        /// Pumps the UI thread until all messages have been processed.
        /// </summary>
        /// <param name="priority"></param>
        void Wait();

        /// <summary>
        /// If true, caller is on that UI thread (in case you have multiple)
        /// </summary>
        /// <returns></returns>
        bool CheckAccess();
    }
}