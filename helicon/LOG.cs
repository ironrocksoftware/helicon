
using System;
using IronRockUtils;

namespace helicon
{
	public class LOG
	{
		public static bool echo = true;

		public static void setDefaultLogOutput(string prefix)
		{
			Log.logFolder = AppDomain.CurrentDomain.BaseDirectory;
			if (!Log.logFolder.EndsWith("\\")) Log.logFolder += "\\";

			Log.logFolder += prefix + "_";
		}

		public static void write (string message)
		{
			if (echo)
				Console.WriteLine(message);

			Log.write (message);
		}

		public static void write (string message, bool allowEcho)
		{
			if (echo && allowEcho)
				Console.WriteLine(message);

			Log.write (message);
		}
	}
}
