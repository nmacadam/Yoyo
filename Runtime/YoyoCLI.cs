// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using System.Net;
using UnityEngine;

namespace Yoyo.Runtime
{
	internal static class YoyoCLI
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void ProcessArguments()
		{
			string[] args = System.Environment.GetCommandLineArgs();
			YoyoSession session = GameObject.FindObjectOfType<YoyoSession>();

			if (args.Length > 0 && session == null)
			{
				Debug.LogError("No YoyoSession is present in the default scene, ignoring arguments...");
				Console.WriteLine("No YoyoSession is present in the default scene, ignoring arguments...");
				return;
			}
			else if (args.Length == 0)
			{
				return;
			}

			// read flags with arguments
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i].Equals("-ip"))
				{
					if (IPAddress.TryParse(args[i + 1], out IPAddress address))
					{
						session.SetIP(address);
					}
				}
				else if (args[i].Equals("-port") && int.TryParse(args[i + 1], out int port))
				{
					session.SetPort(port);
				}
			}

			// read no-argument flags
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i].Equals("-server"))
				{
					session.StartServer();
				}
				else if (args[i].Equals("-client"))
				{
					session.StartClient();
				}
			}
		}
	}
}