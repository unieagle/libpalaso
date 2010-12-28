using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Palaso.i18n
{
	public class StringCatalog
	{
		private Dictionary<string, string> _catalog;
		private static StringCatalog _singleton;
		private static Font _font;
		private static bool _inInternationalizationTestMode;

		/// <summary>
		/// Construct with no actual string file
		/// </summary>
		public StringCatalog(): this(String.Empty, 9)
		{
		}
		/// <summary>
		/// Construct with no actual string file
		/// </summary>
		public StringCatalog(string labelFontName, float labelFontSizeInPoints)
		{
			Init();
			SetupUIFont(labelFontName, labelFontSizeInPoints );
		}
		public StringCatalog(string pathToPoFile, string labelFontName, float labelFontSizeInPoints)
		{
			Init();
			_inInternationalizationTestMode = pathToPoFile == "test";
			if (!_inInternationalizationTestMode)
			{
				using (var reader = File.OpenText(pathToPoFile))
				{
					string id = null;
					string line = reader.ReadLine();
					while (line != null)
					{
						if (line.StartsWith("msgid"))
						{
							id = GetStringBetweenQuotes(line).Trim();
						}
						else if (line.StartsWith("msgstr") && !string.IsNullOrEmpty(id))
						{
							string s = GetStringBetweenQuotes(line);
							if (s.Length > 0)
							{
								_catalog.Add(id, s);
							}
							//id = null;
						}
							//handle multi-line messages
						else if (line.StartsWith("\"") && !string.IsNullOrEmpty(id))
						{
							string s = GetStringBetweenQuotes(line);
							if (s.Length > 0)
							{
								if (!_catalog.ContainsKey(id))
								{
									_catalog.Add(id, string.Empty);
								}
								_catalog[id] = _catalog[id] + s;
							}
						}
						else
						{
							id = null;
						}
						line = reader.ReadLine();
					}
				}
			}

			SetupUIFont(labelFontName,  labelFontSizeInPoints);
		}

		private void SetupUIFont(string labelFontName, float labelFontSizeInPoints)
		{
			if (_inInternationalizationTestMode)
			{
				LabelFont = new Font(FontFamily.GenericSansSerif, 9);
				return;
			}

			LabelFont = new Font(FontFamily.GenericSansSerif, (float) 8.25, FontStyle.Regular);
			if(!String.IsNullOrEmpty(labelFontName ))
			{
				try
				{
					LabelFont = new Font(labelFontName, labelFontSizeInPoints, FontStyle.Regular);
				}
				catch (Exception)
				{
					Palaso.Reporting.ErrorReport.NotifyUserOfProblem(
						"Could not find the requested UI font '{0}'.  Will use a generic font instead.",
						labelFontName);
				}
			}
		}

		public static string Get(string id)
		{
			return Get(id,  String.Empty);
		}

		/// <summary>
		/// Clients should use this rather than running string.Format themselves,
		/// because this has error checking and a helpful message, should the number
		/// of parameters be wrong.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="translationNotes">just for the string scanner's use</param>
		/// <param name="args">arguments to the string, used in string.format</param>
		/// <returns></returns>
		public static string GetFormatted(string id, string translationNotes, params object[] args)
		{
			//todo: this doesn't notice if the catalog has too few arugment slots, e.g.
			//if it says "blah" when it should say "blah{0}"

			try
			{
				var s = Get(id, translationNotes);
				try
				{
					s = String.Format(s, args);
					return s;
				}
				catch(Exception e)
				{
					Reporting.ErrorReport.NotifyUserOfProblem(
						"There was a problem localizing\r\n'{0}'\r\ninto this UI language... check number of parameters. The code expects there to be {1}.  The current localized string is\r\n'{2}'.\r\nThe error was {3}", id, args.Length, s, e.Message);

					return "!!"+s; // show it without the formatting
				}
			}
			catch(Exception e)
			{
				return "Error localizing string '" + id + "' to this UI language";
			}
		}

		public static string Get(string id, string translationNotes)
		{
			if (!String.IsNullOrEmpty(id) && id[0] == '~')
			{
				id = id.Substring(1);
			}
			if (_singleton == null) //todo: this should not be needed
			{
				return id;
			}

			if (_inInternationalizationTestMode)
			{
				return "*"+_singleton[id];
			}
			else
			{
				return _singleton[id];
			}
		}


		private void Init()
		{
			_singleton = this;
			_catalog = new Dictionary<string, string>();
		}

		private static string GetStringBetweenQuotes(string line)
		{
			int s = line.IndexOf('"');
			int f = line.LastIndexOf('"');
			return line.Substring(s + 1, f - (s + 1));
		}

		public string this[string id]
		{
			get
			{
				string translated = null;
				if (_catalog.ContainsKey(id))
				{
					return _catalog[id];
				}
				//REVIEW: What's this about?  It was   id = id.Replace("&&", "&");  which was removing the && we need when it gets to the UI
				var idWithSingleAmpersand  =id.Replace("&&", "&");
				if (_catalog.ContainsKey(idWithSingleAmpersand))
				{
					return _catalog[idWithSingleAmpersand];
				}
				return id;
			}
		}

		public static Font LabelFont
		{
			get
			{
				if (_font == null)
				{
					_font = new Font(FontFamily.GenericSansSerif, 9);
				}
				return _font;
			}
			set
			{
				_font = value;
			}
		}
		public static Font ModifyFontForLocalization(Font incoming)
		{
			float sBaseFontSizeInPoints = (float)8.25;
			float points = incoming.SizeInPoints + (StringCatalog.LabelFont.SizeInPoints- sBaseFontSizeInPoints);
			//float points = incoming.SizeInPoints * (StringCatalog.LabelFont.SizeInPoints / sBaseFontSizeInPoints);
			// 0 < points <= System.Single.MaxValue must be true or Font will throw
			points = Math.Max(Single.Epsilon, Math.Min(Single.MaxValue, points));
			return new Font(StringCatalog.LabelFont.Name, points, incoming.Style);

		}
	}
}