/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;

namespace OnlineMaps
{
    /// <summary>
    /// Prevents re-entrant calls for a given target object by tracking active guards.
    /// </summary>
    public static class RecursionGuard
    {
        private static HashSet<object> guards = new HashSet<object>();

        /// <summary>
        /// Invokes the specified action if the target is not already protected.
        /// The target is protected for the duration of the action and released afterwards.
        /// </summary>
        /// <param name="target">The object to protect against re-entrance.</param>
        /// <param name="action">The action to execute if protection is acquired.</param>
        public static void Invoke(object target, Action action)
        {
            if (!Protect(target)) return;
            try
            {
                action();
            }
            finally
            {
                Release(target);
            }
        }

        /// <summary>
        /// Attempts to protect the specified target from re-entrance.
        /// </summary>
        /// <param name="target">The object to protect.</param>
        /// <returns>
        /// True if the target was not already protected and protection was acquired; otherwise false.
        /// </returns>
        public static bool Protect(object target)
        {
            if (target == null) return false;
            return guards.Add(target);
        }

        /// <summary>
        /// Releases protection for the specified target.
        /// </summary>
        /// <param name="target">The object to release protection for.</param>
        public static void Release(object target)
        {
            if (target == null) return;
            guards.Remove(target);
        }

        /// <summary>
        /// Attempts to execute the provided function if it is not already protected,
        /// returning the function result through an out parameter.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="func">The function to execute under protection.</param>
        /// <param name="value">When this method returns, contains the function result if execution succeeded; otherwise the default value of T.</param>
        /// <returns>
        /// True if the function was executed and a value was produced; false if the function was not executed because it was already protected.
        /// </returns>
        public static bool TryGet<T>(Func<T> func, out T value)
        {
            value = default;
            if (!Protect(func)) return false;

            try
            {
                value = func();
                return true;
            }
            finally
            {
                Release(func);
            }
        }
    }
}