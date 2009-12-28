using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Palaso.UI.WindowsForms.Keyboarding;
using Palaso.WritingSystems;

namespace Palaso.UI.WindowsForms.WritingSystems
{
	public class WritingSystemVariantSuggestor: IWritingSystemVariantSuggestor
	{
		/// <summary>
		/// these are ordered in terms of perference, so the last one is just the fallback
		/// </summary>
		string[] fontsForIPA = { "arial unicode ms", "lucinda sans unicode", "doulous sil", FontFamily.GenericSansSerif.Name };


		public IEnumerable<WritingSystemDefinition> GetSuggestions(WritingSystemDefinition primary, IEnumerable<WritingSystemDefinition> existingWritingSystemsForLanguage)
		{
			if(string.IsNullOrEmpty(primary.ISO))
				yield break;

			if (!existingWritingSystemsForLanguage.Any(def => def.Script == "ipa" && string.IsNullOrEmpty(def.Variant)))
			{
				var x= new WritingSystemDefinition(primary.ISO, "ipa",primary.Region, primary.Variant, primary.LanguageName, "ipa", false);
				x.DefaultFontSize = primary.DefaultFontSize;
				x.DefaultFontName = fontsForIPA.FirstOrDefault(FontExists);

				x.Keyboard = Keyboarding.KeyboardController.GetIpaKeyboardIfAvailable();
				yield return x;
			}
		}

		private bool FontExists(string name)
		{
			var f = new Font(name, 12);
			return f.Name.ToLower()==name.ToLower();
		}
	}
}