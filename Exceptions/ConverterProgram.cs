using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace Exceptions
{
	public class ConverterProgram
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public static void Main(params string[] args)
		{
			try
			{
				var filenames = args.Any() ? args : new[] { "text.txt" };
				Settings settings;
				settings = LoadSettings();
				ConvertFiles(filenames, settings);
			}
			catch (Exception e)
			{
				log.Error(e);
			}
		}

		private static void ConvertFiles(string[] filenames, Settings settings)
		{
			var tasks = filenames
				.Select(fn => Task.Run(() => ConvertFile(fn, settings)).ContinueWith(t => { HandleExceptions(t); })) 
				.ToArray();
			Task.WaitAll(tasks); 
		}

		private static void HandleExceptions(Task task)
		{
			if (task.Exception == null)
				return;
			foreach (var e in task.Exception.InnerExceptions)
				log.Error(e);
		}

		private static Settings LoadSettings() 
		{
			try
			{
				var serializer = new XmlSerializer(typeof(Settings));
				if (!File.Exists("settings.xml"))
				{
					log.Warn("Файл настроек settings.xml отсутствует");
					return Settings.Default;
				}
				var content = File.ReadAllText("settings.xml");
				return (Settings) serializer.Deserialize(new StringReader(content));
			}
			catch (Exception e)
			{
				throw new XmlException("Не удалось прочитать файл настроек");
			}
		}

		private static void ConvertFile(string filename, Settings settings)
		{
			if (!File.Exists(filename))
			{
				log.Error(new FileNotFoundException($"Не удалось сконвертировать {filename}"));
				return;
			}
			Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
			if (settings.Verbose)
			{
				log.Info("Processing file " + filename);
				log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
			}
			var lines = PrepareLines(filename); 
			try
			{
				var convertedLines = lines
					.Select(ConvertLine)
					.Select(s => s.Length + " " + s);
				File.WriteAllLines(filename + ".out", convertedLines);
			}
			catch
			{
				throw new FormatException("Некорректная строка");
			}
			
			
		}

		private static IEnumerable<string> PrepareLines(string filename)
		{
			var lineIndex = 0;
			foreach (var line in File.ReadLines(filename))
			{
				if (line == "") continue;
				yield return line.Trim();
				lineIndex++;
			}
			yield return lineIndex.ToString();
		}

	    public static string ConvertLine(string arg)
	    {
		    if (TryConvertAsDateTime(arg))
			    return ConvertAsDateTime(arg);
		    if (TryConvertAsDouble(arg))
			    return ConvertAsDouble(arg);
			return ConvertAsCharIndexInstruction(arg) ?? throw new FormatException("Некорректная строка");
	    }

		private static bool TryConvertAsCharIndexInstruction(string s)
		{
			var parts = s.Split();
			return int.TryParse(parts[0], out var ans);
		}

		private static string ConvertAsCharIndexInstruction(string s)
		{
			var parts = s.Split();
			if (parts.Length < 2) return null;
			var charIndex = int.Parse(parts[0]);
			if ((charIndex < 0) || (charIndex >= parts[1].Length))
				return null;
			var text = parts[1];
			return text[charIndex].ToString();
		}

		private static bool TryConvertAsDateTime(string arg)
		{
			return DateTime.TryParse(arg, out var ans);
		}

		private static string ConvertAsDateTime(string arg)
		{
			DateTime.TryParse(arg, out var ans);

			return ans.ToString(CultureInfo.InvariantCulture);
		}
		private static bool TryConvertAsDouble(string arg)
		{
			return double.TryParse(arg, out var ans);
		}

		private static string ConvertAsDouble(string arg)
		{
			double.TryParse(arg, out var ans);
			return ans.ToString(CultureInfo.InvariantCulture);
		}
	}
}