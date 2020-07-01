
using System;
using IronRockUtils;
using System.Text;
using System.Collections.Generic;

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

		public static string escapeField (string value, bool useQuotes)
		{
			if (!useQuotes) return value.Trim();
			return "\"" + value.Trim().Replace("\"", "\"\"") + "\"";
		}

		public static void loadIntoDatabase (SQLWrapper sql, string table, string filePath, bool dropTable, bool createTable, bool firstRowHeaders, char delimiter, bool removeQuotes)
		{
			System.IO.StreamReader inputFile = new System.IO.StreamReader(filePath);

			string[] cols2;
			int maxcols2 = 0;

			if (dropTable) {
				try { sql.execStmt("DROP TABLE " + table); } catch (Exception e) { }
			}

			try
			{
				string line = inputFile.ReadLine();

				if (firstRowHeaders == true || createTable == true)
				{
					cols2 = parseColumns(line, delimiter);

					if (removeQuotes == true)
					{
						for (int j = 0; j < cols2.Length; j++)
						{
							if (cols2[j].StartsWith("\"")) cols2[j] = cols2[j].Substring(1);
							if (cols2[j].EndsWith("\"")) cols2[j] = cols2[j].Substring(0, cols2[j].Length-1);
						}
					}

					if (createTable == true)
					{
						string temp = "";
						
						temp += "[source_path] VARCHAR(MAX)";
						temp += ",[source_datetime] DATETIME";

						maxcols2 = cols2.Length;
	
						for (int i = 0; i < cols2.Length; i++)
							temp += ",[" + cols2[i] + "] VARCHAR(MAX)";
	
						sql.execStmt("CREATE TABLE "+table+" ("+temp+")");
					}
				}

				int batch = 0;
				StringBuilder query = new StringBuilder (8192);

				if (firstRowHeaders == true)
					line = inputFile.ReadLine();

				int numrows = 0;
				long perf = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

				string source_path = "'" + filePath.Replace("'","''") + "'";
				string source_datetime = "'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "'";

				for (; line != null; line = inputFile.ReadLine())
				{
					cols2 = parseColumns(line, delimiter);

					if (removeQuotes == true)
					{
						for (int j = 0; j < cols2.Length; j++)
						{
							if (cols2[j].StartsWith("\"")) cols2[j] = cols2[j].Substring(1);
							if (cols2[j].EndsWith("\"")) cols2[j] = cols2[j].Substring(0, cols2[j].Length-1);
						}
					}

					numrows++;

					query.Append ("INSERT INTO "+table+" VALUES");
					query.Append ('(');
					query.Append(source_path);
					query.Append(source_datetime);

					for (int j = 0; j < maxcols2; j++)
					{
						query.Append(',');
						query.Append('\'');

						if (j < cols2.Length)
							query.Append(cols2[j].Replace("'","''"));

						query.Append('\'');
					}

					query.Append (')');
					query.Append (';');

					batch++;

					if (query.Length >= 6144)
					{
						sql.execStmt(query.ToString());

						query.Length = 0;
						batch = 0;
					}
				}

				if (batch != 0) {
					sql.execStmt(query.ToString());
				}

				inputFile.Close();
			}
			catch (Exception e)
			{
				if (inputFile != null)
					inputFile.Close();
				
				throw e;
			}
		}
		
		public static List<Dictionary<string, object>> loadIntoArray (string filePath, bool firstRowHeaders, char delimiter, bool removeQuotes)
		{
			System.IO.StreamReader inputFile = new System.IO.StreamReader(filePath);

			string[] cols2;
			int maxcols2 = 0;

			List<Dictionary<string, object>> output = new List<Dictionary<string, object>> ();
			string[] names;

			try
			{
				string line = inputFile.ReadLine();

				if (firstRowHeaders == true)
				{
					cols2 = parseColumns(line, delimiter);

					if (removeQuotes == true)
					{
						for (int j = 0; j < cols2.Length; j++)
						{
							if (cols2[j].StartsWith("\"")) cols2[j] = cols2[j].Substring(1);
							if (cols2[j].EndsWith("\"")) cols2[j] = cols2[j].Substring(0, cols2[j].Length-1);
						}
					}

					maxcols2 = cols2.Length;
					names = new String[maxcols2];

					for (int i = 0; i < cols2.Length; i++)
						names[i] = cols2[i];
				}
				else
				{
					cols2 = parseColumns(line, delimiter);

					maxcols2 = cols2.Length;
					names = new String[maxcols2];

					for (int i = 0; i < cols2.Length; i++)
						names[i] = "Col" + (i+1);
				}

				if (firstRowHeaders == true)
					line = inputFile.ReadLine();

				int numrows = 0;

				for (; line != null; line = inputFile.ReadLine())
				{
					cols2 = parseColumns(line, delimiter);

					if (removeQuotes == true)
					{
						for (int j = 0; j < cols2.Length; j++)
						{
							if (cols2[j].StartsWith("\"")) cols2[j] = cols2[j].Substring(1);
							if (cols2[j].EndsWith("\"")) cols2[j] = cols2[j].Substring(0, cols2[j].Length-1);
						}
					}

					numrows++;
					
					Dictionary<string, object> tmp = new Dictionary<string, object> ();
					output.Add(tmp);

					for (int j = 0; j < maxcols2; j++)
					{
						if (j < cols2.Length) {
							tmp.Add(names[j], cols2[j]);
						}
					}
				}

				inputFile.Close();
			}
			catch (Exception e)
			{
				if (inputFile != null)
					inputFile.Close();
				
				throw e;
			}

			return output;
		}
	}
}
