
using System;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections;
using System.Xml;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using IronRockUtils;
using IronRockUtils.Json;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;
using MimeKit.Text;
using OpenPop.Mime;
using OpenPop.Mime.Header;
using OpenPop.Pop3;
using OpenPop.Pop3.Exceptions;
using OpenPop.Common.Logging;
using ImapX;
using ImapX.Enums;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Runtime.InteropServices;
using MimeKit;

namespace helicon
{
	class Program
	{
 		private const int STD_OUTPUT_HANDLE = -11;
    	private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    	[DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		public static extern uint GetLastError();

		/* *********************************************************** */
		private static Config config;
		private static System.Threading.Mutex mutex = null;
		private static FileInfo processFileInfo;

		private static string VERSION_NAME = "2.2.10";

		/* *********************************************************** */
		private static int VERSION;
		private static int CURRENT_VERSION;

		/* **************************imap********************************* */
		private static SQLWrapper SQL = null;
		private static Dictionary<string, object> CONTEXT = null;

		private static ImapX.ImapClient g_imap_client = null;
		private static MailKit.Net.Imap.ImapClient g_imap_clientx = null;
		private static Random random = new Random((int)(DateTime.Now.Ticks & 0x7FFFFFFF)); 

		/* *********************************************************** */
		public static int VersionInt (string value)
		{
			string[] vals = value.Split('.');

			int i = 0;
			int version = 0;

			for (; i < 4 && i < vals.Length; i++)
			{
				version += int.Parse(vals[i]);
				version *= 100;
			}

			for (; i < 4; i++)
				version *= 100;

			return version;
		}

		// *****************************************************
		// Formatter.

		private static byte[] GetByteArray (object value)
		{
			if (value.GetType().Name == "Byte[]")
				return (byte[])value;

			return System.Text.Encoding.UTF8.GetBytes(value.ToString());
		}

		private static string GetUtf8String (object value, int count=0)
		{
			if (value.GetType().Name == "Byte[]")
			{
				if (count != 0)
					return System.Text.Encoding.UTF8.GetString((byte[])value, 0, count);
				else
					return System.Text.Encoding.UTF8.GetString((byte[])value);
			}

			return Convert.ToString(value);
		}

		private static string GetHexString (byte[] value)
		{
			StringBuilder sb = new StringBuilder ();

			if (value != null)
			{
				for (int i = 0; i < value.Length; i++)
				{
					sb.Append(value[i].ToString("X2"));
				}
			}

			return "0x" + sb.ToString();
		}

		private static string Escape (string value)
		{
			return "'" + value.Replace("'", "''") + "'";
		}

		private static string UnEscape (string value)
		{
			string val = "";
			int state = 0;

			for (int i = 0; i < value.Length; i++)
			{
				if (state == 0 && value[i] != '\\')
				{
					val += value[i];
					continue;
				}

				if (state == 0)
				{
					state = 1;
					continue;
				}

				switch(value[i])
				{
					case '0': val += '\0'; break;
					case 'b': val += '\b'; break;
					case 't': val += '\t'; break;
					case 'n': val += '\n'; break;
					case 'f': val += '\f'; break;
					case 'v': val += '\v'; break;
					case 'r': val += '\r'; break;
					case '\'': val += '\''; break;
					case '"': val += '"'; break;
					case '/': val += '/'; break;
					case '\\': val += '\\'; break;
				}

				state = 0;
			}

			return val;
		}

		private static int ii;

		private static bool IsDigit(string s)
		{
			switch (s)
			{
				case "0":
				case "1":
				case "2":
				case "3":
				case "4":
				case "5":
				case "6":
				case "7":
				case "8":
				case "9":
					return true;

				default:
					return false;
			}
		}

		private static object ContextGet (string name)
		{
			if (name.Contains("/"))
			{
				object value = null;
				bool first = true;

				foreach (string i in name.Split('/'))
				{
					if (first)
					{
						value = ContextGet(i);
						first = false;
						continue;
					}

					if (value == null)
						break;

					if (i.StartsWith("#"))
					{
						try
						{
							int i0 = 0;

							if (IsDigit(i.Substring(1,1)))
								i0 = int.Parse(i.Substring(1));
							else
								i0 = int.Parse(ContextGet(i.Substring(1)).ToString());

							try {
								value = ((List<object>)value)[i0];
							} catch (InvalidCastException e1) {
								try {
									value = ((List<string>)value)[i0];
								}
								catch (InvalidCastException e2) {
									value = ((List<Dictionary<string,object>>)value)[i0];
								}
							}
						}
						catch (Exception e)
						{
							LOG.write("Unable to access index: " + i.Substring(1) + " => " + e.Message);
							value = null;
						}
						continue;
					}
					else if (i.StartsWith("@"))
					{
						try {
							string ii = ContextGet(i.Substring(1)).ToString();
							value = ((Dictionary<string,object>)value)[ii];
						}
						catch (Exception e) {
							value = null;
						}

						continue;
					}

					try {
						value = ((Dictionary<string,object>)value)[i];
					}
					catch (Exception e) {
						//LOG.write("Unable to access: " + i + " => " + e.Message);
						value = null;
					}
				}

				return value;
			}

			if (CONTEXT.ContainsKey(name))
				return CONTEXT[name];
			else
				return null;
		}

		private static object ProcessFmt (string value)
		{
			string[] val = value.Split(' ');

			object result = null;
			object result2 = null;

			Regex regex;
			MatchCollection matches;

			int tmp_i;

			switch (val[0])
			{
				case "HEXSTR":
					result = GetHexString(GetByteArray(ContextGet(val[1])));
					break;

				case "TOBASE64":
					result = System.Convert.ToBase64String(GetByteArray(ContextGet(val[1])));
					break;

				case "FROMBASE64":
					result = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(Convert.ToString(ContextGet(val[1]))));
					break;

				case "ESCAPE":
					result = Escape(Convert.ToString(ContextGet(val[1])));
					break;

				case "UNESCAPE":
					result = UnEscape(Convert.ToString(ContextGet(val[1])));
					break;

				case "STRING":
					result = GetUtf8String(GetByteArray(ContextGet(val[1])));
					break;

				case "INT":
					result = (int)GetDouble(ContextGet(val[1]));
					break;

				case "CHR":
					result = ((char)GetInt(val[1])).ToString();
					break;

				case "FLOAT":
					result = GetDouble(ContextGet(val[1]));
					break;

				case "FILE_SIZE":
					result = new FileInfo(ContextGet(val[1]).ToString()).Length;
					break;

				case "FILE_EXISTS":
					result = File.Exists(ContextGet(val[1]).ToString()) ? "1" : "0";
					break;

				case "DIR_EXISTS":
					result = Directory.Exists(ContextGet(val[1]).ToString()) ? "1" : "0";
					break;

				case "ENV":
					result = Environment.GetEnvironmentVariable(val[1]);
					break;

				case "SELF_PATH":
					result = AppDomain.CurrentDomain.BaseDirectory;
					break;

				case "MILLIS":
					result = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString();
					break;

				case "RAND":
					result = random.Next().ToString();

					if (val.Length == 2)
					{
						tmp_i = int.Parse(Convert.ToString(val[1]));
						while (((string)result).Length < tmp_i)
							result = (string)result + random.Next().ToString();

						result = ((string)result).Substring(0, tmp_i);
					}

					break;

				case "UUID":
					result = Guid.NewGuid().ToString();
					break;

				case "TEMPNAM":
					result = System.IO.Path.GetTempPath() + Guid.NewGuid().ToString() + ".tmp";
					break;

				case "REGEX_MATCH":
					regex = new Regex(GetUtf8String(GetByteArray(ContextGet(val[1]))), RegexOptions.ECMAScript | RegexOptions.IgnoreCase);

					string[] tmp = GetUtf8String(GetByteArray(ContextGet(val[2]))).Split('\n');

					result = "0";

					for (int i = 0; i < tmp.Length; i++)
					{
						matches = regex.Matches(tmp[i].Trim());
						if (matches.Count != 0) { result = "1"; break; }
					}

					break;

				case "REGEX_MATCH_CS":
					regex = new Regex(GetUtf8String(GetByteArray(ContextGet(val[1]))), RegexOptions.ECMAScript);

					string[] tmp1 = GetUtf8String(GetByteArray(ContextGet(val[2]))).Split('\n');

					result = "0";

					for (int i = 0; i < tmp1.Length; i++)
					{
						matches = regex.Matches(tmp1[i].Trim());
						if (matches.Count != 0) { result = "1"; break; }
					}

					break;

				case "REGEX_MATCH_ML":
					regex = new Regex(GetUtf8String(GetByteArray(ContextGet(val[1]))), RegexOptions.Multiline | RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
					matches = regex.Matches(GetUtf8String(GetByteArray(ContextGet(val[2]))));
					result = matches.Count != 0 ? "1" : "0";
					break;

				case "COUNT":
					var src = ContextGet(val[1]);
					try {
						result = ((List<object>)src).Count;
					}
					catch (InvalidCastException e1)
					{
						try {
							result = ((List<string>)src).Count;
						}
						catch (InvalidCastException e2)
						{
							try {
								result = ((List<Dictionary<string,object>>)src).Count;
							}
							catch (InvalidCastException e3)
							{
								try {
									result = ((Byte[])ContextGet(val[1])).Length;
								}
								catch (Exception e4) {
									result = "0";
								}
							}
						}
					}
					break;

				case "STRLEN":
					result = ContextGet(val[1]).ToString().Length;
					break;

				case "TRIM":
					result = ContextGet(val[1]).ToString().Trim();
					break;

				case "UPPER":
					result = ContextGet(val[1]).ToString().ToUpper();
					break;

				case "LOWER":
					result = ContextGet(val[1]).ToString().ToLower();
					break;

				case "TOSTRING":
					result = ContextGet(val[1]).ToString();
					break;

				case "DIRNAME":
					result = new FileInfo(ContextGet(val[1]).ToString()).DirectoryName;
					break;

				case "FILENAME":
					result = new FileInfo(ContextGet(val[1]).ToString()).Name;
					if (((string)result).LastIndexOf('.') != -1)
						result = ((string)result).Substring(0, ((string)result).LastIndexOf('.'));
					break;

				case "FILECTIME":
					result = new FileInfo(ContextGet(val[1]).ToString()).CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
					break;

				case "FILEMTIME":
					result = new FileInfo(ContextGet(val[1]).ToString()).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
					break;

				case "FILEATIME":
					result = new FileInfo(ContextGet(val[1]).ToString()).LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss");
					break;

				case "DATETIME":
				// DATETIME
				// DATETIME <DATE>
				// DATETIME <DATE> <TIME>
					if (val.Length == 1) {
						result = DateTime.Now;
					}
					else {
						if (val.Length == 3)
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
						else
							val[1] = Convert.ToString(ContextGet(val[1]));

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).ToString("yyyy-MM-dd HH:mm:ss");
					break;

				case "DATETIME_DATE":
					// DATETIME_DATE
					// DATETIME_DATE <DATE>
					// DATETIME_DATE <DATE> <TIME>
					if (val.Length == 1) {
						result = DateTime.Now;
					}
					else {
						if (val.Length == 3)
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
						else
							val[1] = Convert.ToString(ContextGet(val[1]));

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).ToString("yyyy-MM-dd");
					break;

				case "DATETIME_YEAR":
					// DATETIME_YEAR
					// DATETIME_YEAR <DATE>
					// DATETIME_YEAR <DATE> <TIME>
					if (val.Length == 1) {
						result = DateTime.Now;
					}
					else {
						if (val.Length == 3)
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
						else
							val[1] = Convert.ToString(ContextGet(val[1]));

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).ToString("yyyy");
					break;

				case "DATETIME_MONTH":
					// DATETIME_MONTH
					// DATETIME_MONTH <DATE>
					// DATETIME_MONTH <DATE> <TIME>
					if (val.Length == 1) {
						result = DateTime.Now;
					}
					else {
						if (val.Length == 3)
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
						else
							val[1] = Convert.ToString(ContextGet(val[1]));

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).ToString("MM");
					break;

				case "DATETIME_DAY":
					// DATETIME_DAY
					// DATETIME_DAY <DATE>
					// DATETIME_DAY <DATE> <TIME>
					if (val.Length == 1) {
						result = DateTime.Now;
					}
					else {
						if (val.Length == 3)
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
						else
							val[1] = Convert.ToString(ContextGet(val[1]));

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).ToString("dd");
					break;

				case "DATETIME_DELTA_DAYS":
					// DATETIME_DELTA_DAYS <DAYS>
					// DATETIME_DELTA_DAYS <DATE> <DAYS>
					// DATETIME_DELTA_DAYS <DATE> <TIME> <DAYS>

					if (val.Length == 2) {
						result = DateTime.Now;
						tmp_i = int.Parse(Convert.ToString(ContextGet(val[1])));
					}
					else {
						if (val.Length == 4)
						{
							val[1] = Convert.ToString(ContextGet(val[1])) + " " + Convert.ToString(ContextGet(val[2]));
							tmp_i = int.Parse(Convert.ToString(ContextGet(val[3])));
						}
						else
						{
							val[1] = Convert.ToString(ContextGet(val[1]));
							tmp_i = int.Parse(Convert.ToString(ContextGet(val[2])));
						}

						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					}

					result = ((DateTime)result).Add(new TimeSpan(tmp_i, 0, 0, 0)).ToString("yyyy-MM-dd");
					break;

				case "DATETIME_SPAN_DAYS":
					// DATETIME_SPAN_DAYS <A> <B>

					result = DateTime.ParseExact(Convert.ToString(ContextGet(val[1])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					result2 = DateTime.ParseExact(Convert.ToString(ContextGet(val[2])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);

					result = ((DateTime)result2 - (DateTime)result).TotalDays;
					break;

				case "DATETIME_SPAN_HOURS":
					// DATETIME_SPAN_HOURS <A> <B>

					result = DateTime.ParseExact(Convert.ToString(ContextGet(val[1])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					result2 = DateTime.ParseExact(Convert.ToString(ContextGet(val[2])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);

					result = ((DateTime)result2 - (DateTime)result).TotalHours;
					break;

				case "DATETIME_SPAN_MINUTES":
					// DATETIME_SPAN_MINUTES <A> <B>

					result = DateTime.ParseExact(Convert.ToString(ContextGet(val[1])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					result2 = DateTime.ParseExact(Convert.ToString(ContextGet(val[2])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);

					result = ((DateTime)result2 - (DateTime)result).TotalMinutes;
					break;

				case "DATETIME_SPAN_SECONDS":
					// DATETIME_SPAN_SECONDS <A> <B>

					result = DateTime.ParseExact(Convert.ToString(ContextGet(val[1])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
					result2 = DateTime.ParseExact(Convert.ToString(ContextGet(val[2])), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);

					result = ((DateTime)result2 - (DateTime)result).TotalSeconds;
					break;

				case "DATETIME_FORMAT":
					// DATETIME_FORMAT <FORMAT>
					// DATETIME_YEAR <DATE> <FORMAT>
					if (val.Length == 2) {
						result = DateTime.Now;
						result = ((DateTime)result).ToString(val[1]);
					}
					else {
						val[1] = Convert.ToString(ContextGet(val[1]));
						result = DateTime.ParseExact(val[1].ToString(), new string[] { "MM/dd/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "MM/dd/yyyy", "yyyy-MM-dd" }, null, System.Globalization.DateTimeStyles.None);
						result = ((DateTime)result).ToString(val[2]);
					}

					break;

				default:
					result = ContextGet(val[0]);
					break;
			}

			return result;
		}

		private static object Format (string value, bool _cont=false)
		{
			if (value == null)
				return null;

			string tmp = "", n_value = "";
			int s = 0;

			if (_cont == false) ii = 0;

			while (ii < value.Length)
			{
				switch (s)
				{
					case 0:
						if (value[ii] == ']' && ii+1 < value.Length && value[ii+1] == ']')
						{
							ii += 2;
							CONTEXT["TMP_LOCAL"] = ProcessFmt(n_value);
							return "TMP_LOCAL";
						}
						else if (value[ii] == '[')
							s = 1;
						else
							n_value += value[ii];
						break;

					case 1:
						if (value[ii] == '[')
						{
							s = 2;
							tmp = "";
						}
						else
						{
							n_value += "[" + value[ii];
							s = 0;
						}

						break;

					case 2:
						if (value[ii] == '[' && ii+1 < value.Length && value[ii+1] == '[')
						{
							ii += 2;
							tmp += Format(value, true);
							ii--;
						}
						else if (value[ii] == ']' && ii+1 < value.Length && value[ii+1] == ']')
						{
							if (ii+2 >= value.Length && n_value.Length == 0)
								return ProcessFmt(tmp);

							n_value += ProcessFmt(tmp);
							ii++;
							s = 0;
						}
						else
							tmp += value[ii];

						break;
				}

				ii++;
			}

			return n_value;
		}

		// *****************************************************
		// Xml data accessors.

		private static string GetInnerText (XmlElement node, string def)
		{
			if (node.InnerText.Length > 0)
				return Convert.ToString(node.InnerText.Trim());

			return Convert.ToString(def);
		}

		private static string FmtInnerText (XmlElement node, string def)
		{
			if (node.InnerText.Length > 0)
				return Convert.ToString(Format(node.InnerText.Trim()));

			return Convert.ToString(Format(def));
		}

		private static string FmtInnerXml (XmlElement node, string def)
		{
			if (node.InnerXml.Length > 0)
				return Convert.ToString(Format(node.InnerXml.Trim()));

			return Convert.ToString(Format(def));
		}

		private static object FmtInnerTextObj (XmlElement node, string def)
		{
			if (node.InnerText.Length > 0)
				return Format(node.InnerText.Trim());

			return Format(def);
		}

		private static string GetAttr (XmlElement node, string name, string def, bool trim=true)
		{
			if (trim)
				return Convert.ToString(XmlUtils.GetStringAttribute (node, name, def).Trim());
			else
				return Convert.ToString(XmlUtils.GetStringAttribute (node, name, def));
		}

		private static string FmtAttr (XmlElement node, string name, string def, bool trim=true)
		{
			if (trim)
				return Convert.ToString(Format (XmlUtils.GetStringAttribute (node, name, def).Trim()));
			else
				return Convert.ToString(Format (XmlUtils.GetStringAttribute (node, name, def)));
		}

		private static object FmtAttrObj (XmlElement node, string name, string def)
		{
			return Format("[[" + XmlUtils.GetStringAttribute(node, name, def) + "]]");
		}

		// *****************************************************
		// Converters.

		private static bool GetBool (object value)
		{
			string s = value.ToString();

			if (s == "1" || s.ToUpper() == "TRUE")
				return true;

			if (GetInt(value) > 0)
				return true;

			return false;
		}

		private static int GetInt (object value)
		{
			int result;

			if (int.TryParse(value.ToString(), out result))
				return result;

			return 0;
		}

		private static double GetDouble (object value)
		{
			double result;

			if (double.TryParse(value.ToString(), out result))
				return result;

			return 0;
		}

		/* *************************************************** */
		public static bool Eval (string sCSCode)
		{
			if (sCSCode[0] == '(')
				return GetBool(NewEvalExpr(sCSCode));

			throw new Exception ("Deprecated Eval detected: " + sCSCode);
/*
			CodeDomProvider icc = CodeDomProvider.CreateProvider("CSharp");
			CompilerParameters cp = new CompilerParameters();

			cp.CompilerOptions = "/t:library";
			cp.GenerateInMemory = true;

			StringBuilder sb = new StringBuilder("");

			sb.Append("namespace CSCodeEvaler{ \n");
			sb.Append("public class CSCodeEvaler{ \n");
			sb.Append("public object EvalCode(){\n");
			sb.Append("return ("+sCSCode+"); \n");
			sb.Append("} \n");
			sb.Append("} \n");
			sb.Append("}\n");

			CompilerResults cr = icc.CompileAssemblyFromSource(cp, sb.ToString());
			if (cr.Errors.Count > 0) throw new Exception (cr.Errors[0].ErrorText);

			System.Reflection.Assembly a = cr.CompiledAssembly;
			object o = a.CreateInstance("CSCodeEvaler.CSCodeEvaler");

			Type t = o.GetType();
			MethodInfo mi = t.GetMethod("EvalCode");

			object s = mi.Invoke(o, null);

			try { return (bool)s == true; } catch (Exception) { }
			try { return (int)s != 0; } catch (Exception) { }
*/
			return false;
		}

		static string expr;
		static int offs;

		public static void XSkipWhite()
		{
			while (offs < expr.Length && expr[offs] <= 32)
				offs++;
		}

		public static string XGetToken()
		{
			string token = "";

			if (offs >= expr.Length)
				return token;

			if (expr[offs] == '"')
			{
				offs++;

				while (offs < expr.Length && expr[offs] != '"')
					token += expr[offs++];

				offs++;

				token = Format(token).ToString();
			}
			else if (expr[offs] == '\'')
			{
				offs++;

				while (offs < expr.Length && expr[offs] != '\'')
					token += expr[offs++];

				offs++;

				token = Format(token).ToString();
			}
			else if (expr[offs] == '[')
			{
				while (offs < expr.Length && !token.EndsWith("]]"))
					token += expr[offs++];

				token = Format(token).ToString();
			}
			else
			{
				while (offs < expr.Length && expr[offs] != ' ' && expr[offs] != ')')
					token += expr[offs++];
			}

			return token;
		}

		public static object XEvalExpr()
		{
			XSkipWhite();

			if (expr[offs] == '(')
			{
				offs++;
				string op = XGetToken();
				double d1, d2;
				bool b1, b2;
				string s1, s2;
				object res = "";
				int i1, i2;

				switch (op.ToUpper())
				{
					case "+": case "ADD":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 + d2;
						break;

					case "-": case "SUB":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 - d2;
						break;

					case "*": case "MUL":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 * d2;
						break;

					case "/": case "DIV":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 / d2;
						break;

					case "//": case "IDIV":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = (int)(d1 / d2);
						break;

					case "!": case "NOT":
						d1 = GetDouble(XEvalExpr());
						res = d1 != 0 ? "0" : "1";
						break;

					case "==": case "EQ": case "EQUALS":
						s1 = XEvalExpr().ToString();
						s2 = XEvalExpr().ToString();
						res = s1 == s2 ? "1" : "0";
						break;

					case "!=": case "NE": case "NOT-EQUALS":
						s1 = XEvalExpr().ToString();
						s2 = XEvalExpr().ToString();
						res = s1 != s2 ? "1" : "0";
						break;

					case "<=": case "LE": case "LESS-EQUAL":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 <= d2 ? "1" : "0";
						break;

					case "<": case "LT": case "LESS-THAN":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 < d2 ? "1" : "0";
						break;

					case ">=": case "GE": case "GREATER-EQUAL":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 >= d2 ? "1" : "0";
						break;

					case ">": case "GT": case "GREATER-THAN":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 > d2 ? "1" : "0";
						break;

					case "MIN":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 < d2 ? d1 : d2;
						break;

					case "MAX":
						d1 = GetDouble(XEvalExpr());
						d2 = GetDouble(XEvalExpr());
						res = d1 > d2 ? d1 : d2;
						break;

					case "OR":
						b1 = GetBool(XEvalExpr());

						while (true)
						{
							XSkipWhite();
							if (expr[offs] == ')') break;

							b2 = GetBool(XEvalExpr());
							b1 = b1 || b2;
						}

						res = b1 ? "1" : "0";
						break;

					case "AND":
						b1 = GetBool(XEvalExpr());

						while (true)
						{
							XSkipWhite();
							if (expr[offs] == ')') break;

							b2 = GetBool(XEvalExpr());
							b1 = b1 && b2;
						}

						res = b1 ? "1" : "0";
						break;

					case "SUBSTR":
						i1 = GetInt(XEvalExpr());
						i2 = GetInt(XEvalExpr());

						s1 = XEvalExpr().ToString();

						if (s1.Length != 0)
						{
							if (i1 < 0) i1 += s1.Length;
							if (i2 < 0) i2 += s1.Length;

							if (i1 < 0) i1 = 0;
							if (i2 < 0) i2 = 0;

							if (i1 >= s1.Length) i1 = s1.Length-1;
							if (i2 >= s1.Length) i2 = s1.Length-1;

							res = s1.Substring(i1, i2);
						}
						else
							res = "";

						break;

					case "MID":
						i1 = GetInt(XEvalExpr());
						i2 = GetInt(XEvalExpr());

						s1 = XEvalExpr().ToString();

						if (s1.Length != 0)
						{
							if (i1 < 0) i1 += s1.Length;
							if (i1 < 0) i1 = 0;
							if (i1 >= s1.Length) i1 = s1.Length-1;

							if (i2 < 0) i2 += s1.Length+1 - i1;
							if (i2 < 0) i2 = 0;
							if (i1+i2 >= s1.Length) i2 = s1.Length-i1;

							res = s1.Substring(i1, i2);
						}
						else
							res = "";

						break;

					case "?": case "IF":
						b1 = GetBool(XEvalExpr());

						s1 = XEvalExpr().ToString();
						s2 = XEvalExpr().ToString();

						res = b1 ? s1 : s2;
						break;

					case "UPPER":
						res = XEvalExpr().ToString().ToUpper();
						break;

					case "LOWER":
						res = XEvalExpr().ToString().ToLower();
						break;

					case "STARTSWITH":
						s1 = XEvalExpr().ToString();
						res = XEvalExpr().ToString().StartsWith(s1) ? "1" : "0";
						break;

					case "ENDSWITH":
						s1 = XEvalExpr().ToString();
						res = XEvalExpr().ToString().EndsWith(s1) ? "1" : "0";
						break;

					case "STRLEN":
						s1 = XEvalExpr().ToString();
						res = s1.Length;
						break;

					case "CHRCOUNT":
						s1 = XEvalExpr().ToString();
						s2 = XEvalExpr().ToString();

						i1 = 0;

						for (int i = 0; i < s1.Length; i++)
							i1 += s2.IndexOf(s1[i]) != -1 ? 1 : 0;

						res = i1;
						break;

					case "RLIKE":
						s2 = XEvalExpr().ToString();
						s1 = XEvalExpr().ToString();

						Regex regex = new Regex(s1, RegexOptions.ECMAScript | RegexOptions.IgnoreCase | RegexOptions.Multiline);

						res = 0;

						var matches = regex.Matches(s2);
						if (matches.Count != 0) res = 1;

						break;
				}

				while (offs < expr.Length && expr[offs] != ')')
					offs++;

				if (offs >= expr.Length)
					throw new Exception("Possibly malformed condition, missing ')'.");

				offs++;
				return res.ToString();
			}

			return XGetToken();
		}

		public static object NewEvalExpr (string expr)
		{
			expr = expr.Replace("\n", " ");
			expr = expr.Replace("\r", " ");
			Program.expr = expr;
			Program.offs = 0;
			object result = null;

			try {
				result = XEvalExpr();

				if (Program.expr.Length != Program.offs)
					throw new Exception("Possibly malformed condition: " + Program.offs + " / " + Program.expr.Length);
			}
			catch (Exception e)
			{
				throw new Exception(e.Message + "\n" + "Expr: " + expr);
			}

			return result;
		}

		public static object EvalExpr (string sCSCode)
		{
			if (sCSCode[0] == '(') return NewEvalExpr(sCSCode);
			throw new Exception ("Deprecated eval detected: " + sCSCode);

			/*CodeDomProvider icc = CodeDomProvider.CreateProvider("CSharp");
			CompilerParameters cp = new CompilerParameters();

			cp.CompilerOptions = "/t:library";
			cp.GenerateInMemory = true;

			StringBuilder sb = new StringBuilder("");

			sb.Append("namespace CSCodeEvaler{ \n");
			sb.Append("public class CSCodeEvaler{ \n");
			sb.Append("public object EvalCode(){\n");
			sb.Append("return ("+sCSCode+"); \n");
			sb.Append("} \n");
			sb.Append("} \n");
			sb.Append("}\n");

			CompilerResults cr = icc.CompileAssemblyFromSource(cp, sb.ToString());
			if (cr.Errors.Count > 0) throw new Exception (cr.Errors[0].ErrorText);

			System.Reflection.Assembly a = cr.CompiledAssembly;
			object o = a.CreateInstance("CSCodeEvaler.CSCodeEvaler");

			Type t = o.GetType();
			MethodInfo mi = t.GetMethod("EvalCode");

			object s = mi.Invoke(o, null);
			return s;*/
			return null;
		}

		private static void EnableAnsiConsole()
		{
			var h = GetStdHandle(STD_OUTPUT_HANDLE);

			uint outConsoleMode = 0;

			if (!GetConsoleMode(h, out outConsoleMode))
				return;

			outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
			if (!SetConsoleMode(h, outConsoleMode)) return;
		}

		/* *************************************************** */
		public static void Main(string[] args)
		{
			EnableAnsiConsole();

			System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;

			if (args.Length == 1 && args[0] == "-v")
			{
				Console.WriteLine("\nhelicon: version " + VERSION_NAME);
				return;
			}

			Config.defFilename = "config.dat";

			try {
				config = new Config ("h3l1c0n#2020");
			}
			catch (Exception e) {
				LOG.write ("Error: Unable to load configuration (config.dat) possibly corrupt file.");
				LOG.write ("Message: " + e.Message);
				return;
			}

			if (args.Length == 0)
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new ConfigForm (config));

				return;
			}

			if (args.Length > 0)
			{
				FileInfo f = new FileInfo (args[0]);
				if (f.Exists)
				{
					bool createdNew;

					mutex = new System.Threading.Mutex (true, f.FullName.Replace("\\", "").Replace(" ", "").Replace(":", ""), out createdNew);
					if (!createdNew)
					{
						LOG.write ("Exiting because another instance is already running for file: " + f.FullName);
						return;
					}
				}

				LOG.setDefaultLogOutput(f.Name.Substring(0, f.Name.Length - f.Extension.Length));
			}

			try
			{
				ExecuteProcess (args[0], args, false);
			}
			catch (Exception e)
			{
				LOG.write ("Error: " + e.Message);
				LOG.write ("Stack Trace: " + e.StackTrace, false);
			}

			if (mutex != null) mutex.ReleaseMutex();
		}

		/* ******************************************** */
		public static void ExecuteProcess (string filename, string[] args, bool reEntry)
		{
			FileInfo f = new FileInfo (filename);
			if (!f.Exists)
			{
				f = new FileInfo (filename + ".xml");
				if (!f.Exists) {
					LOG.write ("Error: Input file not found: `" + filename + "'");
					return;
				}
			}

			processFileInfo = f;

			// *****************************************************
			// Open process description file.

			XmlDocument xml = new XmlDocument ();

			try {
				xml.Load (f.FullName);
			}
			catch (Exception e)
			{
				LOG.write ("Error: While opening process description file: `" + filename + "'");
				LOG.write ("Message: " + e.Message);
				return;
			}

			// *****************************************************
			// Check if the file has a valid version and signature.

			XmlElement elem = XmlUtils.FirstChildElement(xml);

			if (elem.Name != "Process")
			{
				LOG.write ("Error: Input file is missing the <Process/> header.");
				return;
			}

			string tmp = XmlUtils.GetStringAttribute (elem, "Version", VERSION_NAME);

			VERSION = VersionInt(tmp);
			CURRENT_VERSION = VersionInt(VERSION_NAME);

			if (VERSION > CURRENT_VERSION)
			{
				LOG.write ("Error: Process requires newer version " + tmp + " (current is " + VERSION_NAME + ").");
				return;
			}

			// *****************************************************
			// Prepare context.

			if (reEntry == false)
			{
				CONTEXT = new Dictionary<string, object> ();

				CONTEXT["TAB"] = "\t";
				CONTEXT["SP"] = " ";
				CONTEXT["NL"] = "\n";
				CONTEXT["LF"] = "\n";
				CONTEXT["CR"] = "\r";
				CONTEXT["CRLF"] = "\r\n";
				CONTEXT["@DEF"] = "\x1B[0m";
				CONTEXT["@RED"] = "\x1B[91m";
				CONTEXT["@GREEN"] = "\x1B[92m";
				CONTEXT["@YELLOW"] = "\x1B[93m";
				CONTEXT["@BLUE"] = "\x1B[94m";
				CONTEXT["@MAGENTA"] = "\x1B[95m";
				CONTEXT["@CYAN"] = "\x1B[96m";
				CONTEXT["@WHITE"] = "\x1B[97m";

				CONTEXT["ResponseHeaders"] = Api.outHeaders;

				if (args != null)
				{
					List<string> _args = new List<string> ();

					for (int i = 1; i < args.Length; i++)
						_args.Add(args[i]);

					CONTEXT["ARGS"] = _args;
				}
			}

			// *****************************************************
			// First ensure all actions are supported.

			int errors = ValidateActions (elem);
			if (errors != 0)
			{
				LOG.write("Fatal: " + errors + " error(s) were found before processing, unable to continue.");
				return;
			}

			// *****************************************************
			// Execute each action.

			try {
				ExecuteActions(elem);
			}
			catch (FalseException e) {
			}
			catch (Exception e) {
				LOG.write ("Error: " + e.Message);
				LOG.write ("Stack Trace: " + e.StackTrace, false);
			}

			if (SQL != null && reEntry == false)
				SQL.close();
		}

		// *****************************************************
		static int ValidateActions (XmlElement root)
		{
			int errors = 0;
			if (root == null) return 0;

			foreach (XmlNode xmlnode in root.ChildNodes)
			{
				if (xmlnode.NodeType != XmlNodeType.Element)
					continue;

				XmlElement node = (XmlElement)xmlnode;

				switch (node.Name)
				{
					case "SetLogPath":
					case "SetLogEcho":
					case "Echo":
					case "Trace":
					case "DumpVars":
					case "Stop":
					case "Skip":
					case "Repeat":
					case "RaiseException":
					case "Shell":
					case "SetVar":
					case "SetEnv":

					case "FileLoad":
					case "FileSave":
					case "FileAppend":
					case "FileDelete":
					case "FileCopy":
					case "FileMove":
					case "FileDownload":
					case "DirCreate":
					case "DirDelete":
					case "DirCopy":
					case "DirMove":
					case "DirInfo":
						continue;

					case "SqlOpen":
					case "SqlClose":
					case "SqlStatement":
					case "SqlLoadRow":
					case "SqlLoadArray":
					case "CsvLoadArray":
					case "FlattenData":
						continue;

					case "ForEachFile":
					case "ForEachDir":
					case "ForEachRow":
					case "ForEachIndex":
					case "ForEachKey":
					case "ForRange":
						errors += ValidateActions (node);
						continue;

					case "If":
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("True"));
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("False"));
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("Error"));
						continue;

					case "Block":
						errors += ValidateActions (node);
						continue;

					case "SafeBlock":
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("Execute"));
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("OnFailure"));
						errors += ValidateActions ((XmlElement)node.SelectSingleNode("OnSuccess"));
						continue;

					case "Subroutine":
						errors += ValidateActions (node);
						Subroutine(node);
						continue;

					case "CallSubroutine":
					case "Call":
					case "JsonLoad":
					case "ApiCall":
					case "Post":
					case "Request":
					case "Pop3LoadArray":

					case "ImapLoadArray":
					case "ImapOpen":
					case "ImapClose":
					case "ImapSetSeen":
					case "ImapLoadMessage":

					case "ImapLoadArrayX":
					case "ImapOpenX":
					case "ImapCloseX":
					case "ImapSetSeenX":
					case "ImapLoadMessageX":

					case "LoadEmlMessage":
					case "MsgLoadInfo":
					case "PdfLoadInfo":
					case "PdfLoadTextArray":
					case "PdfMerge":
					case "PdfSlice":
					case "PdfStamp":
					case "PdfOpen":
					case "PdfClose":
					case "PdfFind":
					case "PdfOverlay":
					case "PdfCleanup":
					case "IFilterLoadText":
					case "PdfLoadText":
					case "RegexExtract":
					case "SplitText":
					case "JoinItems":
					case "ReplaceText":
					case "Switch":
					case "SendMail":
					case "Sleep":
					case "ZipExtract":
					case "ZipCompress":
						continue;
				}

				LOG.write("Error: Unknown action specified: " + node.Name);
				errors++;
			}

			return errors;
		}

		// *****************************************************
		static bool ExecuteActions (XmlElement root)
		{
			if (root == null) return false;

			foreach (XmlNode xmlnode in root.ChildNodes)
			{
				if (xmlnode.NodeType != XmlNodeType.Element)
					continue;

				XmlElement node = (XmlElement)xmlnode;

				switch (node.Name)
				{
					case "SetLogPath":				SetLogPath(node); continue;
					case "SetLogEcho":				SetLogEcho(node); continue;
					case "Echo":					Echo(node); continue;
					case "Trace":					Trace(node); continue;
					case "DumpVars":				DumpVars(node); continue;
					case "Stop":					Stop(node); continue;
					case "Skip":					Skip(node); continue;
					case "Repeat":					Repeat(node); continue;
					case "RaiseException":			RaiseException(node); continue;
					case "Shell":					Shell(node); continue;
					case "SetVar":					SetVar(node); continue;
					case "SetEnv":					SetEnv(node); continue;

					case "FileLoad":				FileLoad(node); continue;
					case "FileSave":				FileSave(node); continue;
					case "FileAppend":				FileAppend(node); continue;
					case "FileDelete":				FileDelete(node); continue;
					case "FileCopy":				FileCopy(node); continue;
					case "FileMove":				FileMove(node); continue;
					case "FileDownload":			FileDownload(node); continue;
					case "DirCreate":				DirCreate(node); continue;
					case "DirDelete":				DirDelete(node); continue;
					case "DirCopy":					DirCopy(node); continue;
					case "DirMove":					DirMove(node); continue;
					case "DirInfo":					DirInfo(node); continue;

					case "SqlOpen":					SqlOpen(node); continue;
					case "SqlClose":				SqlClose(node); continue;
					case "SqlStatement":			SqlStatement(node); continue;
					case "SqlLoadRow":				SqlLoadRow(node); continue;
					case "SqlLoadArray":			SqlLoadArray(node); continue;
					case "CsvLoadArray":			CsvLoadArray(node); continue;
					case "FlattenData":				FlattenData(node); continue;

					case "ForEachFile":				ForEachFile(node); continue;
					case "ForEachDir":				ForEachDir(node); continue;
					case "ForEachRow":				ForEachRow(node); continue;
					case "ForEachIndex":			ForEachIndex(node); continue;
					case "ForEachKey":				ForEachKey(node); continue;
					case "ForRange":				ForRange(node); continue;
					case "If":						If(node); continue;

					case "SafeBlock":				SafeBlock(node); continue;
					case "Block":					Block(node); continue;
					case "Subroutine":				continue;
					case "CallSubroutine":			CallSubroutine(node); continue;
					case "Call":					Call(node); continue;
					case "JsonLoad":				JsonLoad(node); continue;
					case "ApiCall":					ApiCall(node); continue;
					case "Post":					Post(node); continue;
					case "Request":					Request(node); continue;

					case "Pop3LoadArray":			Pop3LoadArray(node); continue;
					case "ImapLoadArray":			ImapLoadArray(node); continue;
					case "ImapOpen":				ImapOpen(node); continue;
					case "ImapClose":				ImapClose(node); continue;
					case "ImapSetSeen":				ImapSetSeen(node); continue;
					case "ImapLoadMessage":			ImapLoadMessage(node); continue;

					case "LoadEmlMessage":			LoadEmlMessage(node); continue;
					case "MsgLoadInfo":				MsgLoadInfo(node); continue;
					case "PdfLoadInfo":				PdfLoadInfo(node); continue;
					case "PdfLoadTextArray":		PdfLoadTextArray(node); continue;
					case "PdfMerge":				PdfMerge(node); continue;
					case "PdfSlice":				PdfSlice(node); continue;
					case "PdfStamp":				PdfStamp(node); continue;
					case "PdfOpen":					PdfOpen(node); continue;
					case "PdfClose":				PdfClose(node); continue;
					case "PdfFind":					PdfFind(node); continue;
					case "PdfOverlay":				PdfOverlay(node); continue;
					case "PdfCleanup":				PdfCleanup(node); continue;
					case "IFilterLoadText":			IFilterLoadText(node); continue;
					case "PdfLoadText":				PdfLoadText(node); continue;
					case "RegexExtract":			RegexExtract(node); continue;
					case "SplitText":				SplitText(node); break;
					case "JoinItems":				JoinItems(node); break;
					case "ReplaceText":				ReplaceText(node); break;
					case "Switch":					Switch(node); break;
					case "SendMail":				SendMail(node); break;
					case "Sleep":					Sleep(node); break;
					case "ZipExtract":				ZipExtract(node); break;
					case "ZipCompress":				ZipCompress(node); break;
				}
			}

			return true;
		}

		private static bool NodeCheck (XmlElement node)
		{
			if (!node.HasAttribute("When"))
				return true;

			string condition = GetAttr(node, "When", "");
			if (condition.Length == 0) return true;

			if (Eval(condition.Replace('\'', '"')))
				return true;

			return false;
		}

		// #####################################################
		//                  ACTION HANDLERS
		// #####################################################

		// *****************************************************
		private static void SetLogPath (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			Log.logFolder = FmtInnerText (node, AppDomain.CurrentDomain.BaseDirectory);
			if (!Log.logFolder.EndsWith("\\")) Log.logFolder += "\\";
		}

		// *****************************************************
		private static void SetLogEcho (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			LOG.echo = GetBool (FmtInnerText (node, "FALSE"));
		}

		// *****************************************************
		private static void Echo (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try {
				string val;

				if (GetBool(FmtAttr(node, "Eval", "FALSE")) || GetBool(FmtAttr(node, "Expr", "FALSE")))
					val = (string)EvalExpr(GetInnerText(node, ""));
				else
					val = FmtInnerText(node, "");

				if (GetBool(FmtAttr(node, "NewLine", "TRUE")))
					Console.WriteLine(val);
				else
					Console.Write(val);
			}
			catch (Exception e)
			{
				throw new Exception ("Echo: " + e.Message);
			}
		}

		// *****************************************************
		private static void Trace (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			LOG.write (FmtInnerText (node, "(No Message Specified)"));
		}

		// *****************************************************
		private static void DumpVars (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			foreach (string i in CONTEXT.Keys)
			{
				Console.WriteLine(i);
			}
		}

		// *****************************************************
		private static void Stop (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			throw new StopException();
		}

		// *****************************************************
		private static void Skip (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			throw new SkipException();
		}

		// *****************************************************
		private static void Repeat (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			throw new RepeatException();
		}

		// *****************************************************
		private static void RaiseException (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			throw new Exception (FmtInnerText (node, "(User-Defined Exception)"));
		}

		// *****************************************************
		private static void Shell (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string command = FmtInnerText(node, "");

			if (command.Length == 0)
				throw new Exception ("Shell(): Empty command specified.");

			string a = Utils.Run("CMD.EXE", "/C " + command);

			CONTEXT["Shell"] = a;
		}

		// *****************************************************
		private static void SetVar (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (GetBool(FmtAttr(node, "Eval", "FALSE")) || GetBool(FmtAttr(node, "Expr", "FALSE")))
				CONTEXT[FmtAttr(node, "Name", "Var")] = EvalExpr(GetInnerText(node, ""));
			else if (GetBool(FmtAttr(node, "Object", "FALSE")))
				CONTEXT[FmtAttr(node, "Name", "Var")] = FmtInnerTextObj(node, "");
			else
			{
				if (GetBool(FmtAttr(node, "Append", "FALSE")))
					CONTEXT[FmtAttr(node, "Name", "Var")] += FmtInnerText(node, "");
				else
					CONTEXT[FmtAttr(node, "Name", "Var")] = FmtInnerText(node, "");
			}
		}

		// *****************************************************
		private static void SetEnv (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			Environment.SetEnvironmentVariable(FmtAttr(node, "Name", "Var"), FmtInnerText(node, ""), EnvironmentVariableTarget.Process);
		}

		// *****************************************************
		private static void FileLoad (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "FALSE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("FileLoad(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("FileLoad("+path+"): File not found.");

			string prefix = FmtAttr(node, "Prefix", "");
			if (prefix.Length > 0) prefix += ".";

			try
			{
				byte[] data = File.ReadAllBytes(path);

				CONTEXT[prefix + "FileData"] = data;
				CONTEXT[prefix + "FileDataSize"] = data.Length;
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("FileLoad("+path+"): " + e.Message);

				CONTEXT[prefix + "FileData"] = new byte[0];
				CONTEXT[prefix + "FileDataSize"] = 0;
			}
		}

		// *****************************************************
		private static void FileSave (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0) return;

			try
			{
				FileInfo fi = new FileInfo (path);

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				if (File.Exists(path))
					File.Delete(path);

				File.WriteAllBytes(path, GetByteArray(FmtInnerTextObj(node, "")));
			}
			catch (Exception e)
			{
				LOG.write ("Stack Trace: " + e.StackTrace, false);
				throw new Exception ("FileSave(" + path + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void FileAppend (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0) return;

			try
			{
				FileInfo fi = new FileInfo (path);

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				FileStream fs = new FileStream (path, FileMode.OpenOrCreate);
				fs.Seek(0, SeekOrigin.End);

				byte[] data = GetByteArray(FmtInnerTextObj(node, ""));
				fs.Write(data, 0, data.Length);

				fs.Close();
			}
			catch (Exception e)
			{
				throw new Exception ("FileAppend(" + path + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void FileDelete (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0) return;

			try
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			catch (Exception e)
			{
				throw new Exception ("FileDelete(" + path + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void FileCopy (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string src = FmtAttr(node, "Src", "");
			if (src.Length == 0) return;

			string dest = FmtAttr(node, "Dest", "");
			if (dest.Length == 0) return;

			FileInfo fi = new FileInfo(dest);

			try
			{
				if (!File.Exists(src))
					throw new Exception("Source file " + src + " does not exist, or access is denied.");

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				dest = fi.DirectoryName + "\\" + (fi.Name == "" ? new FileInfo(src).Name : fi.Name);
				File.Copy(src, dest, true);

				if (!File.Exists(dest))
					throw new Exception("Destination file " + dest + " does not exist. Copy failed.");
			}
			catch (Exception e)
			{
				throw new Exception ("FileCopy(" + src + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void FileMove (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string src = FmtAttr(node, "Src", "");
			if (src.Length == 0) return;

			string dest = FmtAttr(node, "Dest", "");
			if (dest.Length == 0) return;

			FileInfo fi = new FileInfo(dest);

			try
			{
				if (!File.Exists(src))
					throw new Exception("Source file " + src + " does not exist, or access is denied.");

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				string dst = fi.DirectoryName + "\\" + (fi.Name == "" ? new FileInfo(src).Name : fi.Name);

				if (File.Exists(dst))
					File.Delete(dst);

				File.Move(src, dst);

				if (!File.Exists(dst))
					throw new Exception("Destination file " + dest + " does not exist. Move failed.");

				if (File.Exists(src))
					throw new Exception("Source file " + src + " still exists. Move failed.");
			}
			catch (Exception e)
			{
				throw new Exception ("FileMove(" + src + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void FileDownload (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string url = FmtAttr(node, "Url", "");
			if (url.Length == 0) return;

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0) return;

			Api.clearRequest();
			Api.clearCookies();

			try
			{
				FileInfo fi = new FileInfo (path);

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				if (File.Exists(path))
					File.Delete(path);

				string tmp = Api.executeRequest(url, "get", false);
				byte[] data = Encoding.GetEncoding(1252).GetBytes(tmp);

				File.WriteAllBytes(path, data);
			}
			catch (Exception e)
			{
				throw new Exception ("FileDownload(" + path + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void DirCreate (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("DirCreate(): Empty path specified.");

				return;
			}

			if (Directory.Exists(path))
				return;

			try
			{
				Directory.CreateDirectory(path);
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("DirCreate("+path+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void DirDelete (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("DirDelete(): Empty path specified.");

				return;
			}

			if (!Directory.Exists(path))
				return;

			try
			{
				if (GetBool(FmtAttr(node, "Recursive", "FALSE")))
					Directory.Delete(path, true);
				else
					Directory.Delete(path);
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("DirDelete("+path+"): " + e.Message);
			}
		}

		private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
		{
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);
			if (!dir.Exists) throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);

			DirectoryInfo[] dirs = dir.GetDirectories();

			Directory.CreateDirectory(destDirName);

			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files)
			{
				string tempPath = System.IO.Path.Combine(destDirName, file.Name);
				file.CopyTo(tempPath, false);
			}

			if (copySubDirs)
			{
				foreach (DirectoryInfo subdir in dirs)
				{
					string tempPath = System.IO.Path.Combine(destDirName, subdir.Name);
					DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
				}
			}
		}

		// *****************************************************
		private static void DirCopy (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string src = FmtAttr(node, "Src", "");
			if (src.Length == 0) return;

			string dest = FmtAttr(node, "Dest", "");
			if (dest.Length == 0) return;

			try
			{
				if (!Directory.Exists(src))
					throw new Exception("Source dir " + src + " does not exist, or access is denied.");

				if (!Directory.Exists(dest))
					Directory.CreateDirectory(dest);

				DirectoryCopy (src, dest, GetBool(FmtAttr(node, "Recursive", "FALSE")));
			}
			catch (Exception e)
			{
				throw new Exception ("DirCopy(" + src + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void DirMove (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string src = FmtAttr(node, "Src", "");
			if (src.Length == 0) return;

			string dest = FmtAttr(node, "Dest", "");
			if (dest.Length == 0) return;

			try
			{
				if (!Directory.Exists(src))
					throw new Exception("Source dir " + src + " does not exist, or access is denied.");

				if (Directory.Exists(dest))
					Directory.Delete(dest, true);

				Directory.CreateDirectory((new DirectoryInfo(dest)).Parent.Name);

				Directory.Move(src, dest);
			}
			catch (Exception e)
			{
				throw new Exception ("DirMove(" + src + " => " + dest + "): " + e.Message);
			}
		}

		// *****************************************************
		private static void DirInfo (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string src = FmtAttr(node, "Path", "");
			if (src.Length == 0) return;

			CONTEXT["Dir.Name"] = "";
			CONTEXT["Dir.Exists"] = 0;
			CONTEXT["Dir.FullPath"] = "";
			CONTEXT["Dir.FileCount"] = 0;
			CONTEXT["Dir.DirCount"] = 0;

			if (Directory.Exists(src))
			{
				DirectoryInfo i = new DirectoryInfo(src);

				CONTEXT["Dir.Name"] = i.Name;
				CONTEXT["Dir.Exists"] = i.Exists ? 1 : 0;
				CONTEXT["Dir.FullPath"] = i.FullName;
				CONTEXT["Dir.FileCount"] = i.GetFiles().Length;
				CONTEXT["Dir.DirCount"] = i.GetDirectories().Length;
			}
		}

		// *****************************************************
		private static void SqlOpen (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (SQL != null) SQL.close();

			int timeout = GetInt(FmtAttr(node, "Timeout", "300"));
			int dataTimeout = GetInt(FmtAttr(node, "DataTimeout", "300"));

			SQL = new SQLWrapper (config, timeout, dataTimeout);

			if (!SQL.open())
				throw new Exception ("SqlOpen: Unable to connect to SQL database server.");
		}

		// *****************************************************
		private static void SqlClose (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (SQL != null) SQL.close();
			SQL = null;
		}

		// *****************************************************
		private static void ForEachFile (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			StringArray ext = new StringArray (FmtAttr(node, "WithExtension", ""), ' ').Trim().Clip().ToUpper();

			int minSize = GetInt(FmtAttr(node, "WithMinSize", "0"));
			int maxSize = GetInt(FmtAttr(node, "WithMaxSize", "0"));

			bool includeHidden = GetBool(FmtAttr(node, "IncludeHidden", "FALSE"));
			bool recursive = GetBool(FmtAttr(node, "Recursive", "FALSE"));
			string path = FmtAttr(node, "InDirectory", ".");

			FileInfo[] files;

			try {
				files = new DirectoryInfo (path).GetFiles("*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			}
			catch (Exception e) {
				throw new Exception ("ForEachFile("+path+"): " + e.Message);
			}

			foreach (FileInfo file in files)
			{
				if (!includeHidden && (file.Attributes & FileAttributes.Hidden) != 0)
					continue;

				string extension = file.Extension.Length > 0 ? file.Extension : ".";

				if (ext.Length > 0 && ext.IndexOf(extension.ToUpper()) == -1)
					continue;

				if (minSize != 0 && file.Length < minSize)
					continue;

				if (maxSize != 0 && file.Length > maxSize)
					continue;

				CONTEXT["File.Ext"] = extension;
				CONTEXT["File.Name"] = file.Name;
				CONTEXT["File.Path"] = file.DirectoryName;
				CONTEXT["File.Size"] = file.Length.ToString();
				CONTEXT["File.FullPath"] = file.FullName;

			Repeat:
				try {
					ExecuteActions(node);
				}
				catch (StopException e) {
					break;
				}
				catch (SkipException e) {
					continue;
				}
				catch (RepeatException e) {
					goto Repeat;
				}
				catch (Exception e)
				{
					LOG.write ("Error: ForEachFile("+file.Name+"): " + e.Message);
				}
			}

			CONTEXT.Remove("File.Ext");
			CONTEXT.Remove("File.Name");
			CONTEXT.Remove("File.Path");
			CONTEXT.Remove("File.Size");
			CONTEXT.Remove("File.FullPath");
		}

		// *****************************************************
		private static void ForEachDir (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool includeHidden = GetBool(FmtAttr(node, "IncludeHidden", "FALSE"));
			bool recursive = GetBool(FmtAttr(node, "Recursive", "FALSE"));
			string path = FmtAttr(node, "InDirectory", ".");

			DirectoryInfo[] dirs;

			try {
				dirs = new DirectoryInfo (path).GetDirectories("*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			}
			catch (Exception e) {
				throw new Exception ("ForEachDir("+path+"): " + e.Message);
			}

			foreach (DirectoryInfo dir in dirs)
			{
				if (!includeHidden && (dir.Attributes & FileAttributes.Hidden) != 0)
					continue;

				CONTEXT["Dir.Name"] = dir.Name;
				CONTEXT["Dir.FullPath"] = dir.FullName;
				CONTEXT["Dir.FileCount"] = dir.GetFiles().Length;
				CONTEXT["Dir.DirCount"] = dir.GetDirectories().Length;

			Repeat:
				try {
					ExecuteActions(node);
				}
				catch (StopException e) {
					break;
				}
				catch (SkipException e) {
					continue;
				}
				catch (RepeatException e) {
					goto Repeat;
				}
				catch (Exception e)
				{
					LOG.write ("Error: ForEachDir("+dir.Name+"): " + e.Message);
				}
			}

			CONTEXT.Remove("Dir.Name");
			CONTEXT.Remove("Dir.FullPath");
			CONTEXT.Remove("Dir.FileCount");
			CONTEXT.Remove("Dir.DirCount");
		}

		// *****************************************************
		private static void SqlStatement (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (SQL == null)
				throw new Exception ("SqlStatement: SQL connection is not open. Use <SqlOpen> first.");

			string query = FmtInnerText(node, "");
			if (String.IsNullOrEmpty(query))
			{
				LOG.write ("Warning: SqlStatement: Empty query specified.");
				return;
			}

			try {
				SQL.execStmt(query);
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("SqlStatement: " + e.Message);

				LOG.write ("Error: SqlStatement: " + e.Message);
			}
		}

		// *****************************************************
		private static void SqlLoadRow (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (SQL == null)
				throw new Exception ("SqlLoadRow: SQL connection is not open. Use <SqlOpen> first.");

			string query = FmtInnerText(node, "");
			if (String.IsNullOrEmpty(query))
			{
				LOG.write ("Warning: SqlLoadRow: Empty query specified.");
				return;
			}

			try
			{
				List<Dictionary<string, object>> result = SQL.getNvoArray(query);
				if (result.Count < 1) return;

				string prefix = FmtAttr(node, "Prefix", "");
				if (!String.IsNullOrEmpty(prefix)) prefix += ".";

				Dictionary<string, object> row = result[0];

				foreach (string name in row.Keys)
				{
					CONTEXT[prefix + name] = row[name];
				}
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("SqlLoadRow: " + e.Message);

				LOG.write ("Error: SqlLoadRow: " + e.Message);
			}
		}

		// *****************************************************
		private static void SqlLoadArray (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			if (SQL == null)
				throw new Exception ("SqlLoadArray: SQL connection is not open. Use <SqlOpen> first.");

			string query = FmtInnerText(node, "");
			if (String.IsNullOrEmpty(query))
			{
				LOG.write ("Warning: SqlLoadArray: Empty query specified.");
				return;
			}

			bool flat = GetBool(FmtAttr(node, "Flat", "FALSE"));

			try
			{
				List<Dictionary<string, object>> result = SQL.getNvoArray(query);

				if (flat)
				{
					List<Dictionary<string, object>> temp = new List<Dictionary<string, object>> ();

					foreach (Dictionary<string,object> row in result)
					{
						List<Dictionary<string,object>> tmp = new List<Dictionary<string, object>> ();

						foreach (string key in row.Keys)
						{
							Dictionary<string,object> x = new Dictionary<string, object> ();
							x.Add("Key", key);
							x.Add("Value", row[key]);
							tmp.Add(x);
						}

						Dictionary<string,object> y = new Dictionary<string, object> ();
						y.Add("Row", tmp);
						temp.Add(y);
					}

					CONTEXT[FmtAttr(node, "Into", "Array")] = temp;
				}
				else
					CONTEXT[FmtAttr(node, "Into", "Array")] = result;

				if (result.Count != 0)
				{
					List<Dictionary<string, object>> temp = new List<Dictionary<string, object>> ();

					foreach (string key in result[0].Keys)
					{
						Dictionary<string, object> x = new Dictionary<string, object> ();
						x.Add("Key", key);
						temp.Add(x);
					}

					CONTEXT[FmtAttr(node, "Into", "Array") + "_Keys"] = temp;
				}
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("SqlLoadArray: " + e.Message);

				LOG.write ("Error: SqlLoadArray: " + e.Message);
			}
		}

		// *****************************************************
		private static void CsvLoadArray (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));
			bool firstRowHeaders = GetBool(FmtAttr(node, "Header", "TRUE"));
			bool removeQuotes = GetBool(FmtAttr(node, "RemoveQuotes", "TRUE"));
			char delimiter = FmtAttr(node, "Delimiter", ",").ToCharArray()[0];

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict) throw new Exception ("CsvLoadArray(): Empty path specified.");
				return;
			}

			if (!File.Exists(path))
			{
				if (strict) throw new Exception ("CsvLoadArray(): Input dir does not exist.");
				return;
			}

			try
			{
				List<Dictionary<string, object>> result = CsvUtils.loadIntoArray(path, firstRowHeaders, delimiter, removeQuotes);
				CONTEXT[FmtAttr(node, "Into", "Array")] = result;
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("CsvLoadArray("+path+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void FlattenData (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string prefix = FmtAttr(node, "Prefix", "");
			if (!String.IsNullOrEmpty(prefix)) prefix += ".";

			List<Dictionary<string, object>> list;

			try {
				//list = (List<Dictionary<string, object>>)CONTEXT[FmtAttr(node, "From", "Array")];
				list = (List<Dictionary<string, object>>)FmtAttrObj(node, "From", "Array");
				if (list == null) throw new Exception ("Input array "+FmtAttr(node, "From", "Array")+" is null.");
			}
			catch (Exception e) {
				throw new Exception ("FlattenData: Unable to get Array: " + e.Message);
			}

			try
			{
				List<Dictionary<string, object>> temp = new List<Dictionary<string, object>> ();

				foreach (Dictionary<string,object> row in list)
				{
					List<Dictionary<string,object>> tmp = new List<Dictionary<string, object>> ();

					foreach (string key in row.Keys)
					{
						Dictionary<string,object> x = new Dictionary<string, object> ();
						x.Add("Key", key);
						x.Add("Value", row[key]);
						tmp.Add(x);
					}

					Dictionary<string,object> y = new Dictionary<string, object> ();
					y.Add("Row", tmp);
					temp.Add(y);
				}

				CONTEXT[FmtAttr(node, "Into", "Array")] = temp;

				if (list.Count != 0)
				{
					List<Dictionary<string, object>> temp2 = new List<Dictionary<string, object>> ();

					foreach (string key in list[0].Keys)
					{
						Dictionary<string, object> x = new Dictionary<string, object> ();
						x.Add("Key", key);
						temp2.Add(x);
					}

					CONTEXT[FmtAttr(node, "Into", "Array") + "_Keys"] = temp2;
				}
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("FlattenData: " + e.Message);

				LOG.write ("Error: FlattenData: " + e.Message);
			}
		}

		// *****************************************************
		private static void ForEachRow (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string prefix = FmtAttr(node, "Prefix", "");
			if (!String.IsNullOrEmpty(prefix)) prefix += ".";

			List<Dictionary<string, object>> list;

			try {
				//list = (List<Dictionary<string, object>>)CONTEXT[FmtAttr(node, "In", "Array")];
				list = (List<Dictionary<string, object>>)FmtAttrObj(node, "In", "Array");
				if (list == null) throw new Exception ("Input array "+FmtAttr(node, "In", "Array")+" is null.");
			}
			catch (Exception e) {
				throw new Exception ("ForEachRow: Unable to get Array: " + e.Message);
			}

			foreach (Dictionary<string, object> row in list)
			{
				if (row == null)
					continue;

				foreach (string name in row.Keys)
					CONTEXT[prefix + name] = row[name];

			Repeat:
				try {
					ExecuteActions(node);
				}
				catch (StopException e) {
					break;
				}
				catch (SkipException e) {
					continue;
				}
				catch (RepeatException e) {
					goto Repeat;
				}
				catch (Exception e) {

					if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
						throw new Exception ("ForEachRow: " + e.Message);

					LOG.write ("Error: ForEachRow: " + e.Message);
				}
			}

			if (list.Count > 0)
			{
				foreach (string name in list[0].Keys)
					CONTEXT.Remove(prefix + name);
			}
		}

		// *****************************************************
		private static void ForEachIndex (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string varName = FmtAttr(node, "VarName", "X");
			List<object> listA = null;
			List<Dictionary<string, object>> listB = null;

			try {
				//listA = (List<object>)CONTEXT[FmtAttr(node, "In", "Array")];
				object  x = FmtAttrObj(node, "In", "Array");
				listA = (List<object>)FmtAttrObj(node, "In", "Array");
				if (listA == null) throw new Exception ("Input array "+FmtAttr(node, "In", "Array")+" is null.");
			}
			catch (InvalidCastException e)
			{
				//listB = (List<Dictionary<string, object>>)CONTEXT[FmtAttr(node, "In", "Array")];
				listB = (List<Dictionary<string, object>>)FmtAttrObj(node, "In", "Array");
				if (listB == null) throw new Exception ("Input array "+FmtAttr(node, "In", "Array")+" is null.");
			}
			catch (Exception e) {
				throw new Exception ("ForEachIndex: Unable to get Array: " + e.Message);
			}

			if (listA != null)
			{
				foreach (object field in listA)
				{
					if (field == null)
						continue;

					CONTEXT[varName] = field;

				Repeat:
					try {
						ExecuteActions(node);
					}
					catch (StopException e) {
						break;
					}
					catch (SkipException e) {
						continue;
					}
					catch (RepeatException e) {
						goto Repeat;
					}
					catch (Exception e) {

						if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
							throw new Exception ("ForEachIndex: " + e.Message);

						LOG.write ("Error: ForEachIndex: " + e.Message);
					}
				}
			}
			else if (listB != null)
			{
				foreach (object field in listB)
				{
					if (field == null)
						continue;

					CONTEXT[varName] = field;

				Repeat:
					try {
						ExecuteActions(node);
					}
					catch (StopException e) {
						break;
					}
					catch (SkipException e) {
						continue;
					}
					catch (RepeatException e) {
						goto Repeat;
					}
					catch (Exception e) {

						if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
							throw new Exception ("ForEachIndex: " + e.Message);

						LOG.write ("Error: ForEachIndex: " + e.Message);
					}
				}
			}

			CONTEXT.Remove(varName);
		}

		// *****************************************************
		private static void ForEachKey (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string varName = FmtAttr(node, "VarName", "X");
			Dictionary<string, object> list = null;

			try {
				//list = (Dictionary<string, object>)CONTEXT[FmtAttr(node, "In", "Array")];
				list = (Dictionary<string, object>)FmtAttrObj(node, "In", "Array");
				if (list == null) throw new Exception ("Input array "+FmtAttr(node, "In", "Array")+" is null.");
			}
			catch (Exception e) {
				throw new Exception ("ForEachKey: Unable to get Array: " + e.Message);
			}

			foreach (string field in list.Keys)
			{
				CONTEXT[varName + ".key"] = field;
				CONTEXT[varName] = list[field];

			Repeat:
				try {
					ExecuteActions(node);
				}
				catch (StopException e) {
					break;
				}
				catch (SkipException e) {
					continue;
				}
				catch (RepeatException e) {
					goto Repeat;
				}
				catch (Exception e) {

					if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
						throw new Exception ("ForEachKey: " + e.Message);

					LOG.write ("Error: ForEachKey: " + e.Message);
				}
			}

			CONTEXT.Remove(varName + ".key");
			CONTEXT.Remove(varName);
		}

		// *****************************************************
		private static void ForRange (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string var = FmtAttr(node, "Var", "I");
			int from = GetInt(FmtAttr(node, "From", "0"));
			int count = GetInt(FmtAttr(node, "Count", "0"));

			for (; count != 0; count--, from++)
			{
				CONTEXT[var] = from.ToString();

			Repeat:
				try {
					ExecuteActions(node);
				}
				catch (StopException e) {
					break;
				}
				catch (SkipException e) {
					continue;
				}
				catch (RepeatException e) {
					goto Repeat;
				}
				catch (Exception e) {
					LOG.write ("Error: ForRange: " + e.Message);
					LOG.write ("Stack Trace: " + e.StackTrace, false);
				}
			}

			CONTEXT[var] = from.ToString();
		}

		// *****************************************************
		private static void If (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string condition = GetAttr(node, "Condition", "");
			if (condition == "") return;

			condition = condition.Replace('\'', '"');

			XmlElement ifTrue = (XmlElement)node.SelectSingleNode("True");
			XmlElement ifFalse = (XmlElement)node.SelectSingleNode("False");
			XmlElement ifError = (XmlElement)node.SelectSingleNode("Error");

			bool value = false;

			try {
				value = Eval(condition);
			}
			catch (Exception e)
			{
				CONTEXT["ERRSTR"] = e.Message;

				try {
					ExecuteActions(ifError);
				}
				catch (FalseException e2) {
					throw e2;
				}
				finally {
					CONTEXT.Remove("ERRSTR");
				}

				return;
			}

			try {
				ExecuteActions(value == true ? ifTrue : ifFalse);
			}
			catch (FalseException e2) {
				throw e2;
			}
		}

		// *****************************************************
		private static void SafeBlock (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			XmlElement execute = (XmlElement)node.SelectSingleNode("Execute");
			XmlElement onFailure = (XmlElement)node.SelectSingleNode("OnFailure");
			XmlElement onSuccess = (XmlElement)node.SelectSingleNode("OnSuccess");

			if (execute == null)
				return;

			FalseException se1 = null;

			try {
				ExecuteActions(execute);
			}
			catch (FalseException e) {
				se1 = e;
			}
			catch (Exception e)
			{
				if (onFailure != null)
				{
					CONTEXT["ERRSTR"] = e.Message;

					try {
						ExecuteActions(onFailure);
					}
					catch (FalseException e2) {
						throw e2;
					}
					catch (Exception e2)
					{
						LOG.write ("Error: SafeBlock: " + e.Message);
						LOG.write ("Error: SafeBlock: onFailure: " + e2.Message);
					}
					finally {
						CONTEXT.Remove("ERRSTR");
					}

					return;
				}

				LOG.write ("Error: SafeBlock: " + e.Message);
				return;
			}

			if (onSuccess != null)
			{
				try {
					ExecuteActions(onSuccess);
				}
				catch (FalseException e2) {
					throw e2;
				}
				catch (Exception e2)
				{
					LOG.write ("Error: SafeBlock: onSuccess: " + e2.Message);
				}
			}

			if (se1 != null) throw se1;
		}

		// *****************************************************
		private static void Block (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			ExecuteActions(node);
		}

		// *****************************************************
		private static void Subroutine (XmlElement node)
		{
			string name = FmtAttr(node, "Name", "");
			if (name == "") return;

			CONTEXT["SUBROUTINE_"+name] = node;
		}

		// *****************************************************
		private static void CallSubroutine (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string name = FmtAttr(node, "Name", "");
			if (name == "") return;

			try
			{
				if (!CONTEXT.ContainsKey("SUBROUTINE_"+name))
				{
					LOG.write("Error: Subroutine " + name + " does not exist.");
					return;
				}

				node = (XmlElement)CONTEXT["SUBROUTINE_"+name];
				ExecuteActions (node);
			}
			catch (StopException e) {
			}
		}

		// *****************************************************
		private static void Call (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string name = FmtInnerText(node, "");
			if (name == "") return;

			try
			{
				if (!name.EndsWith(".xml")) name += ".xml";

				if (!File.Exists(name))
					name = processFileInfo.DirectoryName + "\\" + name;

				if (!File.Exists(name))
					throw new Exception ("File '" + name + "' was not found. Unable to execute it.");

				FileInfo oldProcessFileInfo = processFileInfo;

				ExecuteProcess (name, null, true);

				processFileInfo = oldProcessFileInfo;
			}
			catch (Exception e)
			{
				LOG.write ("(Call) ["+name+"] Error: " + e.Message);
				LOG.write ("Stack Trace: " + e.StackTrace, false);
			}
		}

		// *****************************************************
		private static void JsonLoad (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string prefix = FmtAttr(node, "Prefix", "Json");
			if (!prefix.EndsWith(".")) prefix += ".";

			JsonElement elem = JsonElement.fromString(FmtInnerText(node, ""));
			object data = JsonToVars(elem, false);

			string varName = FmtAttr(node, "VarName", "");
			if (!String.IsNullOrEmpty(varName))
				CONTEXT[varName] = data;
			else
				CONTEXT[prefix+"DATA"] = data;
		}

		// *****************************************************
		private static void ApiCall (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string url = FmtAttr(node, "Url", "");
			if (url == "") return;

			string prefix = FmtAttr(node, "Prefix", "Api");
			if (!prefix.EndsWith(".")) prefix += ".";

			string response = FmtAttr(node, "ResponseType", "JSON").ToUpper();
			if (response!="RAW" && response!="JSON") response = "JSON";

			string method = FmtAttr(node, "Method", "post").ToLower();
			if (method!="get" && method!="post") method = "post";

			string auth = FmtAttr(node, "Auth", "").Trim();
			if (auth == "") auth = null;

			string auth2 = FmtAttr(node, "Authorization", "").Trim();
			if (auth2 != "") auth = "!!*" + auth2;

			bool debug = GetBool(FmtAttr(node, "Debug", "false"));

			Api.clearRequest();

			if (GetBool(FmtAttr(node, "ClearCookies", "false")))
				Api.clearCookies();

			if (node.HasChildNodes)
			{
				foreach (XmlNode xmlnode in node.ChildNodes)
				{
					if (xmlnode.NodeType != XmlNodeType.Element)
						continue;

					XmlElement field = (XmlElement)xmlnode;

					string fromFile = FmtAttr(field, "FromFile", "");
					string filename = FmtAttr(field, "Filename", "");
					string fieldName = FmtAttr(field, "Field", "");

					if (fieldName == "")
						fieldName = field.Name;

					if (fromFile != "")
						Api.addRequestFile (fieldName, filename != "" ? filename : new FileInfo(fromFile).Name, File.ReadAllBytes(fromFile));
					else if (filename != "")
						Api.addRequestFile (fieldName, filename, GetByteArray(FmtInnerTextObj(field, "")));
					else
						Api.addRequestField(fieldName, FmtInnerText(field, ""));
				}
			}

			string tmp;
			CONTEXT[prefix + "raw"] = tmp = Api.executeRequestJson(url, method, auth, response == "JSON");
			CONTEXT[prefix + "error"] = Api.errstr;
			CONTEXT[prefix + "rawBytes"] = Encoding.GetEncoding(1252).GetBytes(tmp);

			if (response == "JSON")
			{
				JsonElement elem = Api.jsonResponse;

				if (elem.type == JsonElementType.OBJECT)
				{
					foreach (string keyName in elem.getKeys())
						CONTEXT[prefix + keyName] = elem[keyName].ToValue();
				}

				CONTEXT[prefix + "DATA"] = JsonToVars(elem, debug);
			}
		}

		// *****************************************************
		private static void Post (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string url = FmtAttr(node, "Url", "");
			if (url == "") return;

			string prefix = FmtAttr(node, "Prefix", "Api");
			if (!prefix.EndsWith(".")) prefix += ".";

			string response = FmtAttr(node, "ResponseType", "JSON").ToUpper();
			if (response!="RAW" && response!="JSON") response = "JSON";

			string contentType = FmtAttr(node, "ContentType", "application/octet-stream").ToLower();
			byte[] data = GetByteArray(FmtInnerTextObj(node, ""));

			string auth = FmtAttr(node, "Auth", "").Trim();
			if (auth == "") auth = null;

			bool debug = GetBool(FmtAttr(node, "Debug", "false"));

			Api.clearRequest();

			if (GetBool(FmtAttr(node, "ClearCookies", "false")))
				Api.clearCookies();

			string tmp;
			CONTEXT[prefix + "raw"] = tmp = Api.postData(url, contentType, data, auth, response == "JSON");
			CONTEXT[prefix + "error"] = Api.errstr;
			CONTEXT[prefix + "rawBytes"] = Encoding.GetEncoding(1252).GetBytes(tmp);

			if (response == "JSON")
			{
				JsonElement elem = Api.jsonResponse;

				if (elem.type == JsonElementType.OBJECT)
				{
					foreach (string keyName in elem.getKeys())
						CONTEXT[prefix + keyName] = elem[keyName].ToValue();
				}

				CONTEXT[prefix + "DATA"] = JsonToVars(elem, debug);
			}
		}

		// *****************************************************
		private static void Request (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string url = FmtAttr(node, "Url", "");
			if (url == "") return;

			string varName = FmtAttr(node, "VarName", "Res");
			string method = FmtAttr(node, "Method", "GET").ToUpper();
			string[] headers = FmtAttr(node, "Header", "").Split(';');

			string contentType = FmtAttr(node, "ContentType", "").ToLower();
			byte[] data = null;

			Api.clearRequest();

			if (GetBool(FmtAttr(node, "ClearCookies", "true")))
				Api.clearCookies();

			if (contentType == "")
			{
				if (node.HasChildNodes)
				{
					foreach (XmlNode xmlnode in node.ChildNodes)
					{
						if (xmlnode.NodeType != XmlNodeType.Element)
							continue;
	
						XmlElement field = (XmlElement)xmlnode;
	
						string fromFile = FmtAttr(field, "FromFile", "");
						string filename = FmtAttr(field, "Filename", "");
						string fieldName = FmtAttr(field, "Field", "");

						if (fieldName == "")
							fieldName = field.Name;

						if (fromFile != "")
							Api.addRequestFile (fieldName, filename != "" ? filename : new FileInfo(fromFile).Name, File.ReadAllBytes(fromFile));
						else if (filename != "")
							Api.addRequestFile (fieldName, filename, GetByteArray(FmtInnerTextObj(field, "")));
						else
							Api.addRequestField(fieldName, FmtInnerText(field, ""));
					}
				}
			}
			else
				data = GetByteArray(FmtInnerTextObj(node, ""));

			string auth = FmtAttr(node, "Auth", "").Trim();
			if (auth == "") auth = null;

			bool debug = GetBool(FmtAttr(node, "Debug", "false"));

			string tmp;
			CONTEXT[varName] = Api.runRequest(url, method, contentType, data, auth, headers);
			CONTEXT["ERRSTR"] = Api.errstr;
			CONTEXT["HTTP_CODE"] = Api.responseCode;
		}

		private static object JsonToVars (JsonElement elem, bool debug=false, string pref="")
		{
			if (elem.type == JsonElementType.ARRAY)
			{
				List<object> list = new List<object> ();

				if (debug) Console.WriteLine(pref+"[");

				foreach (JsonElement e in elem.getArrayData())
					list.Add(JsonToVars(e, debug, pref+"    "));

				if (debug) Console.WriteLine(pref+"]");

				return list;
			}
			else if (elem.type == JsonElementType.OBJECT)
			{
				Dictionary<string, object> obj = new Dictionary<string, object> ();

				if (debug) Console.WriteLine(pref+"{");

				foreach (string keyName in elem.getKeys())
				{
					if (debug) Console.Write(pref+"    "+keyName+":");
					obj[keyName] = JsonToVars(elem[keyName], debug, pref+"    ");
				}

				if (debug) Console.WriteLine(pref+"}");

				return obj;
			}

			if (debug) Console.WriteLine(pref + elem.ToValue());

			return elem.ToValue();
		}

		// *****************************************************
		private static void Pop3LoadArray (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			int maxRecords = GetInt(FmtAttr(node, "Count", "100"));
			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "0"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "FALSE"));
			bool getAttachments = false;
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");

			if (String.IsNullOrEmpty(host) || port == 0 || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
			{
				LOG.write ("Warning: Pop3LoadArray: Configuration parameter missing.");
				return;
			}

			Pop3Client client = new Pop3Client ();

			try
			{
				client.Connect(host, port, use_ssl);
				client.Authenticate(username, password);

				List<string> allMsgIds = client.GetMessageUids();
				List<Dictionary<string, object>> result = new List<Dictionary<string, object>> ();

				for (int i = 1; i <= allMsgIds.Count; i++)
				{
					if (maxRecords-- <= 0) break;

					string msgid = allMsgIds[i-1];
					OpenPop.Mime.Message msg = client.GetMessage(i);

					Dictionary<string, object> o = new Dictionary<string, object> ();
					result.Add(o);

					string to_addr = "";

					for (int j = 0; j < msg.Headers.To.Count; j++)
						to_addr += ";" + msg.Headers.To[j].Address;

					to_addr = to_addr.Substring(1);

					o.Add("MSG_ID", msgid);
					o.Add("MSG_DATE", msg.Headers.DateSent.ToString("yyyy-MM-dd HH:mm:ss"));
					o.Add("MSG_FROM_NAME", msg.Headers.From.DisplayName);
					o.Add("MSG_FROM_EMAIL", msg.Headers.From.Address);
					o.Add("MSG_TO_EMAILS", to_addr);
					o.Add("MSG_SUBJECT", msg.Headers.Subject);

					List<OpenPop.Mime.MessagePart> content = msg.FindAllTextVersions();
					if (content.Count > 0)
					{
						string tmp = content[0].GetBodyAsText();
						o.Add("MSG_BODY", tmp);
						o.Add("MSG_BODY_LEN", tmp.Length.ToString());
					}
					else
					{
						o.Add("MSG_BODY", "");
						o.Add("MSG_BODY_LEN", "0");
					}

					if (getAttachments)
					{
						foreach (OpenPop.Mime.MessagePart part in msg.FindAllAttachments())
						{
							if (!part.IsAttachment)
								continue;

							string filepath = msg.Headers.DateSent.ToString("yyyyMMdd_HHmmss_") + part.FileName;
							//File.WriteAllBytes(filepath, part.Body);
						}
					}
				}

				CONTEXT[FmtAttr(node, "Into", "Array")] = result;
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("Pop3LoadArray: " + e.Message);

				LOG.write ("Error: Pop3LoadArray: " + e.Message);
			}
			finally
			{
				if (client != null && client.Connected)
					client.Disconnect();
			}
		}

		// *****************************************************
		private static void ImapLoadArray (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			int maxRecords = GetInt(FmtAttr(node, "Count", "100"));
			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "0"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "FALSE"));
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");
			bool markAsSeen = GetBool(FmtAttr(node, "MarkAsSeen", "FALSE"));
			bool getAttachments = GetBool(FmtAttr(node, "FetchAttachments", "FALSE"));
			bool saveEml = GetBool(FmtAttr(node, "SaveEML", "FALSE"));

			string query = FmtInnerText(node, "");
			if (String.IsNullOrEmpty(query))
			{
				LOG.write ("Warning: ImapLoadArray: Empty query specified.");
				return;
			}

			bool credential_issue = String.IsNullOrEmpty(host) || port == 0 || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password);
			if (g_imap_client == null && credential_issue)
			{
				LOG.write ("Warning: ImapLoadArray: Configuration parameter missing.");
				return;
			}

			ImapClient client = credential_issue ? g_imap_client : new ImapClient ();

			try
			{
				if (!credential_issue)
				{
					if (use_ssl)
						client.Connect(host, port, System.Security.Authentication.SslProtocols.Tls12, false);
					else
						client.Connect(host, port, false, false);

					client.Login(username, password);
				}
Console.WriteLine("3353");
if (client == null) Console.WriteLine("NO CLIENT");
if (client.Folders == null) Console.WriteLine("NO FOLDERS");
if (client.Folders.Inbox == null) Console.WriteLine("NO INBOX");
Console.WriteLine("QUERY="+query);
Console.WriteLine("MAX_RECORDS="+maxRecords);
				long[] allMsgIds = client.Folders.Inbox.SearchMessageIds(query, maxRecords);
Console.WriteLine("3355");
				List<Dictionary<string, object>> result = new List<Dictionary<string, object>> ();
Console.WriteLine("3356");
				string uidValidity = client.Folders.Inbox.UidValidity;
				int _maxRecords = maxRecords;
Console.WriteLine("3359");
				for (int mindx = allMsgIds.Length-1; mindx >= 0; mindx--)
		        {
					if (maxRecords != -1) {
						if (_maxRecords-- <= 0) break;
					}

					long msgid = allMsgIds[mindx];

					try
					{
						ImapX.Message m = new ImapX.Message (msgid, client, client.Folders.Inbox);

						if (m.Download(getAttachments ? MessageFetchMode.Full : MessageFetchMode.Basic, false))
						{
							Dictionary<string, object> o = new Dictionary<string, object> ();
							result.Add(o);

							if (saveEml)
								o.Add("MSG_EML_DATA", m.DownloadRawMessage());

							string to_addr = "";

							for (int j = 0; j < m.To.Count; j++)
								to_addr += ";" + m.To[j].Address;

							to_addr = to_addr.Length != 0 ? to_addr.Substring(1) : "";

							string str_attachments = "";

							o.Add("MSG_ID", msgid.ToString());
							o.Add("MSG_UID_VALIDITY", uidValidity);
							o.Add("MSG_DATE", m.Date != null ? m.Date.Value.ToString("yyyy-MM-dd HH:mm:ss") : "");

							List<Dictionary<string, object>> att = new List<Dictionary<string, object>> ();
							o.Add("MSG_ATTACHMENTS", att);

							if (getAttachments && m.Date != null)
							{
								foreach (ImapX.Attachment part in m.Attachments)
								{
									if (!part.Downloaded) part.Download();

									Dictionary<string, object> tmp2 = new Dictionary<string, object> ();
									tmp2.Add("ATTACHMENT_NAME", part.FileName);
									tmp2.Add("ATTACHMENT_SIZE", part.FileSize);
									tmp2.Add("ATTACHMENT_DATA", part.FileData);

									att.Add(tmp2);
								}
							}

							foreach (ImapX.Attachment part in m.Attachments)
							{
								str_attachments += part.FileName + ";";
							}

							o.Add("MSG_FROM_NAME", m.From.DisplayName);
							o.Add("MSG_FROM_EMAIL", m.From.Address);
							o.Add("MSG_TO_EMAILS", to_addr);
							o.Add("MSG_SUBJECT", m.Subject);
							o.Add("MSG_ATTACHMENT_NAMES", str_attachments);

							string tmp = "";

							string plain = m.Body.Text;
							string html = m.Body.Html;

							if (string.IsNullOrEmpty(plain)) plain = "";
							if (string.IsNullOrEmpty(html)) html = "";

							if (plain == "" && html != "")
							{
								HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
								doc.LoadHtml(html);

								try {
									foreach (var i in doc.DocumentNode.SelectNodes("//style"))
										i.Remove();
								}
								catch (Exception e) {
								}

								try {
									plain = doc.DocumentNode.InnerText;
								}
								catch (Exception e) {
								}
							}

							try { plain = HtmlAgilityPack.HtmlEntity.DeEntitize(plain); } catch (Exception e) { }
							try { html = HtmlAgilityPack.HtmlEntity.DeEntitize(html); } catch (Exception e) { }

							o.Add("MSG_BODY", plain);
							o.Add("MSG_BODY_LEN", plain.Length.ToString());
							o.Add("MSG_BODY_HTML", html);
							o.Add("MSG_BODY_HTML_LEN", html.Length.ToString());

							/*if (m.Body.HasText)
							{
								tmp = m.Body.Text;
							}
							else if (m.Body.HasHtml)
							{
								HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument ();
								doc.LoadHtml(m.Body.Html);

								try {
									foreach (var i in doc.DocumentNode.SelectNodes("//style"))
										i.Remove();
								}
								catch (Exception e) {
								}

								try {
									tmp = doc.DocumentNode.InnerText;
								}
								catch (Exception e) {
									tmp = m.Body.Text;
								}

							}

							try {
								tmp = HtmlAgilityPack.HtmlEntity.DeEntitize(tmp);
							}
							catch (Exception e) {
							}*/

							if (markAsSeen)
								m.Seen = true;
						}
					}
					catch (Exception e)
					{
						LOG.write("Error: ImapLoadArray: " + e.Message);
						LOG.write("StackTrace: " + e.StackTrace);
					}
				}

				CONTEXT[FmtAttr(node, "Into", "Array")] = result;
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("ImapLoadArray: " + e.Message);

				LOG.write ("Error: ImapLoadArray: " + e.Message);
			}
			finally
			{
				if (!credential_issue && client != null && client.IsConnected)
					client.Disconnect();
			}
		}

		// *****************************************************
		private static void ImapOpen (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "0"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "FALSE"));
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");
			bool oauth = GetBool(FmtAttr(node, "OAuth2", "FALSE"));

			bool credential_issue = String.IsNullOrEmpty(host) || port == 0 || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password);

			if (credential_issue)
			{
				LOG.write ("Warning: ImapOpen: Configuration parameter missing.");
				return;
			}

			if (g_imap_client != null)
				g_imap_client.Disconnect();

			g_imap_client = new ImapClient ();
			g_imap_client.IsDebug = true;

			//var writer = new System.Diagnostics.TextWriterTraceListener(System.Console.Out);
			//System.Diagnostics.Debug.Listeners.Add(writer);

			try
			{
				if (use_ssl)
					g_imap_client.Connect(host, port, System.Security.Authentication.SslProtocols.Tls12, false);
				else
					g_imap_client.Connect(host, port, false, false);

				bool res;
				if (oauth) {
					res = g_imap_client.Login(new OAuth2Creds(username, password));
				}
				else
					res = g_imap_client.Login(username, password);

				if (!res) throw new Exception ("Unable to Login");
			}
			catch (Exception e)
			{
				g_imap_client = null;

				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("ImapOpen: " + e.Message);
				Console.WriteLine(e.StackTrace);
				LOG.write ("Error: ImapOpen: " + e.Message);
			}
		}

		// *****************************************************
		private static void ImapClose (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try
			{
				if (g_imap_client != null)
					g_imap_client.Disconnect();
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("ImapClose: " + e.Message);

				LOG.write ("Error: ImapClose: " + e.Message);
			}
			finally
			{
				g_imap_client = null;
			}
		}

		// *****************************************************
		private static void ImapSetSeen (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "0"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "FALSE"));
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");

			bool credential_issue = String.IsNullOrEmpty(host) || port == 0 || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password);

			if (g_imap_client == null && credential_issue)
			{
				LOG.write ("Warning: ImapSetSeen: Configuration parameter missing.");
				return;
			}

			ImapClient client = credential_issue ? g_imap_client : new ImapClient ();

			try
			{
				if (!credential_issue)
				{
					if (use_ssl)
						client.Connect(host, port, System.Security.Authentication.SslProtocols.Tls12, false);
					else
						client.Connect(host, port, false, false);

					client.Login(username, password);
				}

				long msgid = long.Parse(FmtAttr(node, "Id", "0"));

				ImapX.Message m = new ImapX.Message (msgid, client, client.Folders.Inbox);

				m.Seen = GetBool(FmtAttr(node, "Value", "TRUE"));
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("ImapSetSeen: " + e.Message);

				LOG.write ("Error: ImapSetSeen: " + e.Message);
			}
			finally
			{
				if (!credential_issue && client != null && client.IsConnected)
					client.Disconnect();
			}
		}

		// *****************************************************
		private static void ImapLoadMessage (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "0"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "FALSE"));
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");
			bool getAttachments = GetBool(FmtAttr(node, "FetchAttachments", "FALSE"));
			bool saveEml = GetBool(FmtAttr(node, "SaveEML", "FALSE"));

			bool credential_issue = String.IsNullOrEmpty(host) || port == 0 || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password);

			if (g_imap_client == null && credential_issue)
			{
				LOG.write ("Warning: ImapLoadMessage: Configuration parameter missing.");
				return;
			}

			ImapClient client = credential_issue ? g_imap_client : new ImapClient ();

			try
			{
				if (!credential_issue)
				{
					if (use_ssl)
						client.Connect(host, port, System.Security.Authentication.SslProtocols.Tls12, false);
					else
						client.Connect(host, port, false, false);

					client.Login(username, password);
				}

				long msgid = long.Parse(FmtAttr(node, "Id", "0"));

				long[] allMsgIds = client.Folders.Inbox.SearchMessageIds("UID "+msgid, 1000);
				if (allMsgIds.Length == 0) throw new Exception ("Unable to find message with UID="+msgid);

				ImapX.Message m = new ImapX.Message (msgid, client, client.Folders.Inbox);

				if (m.Download(getAttachments ? MessageFetchMode.Full : MessageFetchMode.Basic, true))
				{
					Dictionary<string, object> o = new Dictionary<string, object> ();

					if (saveEml)
						o.Add("MSG_EML_DATA", m.DownloadRawMessage());

					string to_addr = "";

					for (int j = 0; j < m.To.Count; j++)
						to_addr += ";" + m.To[j].Address;

					to_addr = to_addr.Length != 0 ? to_addr.Substring(1) : "";

					string str_attachments = "";

					o.Add("MSG_ID", msgid.ToString());
					o.Add("MSG_DATE", m.Date != null ? m.Date.Value.ToString("yyyy-MM-dd HH:mm:ss") : "");

					List<Dictionary<string, object>> att = new List<Dictionary<string, object>> ();
					o.Add("MSG_ATTACHMENTS", att);

					if (getAttachments && m.Date != null)
					{
						foreach (ImapX.Attachment part in m.Attachments)
						{
							if (!part.Downloaded) part.Download();

							Dictionary<string, object> tmp2 = new Dictionary<string, object> ();
							tmp2.Add("ATTACHMENT_NAME", part.FileName);
							tmp2.Add("ATTACHMENT_SIZE", part.FileSize);
							tmp2.Add("ATTACHMENT_DATA", part.FileData);

							att.Add(tmp2);
						}
					}

					foreach (ImapX.Attachment part in m.Attachments)
					{
						str_attachments += part.FileName + ";";
					}

					o.Add("MSG_FROM_NAME", m.From.DisplayName);
					o.Add("MSG_FROM_EMAIL", m.From.Address);
					o.Add("MSG_TO_EMAILS", to_addr);
					o.Add("MSG_SUBJECT", m.Subject);
					o.Add("MSG_ATTACHMENT_NAMES", str_attachments);

					string tmp = "";

					if (m.Body.HasText)
					{
						tmp = m.Body.Text;
					}
					else if (m.Body.HasHtml)
					{
						HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument ();
						doc.LoadHtml(m.Body.Html);

						try {
							foreach (var i in doc.DocumentNode.SelectNodes("//style"))
								i.Remove();
						}
						catch (Exception e) {
						}

						try {
							tmp = doc.DocumentNode.InnerText;
						}
						catch (Exception e) {
							tmp = m.Body.Text;
						}

					}

					try {
						tmp = HtmlAgilityPack.HtmlEntity.DeEntitize(tmp);
					}
					catch (Exception e) {
					}

					o.Add("MSG_BODY", tmp);
					o.Add("MSG_BODY_LEN", tmp.Length.ToString());

					foreach (string name in o.Keys)
						CONTEXT[name] = o[name];
				}
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("ImapLoadMessage: " + e.Message);

				LOG.write ("Error: ImapLoadMessage: " + e.Message);
			}
			finally
			{
				if (!credential_issue && client != null && client.IsConnected)
					client.Disconnect();
			}
		}

		// *****************************************************
		private static void _GetAttachments (MimeKit.MimeMessage msg, List<Dictionary<string, object>> att, ref string str_attachments, ref string str_body, ref string html_body)
		{
			foreach (MimeKit.MimeEntity entity in msg.Attachments)
			{
				MimeKit.MimePart part;

				try {
					MimeKit.MessagePart submsg = (MimeKit.MessagePart)entity;
					_GetAttachments(submsg.Message, att, ref str_attachments, ref str_body, ref html_body);
					continue;
				}
				catch (Exception e) {
				}

				try {
					part = (MimeKit.MimePart)entity;
				}
				catch (Exception e) {
					throw e;
				}

				Dictionary<string, object> tmp2 = new Dictionary<string, object> ();
				byte[] buff;

				using (Stream s = part.Content.Open()) {
					using(MemoryStream ms2 = new MemoryStream()) {
						s.CopyTo(ms2);
						buff = ms2.ToArray();
					}
				}

				str_attachments += part.FileName + ";";

				tmp2.Add("ATTACHMENT_NAME", part.FileName);
				tmp2.Add("ATTACHMENT_SIZE", buff.Length);
				tmp2.Add("ATTACHMENT_DATA", buff);

				att.Add(tmp2);
			}

			string tmp = null;
			string tmp3 = null;
			string tmp4 = null;

			if (msg.TextBody != null) {
				tmp = msg.TextBody;
			}

			if (msg.HtmlBody != null)
			{
				HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
				doc.LoadHtml(msg.HtmlBody);

				try {
					foreach (var i in doc.DocumentNode.SelectNodes("//style"))
						i.Remove();
				}
				catch (Exception e) {
				}

				try {
					tmp3 = doc.DocumentNode.InnerText;
					tmp4 = doc.DocumentNode.InnerHtml;
				}
				catch (Exception e) {
					tmp3 = null;
					tmp4 = null;
				}
			}

			try {
				if (tmp != null)
					tmp = HtmlAgilityPack.HtmlEntity.DeEntitize(tmp);
			}
			catch (Exception e) { }

			try {
				if (tmp3 != null)
					tmp3 = HtmlAgilityPack.HtmlEntity.DeEntitize(tmp3);
			}
			catch (Exception e) { }

			try {
				if (tmp4 != null)
					tmp4 = HtmlAgilityPack.HtmlEntity.DeEntitize(tmp4);
			}
			catch (Exception e) { }

			if (tmp3 == null)
				tmp3 = "";

			if (tmp4 == null)
				tmp4 = "";

			if (tmp == null)
				tmp = tmp3;

			str_body += tmp + "\n~~~\n";
			html_body += tmp4  + "\n~~~\n";
		}

		private static void LoadEmlMessage (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string path = FmtAttr(node, "Path", "");
			byte[] buffer = GetByteArray(FmtInnerTextObj(node, ""));

			if (path != "" && File.Exists(path))
				buffer = File.ReadAllBytes(path);

			try
			{
				MemoryStream ms = new MemoryStream (buffer);
				MimeKit.MimeMessage m = MimeKit.MimeMessage.Load(ms);

				Dictionary<string, object> o = new Dictionary<string, object> ();

				o.Add("MSG_DATE", m.Date != null ? m.Date.ToString("yyyy-MM-dd HH:mm:ss") : "");

				List<Dictionary<string, object>> att = new List<Dictionary<string, object>> ();
				o.Add("MSG_ATTACHMENTS", att);

				string to_addr = "";
				string cc_addr = "";
				string bcc_addr = "";

				string str_attachments = "";
				string str_body = "";

				string html_body = "";

				_GetAttachments (m, att, ref str_attachments, ref str_body, ref html_body);

				for (int j = 0; j < m.To.Count; j++)
				{
					if (m.To[j] is GroupAddress) {
						foreach (var k in ((GroupAddress)m.To[j]).Members)
							to_addr += ";" + ((MailboxAddress)k).Address;
					}
					else {
						to_addr += ";" + ((MailboxAddress)m.To[j]).Address;
					}
				}

				for (int j = 0; j < m.Cc.Count; j++)
				{
					if (m.Cc[j] is GroupAddress) {
						foreach (var k in ((GroupAddress)m.Cc[j]).Members)
							cc_addr += ";" + ((MailboxAddress)k).Address;
					}
					else {
						cc_addr += ";" + ((MailboxAddress)m.Cc[j]).Address;
					}
				}

				for (int j = 0; j < m.Bcc.Count; j++)
				{
					if (m.Bcc[j] is GroupAddress) {
						foreach (var k in ((GroupAddress)m.Bcc[j]).Members)
							bcc_addr += ";" + ((MailboxAddress)k).Address;
					}
					else {
						bcc_addr += ";" + ((MailboxAddress)m.Bcc[j]).Address;
					}
				}

				to_addr = to_addr.Length != 0 ? to_addr.Substring(1) : "";
				cc_addr = cc_addr.Length != 0 ? cc_addr.Substring(1) : "";
				bcc_addr = bcc_addr.Length != 0 ? bcc_addr.Substring(1) : "";

				o.Add("MSG_FROM_NAME", ((MailboxAddress)m.From[0]).Name);
				o.Add("MSG_FROM_EMAIL", ((MailboxAddress)m.From[0]).Address);
				o.Add("MSG_TO_EMAILS", to_addr);
				o.Add("MSG_CC_EMAILS", cc_addr);
				o.Add("MSG_BCC_EMAILS", bcc_addr);
				o.Add("MSG_SUBJECT", m.Subject);
				o.Add("MSG_ATTACHMENT_NAMES", str_attachments);

				o.Add("MSG_BODY", str_body);
				o.Add("MSG_BODY_LEN", str_body.Length.ToString());

				o.Add("MSG_BODY_HTML", html_body);
				o.Add("MSG_BODY_HTML_LEN", html_body.Length.ToString());
				

				foreach (string name in o.Keys)
					CONTEXT[name] = o[name];
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("LoadEmlMessage: " + e.Message);

				LOG.write ("Error: LoadEmlMessage: " + e.Message);
			}
		}

		private static void MsgLoadInfo (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "FALSE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("MsgLoadInfo(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("MsgLoadInfo("+path+"): File not found.");

			string prefix = FmtAttr(node, "Prefix", "");
			if (prefix.Length > 0) prefix += ".";

			try
			{
				OutlookStorage.Message msg = new OutlookStorage.Message (path);

				CONTEXT[prefix + "MSG_SUBJECT"] = msg.Subject;
				CONTEXT[prefix + "MSG_BODY_TEXT"] = msg.BodyText;

				string rcpt_to = "";
				string rcpt_cc = "";
				string rcpt_bcc = "";
				string rcpt_all = "";

			    foreach (OutlookStorage.Recipient recip in msg.Recipients)
			    {
			    	rcpt_all += "|" + recip.Email;

			    	if (recip.Type == OutlookStorage.RecipientType.To)
						rcpt_to += "|" + recip.Email;
			    	else if (recip.Type == OutlookStorage.RecipientType.CC)
						rcpt_cc += "|" + recip.Email;
			    	else
						rcpt_bcc += "|" + recip.Email;
			    }

			    CONTEXT[prefix + "MSG_RCPT_TO"] = rcpt_to.Length != 0 ? rcpt_to.Substring(1) : "";
			    CONTEXT[prefix + "MSG_RCPT_CC"] = rcpt_cc.Length != 0 ? rcpt_cc.Substring(1) : "";
			    CONTEXT[prefix + "MSG_RCPT_BCC"] = rcpt_bcc.Length != 0 ? rcpt_bcc.Substring(1) : "";
			    CONTEXT[prefix + "MSG_RCPT"] = rcpt_all.Length != 0 ? rcpt_all.Substring(1) : "";

			    List<Dictionary<string, object>> list = new List<Dictionary<string, object>> ();
			    CONTEXT[prefix + "MSG_ATTACHMENTS"] = list;

			    foreach (OutlookStorage.Attachment attach in msg.Attachments)
			    {
			    	Dictionary<string,object> att = new Dictionary<string, object> ();
			    	att.Add("MSG_ATTACHMENT_FILENAME", attach.Filename);
			    	att.Add("MSG_ATTACHMENT_DATA", attach.Data);
			    	list.Add(att);
			    }

			    if (msg.Messages.Count != 0)
			    	Log.write ("Warning: MSG file " + path + " has unsupported sub-messages.");
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("MsgLoadInfo("+path+"): " + e.Message);

				CONTEXT[prefix + "MSG_SUBJECT"] = "";
				CONTEXT[prefix + "MSG_BODY_TEXT"] = "";
			    CONTEXT[prefix + "MSG_RCPT_TO"] = "";
			    CONTEXT[prefix + "MSG_RCPT_CC"] = "";
			    CONTEXT[prefix + "MSG_RCPT_BCC"] = "";
			    CONTEXT[prefix + "MSG_RCPT"] = "";
			    CONTEXT[prefix + "MSG_ATTACHMENTS"] = new List<Dictionary<string,object>> ();
			}
		}

		private static void PdfLoadInfo (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "FALSE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("PdfLoadInfo(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("PdfLoadInfo("+path+"): File not found.");

			string prefix = FmtAttr(node, "Prefix", "");
			if (prefix.Length > 0) prefix += ".";

			try
			{
				PdfReader pdfReader = new PdfReader(path);
				pdfReader.SetUnethicalReading(true);

				PdfDocument pdfDoc = new PdfDocument(pdfReader);

				int minWidth = int.MaxValue;
				int minHeight = int.MaxValue;
				int maxWidth = int.MinValue;
				int maxHeight = int.MinValue;

				for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
				{
					PdfPage pdfPage = pdfDoc.GetPage(page);
					Rectangle size = pdfPage.GetPageSizeWithRotation();
					Rectangle size2 = pdfPage.GetPageSize();

					if (size.GetWidth() < minWidth) minWidth = (int)size.GetWidth();
					if (size.GetHeight() < minHeight) minHeight = (int)size.GetHeight();
					if (size.GetWidth() > maxWidth) maxWidth = (int)size.GetWidth();
					if (size.GetHeight() > maxHeight) maxHeight = (int)size.GetHeight();
				}

				CONTEXT[prefix + "PdfNumPages"] = pdfDoc.GetNumberOfPages();
				CONTEXT[prefix + "PdfMinWidth"] = minWidth / 72.0;
				CONTEXT[prefix + "PdfMinHeight"] = minHeight / 72.0;
				CONTEXT[prefix + "PdfMaxWidth"] = maxWidth / 72.0;
				CONTEXT[prefix + "PdfMaxHeight"] = maxHeight / 72.0;


				pdfDoc.Close();
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfLoadInfo("+path+"): " + e.Message);

				CONTEXT[prefix + "PdfNumPages"] = 0;
				CONTEXT[prefix + "PdfMinWidth"] = 0;
				CONTEXT[prefix + "PdfMinHeight"] = 0;
				CONTEXT[prefix + "PdfMaxWidth"] = 0;
				CONTEXT[prefix + "PdfMaxHeight"] = 0;
			}
		}

		// *****************************************************
		private static void PdfLoadTextArray (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "FALSE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("PdfLoadText(): Empty path specified.");

				return;
			}

			if (!File.Exists(path))
			{
				if (strict)
					throw new Exception ("PdfLoadText("+path+"): File not found.");

				CONTEXT[FmtAttr(node, "Into", "Array")] = new List<Dictionary<string, object>> ();
				return;
			}

			try
			{
				List<Dictionary<string, object>> result = new List<Dictionary<string, object>> ();

				PdfReader pdfReader = new PdfReader(path);
				pdfReader.SetUnethicalReading(true);

				PdfDocument pdfDoc = new PdfDocument(pdfReader);

				for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
				{
					var textEventListener = new LocationTextExtractionStrategy();
					string text = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(page), textEventListener);

		            var o = new Dictionary<string, object> ();
		            result.Add(o);

		            o.Add("PageNumber", page.ToString());
		            o.Add("PageText", text);
				}

				pdfReader.Close();
				CONTEXT[FmtAttr(node, "Into", "Array")] = result;
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfLoadInfo("+path+"): " + e.Message);

				CONTEXT[FmtAttr(node, "Into", "Array")] = new List<Dictionary<string, object>> ();
			}
		}

		// *****************************************************
		private static void PdfLoadText (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("PdfLoadText(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("PdfLoadText("+path+"): File not found.");

			try
			{
				CONTEXT["FileText"] = PdfUtils.GetText(path);
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfLoadText("+path+"): " + e.Message);

				CONTEXT["FileText"] = "";
			}
		}

		// *****************************************************
		private static void PdfMerge (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string outputFile = FmtAttr(node, "Output", "");
			if (outputFile.Length == 0)
			{
				if (strict) throw new Exception ("PdfMerge(): No output file specified.");
				return;
			}

			string input = FmtAttr(node, "Input", "");
			if (input.Length == 0)
			{
				if (strict) throw new Exception ("PdfMerge(): No input file specified.");
				return;
			}

			string[] inputFiles = input.Split('|');

			for (int i = 0; i < inputFiles.Length; i++)
			{
				inputFiles[i] = inputFiles[i].Trim();

				if (!File.Exists(inputFiles[i]))
				{
					if (strict) throw new Exception ("PdfMerge("+inputFiles[i]+"): File not found.");
					return;
				}
			}

			try
			{
				if (File.Exists(outputFile)) File.Delete(outputFile);

				PdfUtils.MergePDFs (outputFile, inputFiles);
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfMerge("+outputFile+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfSlice (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string outputFile = FmtAttr(node, "Output", "");
			if (outputFile.Length == 0)
			{
				if (strict) throw new Exception ("PdfSlice(): No output file specified.");
				return;
			}

			string inputFile = FmtAttr(node, "Input", "");
			if (inputFile.Length == 0)
			{
				if (strict) throw new Exception ("PdfSlice(): No input file specified.");
				return;
			}

			if (!File.Exists(inputFile))
			{
				if (strict) throw new Exception ("PdfSlice("+inputFile+"): File not found.");
				return;
			}

			string range = FmtAttr(node, "Range", "");
			if (range.Length == 0)
			{
				throw new Exception ("PdfSlice(): Range is required.");
				return;
			}

			try
			{
				FileInfo fi = new FileInfo (outputFile);

				if (!File.Exists(fi.DirectoryName))
					Directory.CreateDirectory(fi.DirectoryName);

				string[] _range = range.Split(',');

				int[] pageStart = new int[_range.Length];
				int[] pageEnd = new int[_range.Length];

				for (int i = 0; i < _range.Length; i++)
				{
					string[] tmp = _range[i].Split('-');
					int a = 0, b = 0;

					if (int.TryParse(tmp[0].Trim(), out a))
					{
						if (!int.TryParse(tmp[1].Trim(), out b))
							b = -1;
					}
					else
						a = -1;

					pageStart[i] = a;
					pageEnd[i] = b;
				}

				if (File.Exists(outputFile))
					File.Delete(outputFile);

				PdfUtils.SlicePDF (outputFile, inputFile, pageStart, pageEnd);
			}
			catch (Exception e)
			{
				LOG.write("StackTrace: " + e.StackTrace);
				if (strict) throw new Exception ("PdfSlice("+outputFile+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfStamp (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string outputFile = FmtAttr(node, "Output", "");
			if (outputFile.Length == 0)
			{
				if (strict) throw new Exception ("PdfStamp(): No output file specified.");
				return;
			}

			string inputFile = FmtAttr(node, "Input", "");
			if (inputFile.Length == 0)
			{
				if (strict) throw new Exception ("PdfStamp(): No input file specified.");
				return;
			}

			int pageNum = GetInt(FmtAttr(node, "PageNum", "1"));
			int margin = GetInt(FmtAttr(node, "Margin", "16"));
			int padding = GetInt(FmtAttr(node, "Padding", "4"));
			float fontSize = (float)GetDouble(FmtAttr(node, "FontSize", "14"));

			string X = FmtAttr(node, "X", "Right");
			string Y = FmtAttr(node, "Y", "Top");

			float dx = (float)GetDouble(FmtAttr(node, "OffsX", "0"));
			float dy = (float)GetDouble(FmtAttr(node, "OffsY", "0"));

			try
			{
				if (File.Exists(outputFile))
					File.Delete(outputFile);

				PdfUtils.StampPDF(inputFile, outputFile, pageNum, margin, padding, X, Y, dx, dy, fontSize, FmtInnerText(node, ""));
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfMerge("+outputFile+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfOpen (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string outputFile = FmtAttr(node, "Output", "");
			if (outputFile == "") outputFile = null;

			string inputFile = FmtAttr(node, "Input", "");
			if (inputFile.Length == 0)
			{
				throw new Exception ("PdfOpen(): No input file specified.");
				return;
			}

			try
			{
				CONTEXT["PdfDocument"] = PdfUtils.OpenPDF(inputFile, outputFile);
			}
			catch (Exception e) {
				throw new Exception ("PdfOpen("+outputFile+"): " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfClose (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try {
				PdfUtils.ClosePDF((PdfDocument)CONTEXT["PdfDocument"]);
			}
			catch (Exception e) {
				throw new Exception ("PdfClose: " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfFind (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try {
				CONTEXT[ FmtAttr(node, "Into", "PdfMatches") ] = PdfUtils.FindMatches (
					(PdfDocument)CONTEXT["PdfDocument"],
					FmtInnerText(node, "").Trim(),
					GetInt(FmtAttr(node, "StartPage", "0")),
					GetInt(FmtAttr(node, "MaxPages", "0")),
					GetInt(FmtAttr(node, "MaxCount", "0")),
					GetBool(FmtAttr(node, "IgnoreCase", "True"))
					);
			}
			catch (Exception e) {
				throw new Exception ("PdfFind: " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfOverlay (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try
			{
				int pageNum = GetInt(FmtAttr(node, "Page", ContextGet("Page").ToString()));
				float x = (float)GetDouble(FmtAttr(node, "X", ContextGet("X").ToString()));
				float y = (float)GetDouble(FmtAttr(node, "Y", ContextGet("Y").ToString()));
				float width = (float)GetDouble(FmtAttr(node, "Width", ContextGet("Width").ToString()));
				float height = (float)GetDouble(FmtAttr(node, "Height", ContextGet("Height").ToString()));
				float fontSize = (float)GetDouble(FmtAttr(node, "FontSize", ContextGet("FontSize").ToString()));
				string bg = FmtAttr(node, "Background", "ffffff");
				string fg = FmtAttr(node, "Color", "000000");

				PdfUtils.Overlay(
					(PdfDocument)CONTEXT["PdfDocument"],
					pageNum, x, y, width, height, fontSize, bg, fg, FmtInnerText(node, "")
				);
			}
			catch (Exception e)
			{
				LOG.write("StackTrace: " + e.StackTrace);
				throw new Exception ("PdfOverlay: " + e.Message);
			}
		}

		// *****************************************************
		private static void PdfCleanup (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			int pageNum = GetInt(FmtAttr(node, "Page", ContextGet("Page").ToString()));
			float x = (float)GetDouble(FmtAttr(node, "X", ContextGet("X").ToString()));
			float y = (float)GetDouble(FmtAttr(node, "Y", ContextGet("Y").ToString()));
			float width = (float)GetDouble(FmtAttr(node, "Width", ContextGet("Width").ToString()));
			float height = (float)GetDouble(FmtAttr(node, "Height", ContextGet("Height").ToString()));
			string bg = FmtAttr(node, "Background", "ffffff");

			try
			{
				PdfUtils.Remove(
					(PdfDocument)CONTEXT["PdfDocument"],
					pageNum, x, y, width, height
				);
			}
			catch (Exception e)
			{
				throw new Exception ("PdfCleanup: " + e.Message);
			}
		}

		// *****************************************************
		private static void IFilterLoadText (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "FALSE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("IFilterLoadText(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("IFilterLoadText("+path+"): File not found.");

			try
			{
				TextReader reader = new EPocalipse.IFilter.FilterReader(path);
				using (reader)
				{
					CONTEXT["FileText"] = reader.ReadToEnd();
				}
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("PdfLoadInfo("+path+"): " + e.Message);

				CONTEXT["FileText"] = "";
			}
		}

		// *****************************************************
		private static void RegexExtract (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string pattern = FmtInnerText(node, "");
			if (String.IsNullOrEmpty(pattern))
			{
				LOG.write ("Warning: RegexExtract: Empty regular expression specified.");
				return;
			}

			try
			{
				int maxLen = GetInt(FmtAttr(node, "MaxLen", "8192"));

				if (GetBool(FmtAttr(node, "Single", "FALSE")))
				{
					var regex = GetBool(FmtAttr(node, "IgnoreCase", "TRUE")) ? new Regex(pattern, RegexOptions.Multiline | RegexOptions.ECMAScript | RegexOptions.IgnoreCase) : new Regex(pattern, RegexOptions.Multiline | RegexOptions.ECMAScript);

					string ss = FmtAttr(node, "Source", "");
					if (ss.Length > maxLen) ss = ss.Substring(0, maxLen);

					var matches = regex.Matches(ss);

					for (int i = 0; i < matches.Count; i++)
					{
						for (int j = 0; j < matches[i].Groups.Count; j++)
						{
							CONTEXT["REGEX_" + j] = matches[i].Groups[j].Value;
						}

						break;
					}

					CONTEXT["REGEX_N"] = matches.Count;
				}
				else
				{
					List<Dictionary<string, object>> result = new List<Dictionary<string, object>> ();
					CONTEXT[FmtAttr(node, "Into", "Array")] = result;

					var regex = GetBool(FmtAttr(node, "IgnoreCase", "TRUE")) ? new Regex(pattern, RegexOptions.Multiline | RegexOptions.ECMAScript | RegexOptions.IgnoreCase) : new Regex(pattern, RegexOptions.Multiline | RegexOptions.ECMAScript);
					string ss = FmtAttr(node, "Source", "");
					if (ss.Length > maxLen) ss = ss.Substring(0, maxLen);
					var matches = regex.Matches(ss);

					for (int i = 0; i < matches.Count; i++)
					{
						Dictionary<string,object> row = new Dictionary<string, object> ();

						for (int j = 0; j < matches[i].Groups.Count; j++)
						{
							row.Add("REGEX_" + j, matches[i].Groups[j].Value);
						}

						result.Add(row);
					}

					CONTEXT["REGEX_N"] = matches.Count;
				}
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("RegexExtract: " + e.Message);

				LOG.write ("Error: RegexExtract: " + e.Message);
			}
		}

		// *****************************************************
		private static void SplitText (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try
			{
				List<Dictionary<string, object>> result = new List<Dictionary<string, object>> ();
				CONTEXT[FmtAttr(node, "Into", "Array")] = result;

				string[] data = FmtInnerText(node, "").Split(new string[] { FmtAttr(node, "By", "\n", false) }, StringSplitOptions.None);

				for (int i = 0; i < data.Length; i++)
				{
					Dictionary<string,object> row = new Dictionary<string, object> ();

					data[i] = data[i].Trim();

					row.Add("PartNumber", i.ToString());
					row.Add("PartText", data[i]);
					row.Add("PartLength", data[i].Length);

					result.Add(row);
				}
			}
			catch (Exception e)
			{
				LOG.write ("Error: SplitText: " + e.Message);
			}
		}

		// *****************************************************
		private static void JoinItems (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			List<Dictionary<string, object>> list;

			try {
				//list = (List<Dictionary<string, object>>)CONTEXT[FmtAttr(node, "From", "Array")];
				list = (List<Dictionary<string, object>>)FmtAttrObj(node, "From", "Array");
				if (list == null) throw new Exception ("Input array "+FmtAttr(node, "From", "Array")+" is null.");
			}
			catch (Exception e) {
				throw new Exception ("JoinItems: Unable to get Array: " + e.Message);
			}

			string prefix = FmtAttr(node, "Prefix", "");
			if (prefix.Length > 0) prefix += ".";

			string delim = FmtAttr(node, "Delimiter", ",");
			string result = "";

			foreach (Dictionary<string, object> row in list)
			{
				if (row == null)
					continue;

				if (result.Length != 0)
					result += delim;

				foreach (string name in row.Keys)
					CONTEXT[prefix + name] = row[name];

				result += FmtInnerText (node, "");
			}

			CONTEXT[FmtAttr(node, "Into", "Text")] = result;
		}

		// *****************************************************
		private static void ReplaceText (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			try
			{
				CONTEXT[FmtAttr(node, "Into", "Text")] = FmtInnerText(node, "").Replace(FmtAttr(node, "Old", ""), FmtAttr(node, "New", ""));
			}
			catch (Exception e)
			{
				LOG.write ("Error: ReplaceText: " + e.Message);
			}
		}

		// *****************************************************
		private static void Switch (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string value = FmtAttr(node, "Value", "0");

			try
			{
				for (int i = 0; i < node.ChildNodes.Count; i++)
				{
					if (node.ChildNodes[i].NodeType	!= XmlNodeType.Element)
						continue;

					if (node.ChildNodes[i].Name == "Case" && node.ChildNodes[i].Attributes["Value"].Value == value)
					{
						ExecuteActions((XmlElement)node.ChildNodes[i]);
						break;
					}

					if (node.ChildNodes[i].Name == "Default")
					{
						ExecuteActions((XmlElement)node.ChildNodes[i]);
						break;
					}
				}
			}
			catch (FalseException e)
			{
				throw e;
			}
			catch (Exception e)
			{
				LOG.write ("Error: Switch: " + e.Message);
			}
		}

		// *****************************************************
		private static void SendMail (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			string host = FmtAttr(node, "Host", "");
			int port = GetInt(FmtAttr(node, "Port", "465"));
			bool use_ssl = GetBool(FmtAttr(node, "SSL", "TRUE"));
			bool use_implicit = GetBool((FmtAttr(node, "ImplicitSSL", "FALSE")));
			string username = FmtAttr(node, "Username", "");
			string password = FmtAttr(node, "Password", "");

			if (use_implicit) use_ssl = true;

			if (String.IsNullOrEmpty(host) || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
			{
				LOG.write ("Warning: SendMail: SMTP parameter missing.");
				return;
			}

			string fromAddr = FmtAttr(node, "From", "");
			string fromName = FmtAttr(node, "FromName", fromAddr);

			string subject = FmtAttr(node, "Subject", "");
			string toAddr = FmtAttr(node, "To", "");

			SmtpClient client = new SmtpClient();
			client.Host = host;
			client.Port = port;
			client.EnableSsl = use_ssl;
			client.DeliveryMethod = SmtpDeliveryMethod.Network;
			client.UseDefaultCredentials = true;
			client.Credentials = new System.Net.NetworkCredential(username, password);

			MailMessage mm2 = new MailMessage();

			if (fromAddr != "")
				mm2.From = new System.Net.Mail.MailAddress (fromAddr, fromName);

			if (!String.IsNullOrEmpty(toAddr))
			{
				foreach (string i in toAddr.Split('|'))
					mm2.To.Add(i.Trim());
			}

			mm2.IsBodyHtml = true;
			mm2.BodyEncoding = UTF8Encoding.UTF8;

			System.Web.Mail.MailMessage mm = new System.Web.Mail.MailMessage ();

			mm.From = fromName + " <" + fromAddr + ">";
			mm.To = toAddr;

			System.Web.Mail.SmtpMail.SmtpServer = host;

			MimeMessage mm3 = null;

			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/smtpserver", host);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/smtpserverport", port);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/sendusing", 2);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/smtpauthenticate", 1);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/smtpusessl", use_ssl);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/sendusername", username);
			mm.Fields.Add("http://schemas.microsoft.com/cdo/configuration/sendpassword", password);

			string stmp;
			glib.Email.RxMailMessage mmx = null;

			foreach (XmlNode tmp in node.ChildNodes)
			{
				if (tmp.NodeType != XmlNodeType.Element)
					continue;

				XmlElement elem = (XmlElement)tmp;
				glib.Email.MimeReader mimeReader;

				switch (elem.Name)
				{
					case "Stop":
						return;

					case "LoadEmlFile":
						mimeReader = new glib.Email.MimeReader ();
						mm2 = mmx = mimeReader.GetEmail(FmtInnerText(elem, ""));

						CONTEXT["EML_DATE"] = mm2.Headers["Date"];
						CONTEXT["EML_SUBJECT"] = mm2.Headers["Subject"];
						CONTEXT["EML_TO"] = mm2.Headers["To"];
						CONTEXT["EML_FROM"] = mm2.Headers["From"];

						using (var memoryStream = new MemoryStream())
						{
							var mimeMessage = MimeMessage.CreateFromMailMessage(mm2);
							mimeMessage.WriteTo(memoryStream);
							memoryStream.Position = 0;
							using (var tmp2 = new StreamReader(memoryStream))
							{
								string eml = tmp2.ReadToEnd();
								eml = eml.Replace("multipart/alternative", "multipart/mixed");
								mm3 = MimeMessage.Load(ParserOptions.Default, new MemoryStream(UTF8Encoding.UTF8.GetBytes(eml)));
							}
						}

//Console.WriteLine(">>>>>>> " + CONTEXT["EML_DATE"]);
//Console.WriteLine(">>>>>>> " + CONTEXT["EML_SUBJECT"]);
//Console.WriteLine(">>>>>>> " + CONTEXT["EML_TO"]);
//Console.WriteLine(">>>>>>> " + CONTEXT["EML_FROM"]);

						mm3.Headers.Clear();
						break;

					case "LoadEmlBuffer":
						mimeReader = new glib.Email.MimeReader ();

						using (MemoryStream ms = new MemoryStream (GetByteArray(FmtInnerTextObj(elem, ""))))
						{
							mm2 = mmx = mimeReader.GetEmail(ms);
							mm3 = MimeMessage.Load(ParserOptions.Default, ms);
						}

						CONTEXT["EML_DATE"] = mm3.Headers["Date"];
						CONTEXT["EML_SUBJECT"] = mm3.Headers["Subject"];
						CONTEXT["EML_TO"] = mm3.Headers["To"];
						CONTEXT["EML_FROM"] = mm3.Headers["From"];

						mm3.Headers.Clear();
						break;

					case "Attachment":
						foreach (string i in FmtInnerText(elem, "").Split('|'))
						{
							if (i.Trim().Length == 0) continue;
							mm2.Attachments.Add(new System.Net.Mail.Attachment(i.Trim()));
							mm.Attachments.Add(new System.Web.Mail.MailAttachment (new FileInfo(i.Trim()).FullName));
						}

						break;

					case "Subject":
						mm.Subject = mm2.Subject = FmtInnerText(elem, "");
						if (mm3 != null) mm3.Subject = mm.Subject;
						break;

					case "Message":
						mm.Body = mm2.Body = FmtInnerXml(elem, "");
						break;

					case "MessagePrepend":
						stmp = FmtInnerXml(elem, "");
						mm.Body = stmp + "\n" + mm.Body;
						mm2.Body = stmp + "\n" + mm2.Body;
						mm2.IsBodyHtml = true;
						break;

					case "MessageAppend":
						stmp = FmtInnerXml(elem, "");
						mm.Body = mm.Body + "\n" + stmp;
						mm2.Body = mm2.Body + "\n" + stmp;
						mm2.IsBodyHtml = true;
						break;

					case "Clear_To":
						mm.To = "";
						mm2.To.Clear();
						if (mm3 != null) mm3.To.Clear();
						break;

					case "To":
						mm.To = FmtInnerText(elem, "");

						foreach (string i in FmtInnerText(elem, "").Split('|'))
						{
							if (i.Trim().Length == 0) continue;
							mm2.To.Add(i.Trim());
							if (mm3 != null) mm3.To.Add(new MailboxAddress(i.Trim()));
						}

						break;

					case "Clear_ReplyTo":
						mm2.ReplyToList.Clear();
						if (mm3 != null) mm3.ReplyTo.Clear();
						break;

					case "ReplyTo":
						foreach (string i in FmtInnerText(elem, "").Split('|'))
						{
							if (i.Trim().Length == 0) continue;
							mm2.ReplyToList.Add(i.Trim());
							if (mm3 != null) mm3.ReplyTo.Add(new MailboxAddress(i.Trim()));
						}

						break;

					case "Clear_Cc":
						mm.Cc = "";
						mm2.CC.Clear();
						if (mm3 != null) mm3.Cc.Clear();
						break;

					case "Cc":
						mm.Cc = FmtInnerText(elem, "");

						foreach (string i in FmtInnerText(elem, "").Split('|'))
						{
							if (i.Trim().Length == 0) continue;
							mm2.CC.Add(i.Trim());
							if (mm3 != null) mm3.Cc.Add(new MailboxAddress(i.Trim()));
						}

						break;

					case "Clear_Bcc":
						mm.Bcc = "";
						mm2.Bcc.Clear();
						if (mm3 != null) mm3.Bcc.Clear();
						break;

					case "Bcc":
						mm.Bcc = FmtInnerText(elem, "");

						foreach (string i in FmtInnerText(elem, "").Split('|'))
						{
							if (i.Trim().Length == 0) continue;
							mm2.Bcc.Add(i.Trim());
							if (mm3 != null) mm3.Bcc.Add(new MailboxAddress(i.Trim()));
						}

						break;

					case "From":
						fromAddr = FmtInnerText(elem, "");
						break;

					case "FromName":
						fromName = FmtInnerText(elem, "");
						break;

					case "Dump":
						Console.WriteLine (mmx.MailStructure());
						break;
				}
			}

			if (fromAddr != "")
			{
				mm2.From = new System.Net.Mail.MailAddress (fromAddr, fromName);
				mm.From = fromName + " <" + fromAddr + ">";
				if (mm3 != null) {
					mm3.From.Clear();
					mm3.From.Add(new MailboxAddress(fromName, fromAddr));
				}
			}

			try
			{
				if (mm3 != null)
				{
					using (var cli = new MailKit.Net.Smtp.SmtpClient())
					{
						cli.Connect(host, port, use_ssl ? MailKit.Security.SecureSocketOptions.Auto : MailKit.Security.SecureSocketOptions.None);

						int mode = 0;

						foreach (String i in cli.AuthenticationMechanisms)
						{
							if (i.ToUpper() == "NTLM")
							{
								var ntlm = new MailKit.Security.SaslMechanismNtlm (username, password);
								cli.Authenticate (ntlm);

								mode = 1;
								break;
							}
						}

						if (mode == 0)
							cli.Authenticate(username, password);

						cli.Send(mm3);

						cli.Disconnect(true);
					}
				}
				else
				{
					if (use_implicit == false)
						client.Send(mm2);
					else
						System.Web.Mail.SmtpMail.Send(mm);
				}

				mm2.Attachments.Dispose();
				mm2.Dispose();

				mm.Attachments.Clear();
				client.Dispose();
			}
			catch (Exception e)
			{
				if (!GetBool(FmtAttr(node, "Silent", "FALSE")))
					throw new Exception ("SendMail: " + e.ToString() + ": " + e.Message);

				LOG.write ("Error: SendMail: " + e.ToString() + ": " + e.Message);
			}
		}

		// *****************************************************
		private static void Sleep (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			int amount = GetInt(FmtAttr(node, "Time", "100"));
			if (amount < 0) amount = 100;

			System.Threading.Thread.Sleep(amount);
		}

		// *****************************************************
		private static void ZipExtract (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("ZipExtract(): Empty path specified.");

				return;
			}

			if (strict && !File.Exists(path))
				throw new Exception ("ZipExtract("+path+"): File not found.");

			string target = FmtAttr(node, "Target", "");
			if (target.Length == 0)
			{
				if (strict)
					throw new Exception ("ZipExtract(): Target path not specified.");

				return;
			}

			if (Directory.Exists(target))
				Directory.Delete(target, true);

			try
			{
				System.IO.Compression.ZipFile.ExtractToDirectory(path, target);
				CONTEXT["ERRSTR"] = "";
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("ZipExtract("+path+"): " + e.Message);
				CONTEXT["ERRSTR"] = e.Message;
			}
		}

		// *****************************************************
		private static void ZipCompress (XmlElement node)
		{
			if (!NodeCheck(node)) return;

			bool strict = GetBool(FmtAttr(node, "Strict", "TRUE"));

			string path = FmtAttr(node, "Path", "");
			if (path.Length == 0)
			{
				if (strict)
					throw new Exception ("ZipCompress(): Empty path specified.");

				return;
			}

			string source = FmtAttr(node, "Source", "");
			if (source.Length == 0)
			{
				if (strict)
					throw new Exception ("ZipCompress(): Source path not specified.");

				return;
			}

			if (!Directory.Exists(source))
			{
				if (strict)
					throw new Exception ("ZipCompress(): Source path does not exist.");

				return;
			}

			try
			{
				System.IO.Compression.ZipFile.CreateFromDirectory(source, path);
				CONTEXT["ERRSTR"] = "";
			}
			catch (Exception e)
			{
				if (strict) throw new Exception ("ZipCompress("+path+"): " + e.Message);
				CONTEXT["ERRSTR"] = e.Message;
			}
		}

	}
};
