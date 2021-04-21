// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEngine;

namespace Yoyo.Runtime
{
	/// <summary>
	/// Singleton access for critical networking info
	/// </summary>
	public class YoyoSessionInfo
	{
        private static object m_Lock = new object();
        private static YoyoSession _internalReference;

        private static YoyoSession _instance
        {
            get
            {
                lock (m_Lock)
                {
                    if (_internalReference == null)
                    {
                        _internalReference = GameObject.FindObjectOfType<YoyoSession>();
                    }

                    return _internalReference;
                }
            }
        }

		public static YoyoEnvironment Environment => _instance.Environment;
		public static bool IsActive => _instance.Environment != YoyoEnvironment.None;
		public static bool IsClient => _instance.Environment == YoyoEnvironment.Client;
		public static bool IsServer => _instance.Environment == YoyoEnvironment.Server;
	}
}