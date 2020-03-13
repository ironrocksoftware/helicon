
using System;

namespace helicon
{
	public class CsvUtils
	{
		private static string[] stack = new String[8192];

		public static string[] parseColumns(string str, char delim)
		{
			int i, st = 0, state = 0, count = 0;
			char[] chars;

			str = str.Trim().Replace("\r", "") + "\n";
			chars = str.ToCharArray();

			for (i = 0; i < str.Length; i++)
			{
				switch (state)
				{
					case 0:
						if (chars[i] <= 32 && chars[i] != '\n')
							break;

						if (chars[i] == '"') state = 1; else state = 3;
						st = i;

						if (chars[i] == delim || chars[i] == '\n')
						{
							stack[count++] = str.Substring(st, i - st).Trim();
							state = 0;
						}

						break;

					case 1:
						if (chars[i] == '"') state = 2;
						break;

					case 2:
						if (chars[i] == '"')
						{
							state = 1;
							break;
						}

						stack[count++] = str.Substring(st, i - st).Trim();
						state = 4; i--;

						break;

					case 3:
						if (chars[i] == delim || chars[i] == '\n')
						{
							stack[count++] = str.Substring(st, i - st).Trim();
							state = chars[i] == delim ? 5 : 0;
						}
						break;

					case 4:
						if (chars[i] == delim || chars[i] == '\n') state = chars[i] == delim ? 5 : 0;
						break;

					case 5:
						if (chars[i] == '\n')
						{
							Console.Write("XXXXXXX\n");
							stack[count++] = "";
						}

						i--; state = 0;
						break;
				}
			}

			if (count == 0) return null;

			string[] temp = new string[count];

			for (i = 0; i < count; i++)
				temp[i] = stack[i];

			return temp;
		}

		private static string escapeField (string value, bool useQuotes)
		{
			if (!useQuotes) return value.Trim();
			return "\"" + value.Trim().Replace("\"", "\"\"") + "\"";
		}
	}
}
