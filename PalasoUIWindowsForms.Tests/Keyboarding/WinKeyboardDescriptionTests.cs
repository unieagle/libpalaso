// Copyright (c) 2014 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
#if !__MonoCS__
using System;
using System.Collections.Generic;
using Microsoft.Unmanaged.TSF;
using NUnit.Framework;
using Palaso.Tests.Code;
using Palaso.UI.WindowsForms.Keyboarding.Windows;
using Palaso.WritingSystems;

namespace PalasoUIWindowsForms.Tests.Keyboarding
{
	/// <summary>
	/// This class tests that the Clone method of WinKeyboardDescription clones all required
	/// fields and uses them in equality testing.
	/// </summary>
	/// <remarks>All the tests are in the base class!</remarks>
	class WinKeyboardDescriptionIClonableGenericTests :
		IClonableGenericTests<WinKeyboardDescription, IKeyboardDefinition>
	{
		public override WinKeyboardDescription CreateNewClonable()
		{
			return new WinKeyboardDescription("en", "US", new WinKeyboardAdaptor());
		}

		public override string ExceptionList
		{
			get { return "|Engine|InputLanguage|"; }
		}

		public override string EqualsExceptionList
		{
			get
			{
				return "|Type|Name|OperatingSystem|IsAvailable|InternalName|InternalLocalizedName|" +
					"ConversionMode|SentenceMode|WindowHandle|InputProcessorProfile|";
			}
		}

		protected override List<ValuesToSet> DefaultValuesForTypes
		{
			get
			{
				return new List<ValuesToSet>
				{
					new ValuesToSet(true, false),
					new ValuesToSet("to be", "!(to be)"),
					new ValuesToSet(PlatformID.Win32NT, PlatformID.Unix),
					new ValuesToSet(KeyboardType.OtherIm, KeyboardType.System),
					new ValuesToSet(1, 100),
					new ValuesToSet((IntPtr) 1, (IntPtr) 2),
					new ValuesToSet(new TfInputProcessorProfile {Flags = TfIppFlags.Enabled | TfIppFlags.Active},
						new TfInputProcessorProfile {Flags = TfIppFlags.Enabled})
				};
			}
		}
	}
}

#endif
