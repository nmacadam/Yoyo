// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Collections.Generic;

namespace Yoyo.Runtime
{
	public class ThreadManager
	{
		private static readonly List<Action> _executeOnMainThread = new List<Action>();
        private static readonly List<Action> _executeCopiedOnMainThread = new List<Action>();
        private static bool _actionToExecuteOnMainThread = false;

        /// <summary>Sets an action to be executed on the main thread.</summary>
        /// <param name="action">The action to be executed on the main thread.</param>
        public static void ExecuteOnMainThread(Action action)
        {
            if (action == null)
            {
                UnityEngine.Debug.Log("No action to execute on main thread!");
                return;
            }

            lock (_executeOnMainThread)
            {
                _executeOnMainThread.Add(action);
                _actionToExecuteOnMainThread = true;
            }
        }

        /// <summary>Executes all code meant to run on the main thread. NOTE: Call this ONLY from the main thread.</summary>
        public static void UpdateMain()
        {
            if (_actionToExecuteOnMainThread)
            {
                _executeCopiedOnMainThread.Clear();
                lock (_executeOnMainThread)
                {
                    _executeCopiedOnMainThread.AddRange(_executeOnMainThread);
                    _executeOnMainThread.Clear();
                    _actionToExecuteOnMainThread = false;
                }

                for (int i = 0; i < _executeCopiedOnMainThread.Count; i++)
                {
                    _executeCopiedOnMainThread[i]();
                }
            }
        }
	}
}