// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Palaso.Reporting;
using Palaso.WritingSystems;
using Palaso.UI.WindowsForms.Keyboarding.InternalInterfaces;
using System.Diagnostics;
using System.Text;


#if __MonoCS__
using Palaso.UI.WindowsForms.Keyboarding.Linux;
#else
using Palaso.UI.WindowsForms.Keyboarding.Windows;
using Microsoft.Unmanaged.TSF;
#endif
using Palaso.UI.WindowsForms.Keyboarding.Types;

namespace Palaso.UI.WindowsForms.Keyboarding
{
	/// <summary>
	/// Singleton class with methods for registering different keyboarding engines (e.g. Windows
	/// system, Keyman, XKB, IBus keyboards), and activating keyboards.
	/// Clients have to call KeyboardController.Initialize() before they can start using the
	/// keyboarding functionality, and they have to call KeyboardController.Shutdown() before
	/// the application or the unit test exits.
	/// </summary>
	public static class KeyboardController
	{
		#region Nested Manager class
		/// <summary>
		/// Allows setting different keyboard adapters which is needed for tests. Also allows
		/// registering keyboard layouts.
		/// </summary>
		/// <remarks>Beware that the public methods of this class get called by unit tests (e.g.
		/// SIL.FieldWorks.Common.RootSites.SimpleRootSiteTests.IbusRootSiteEventHandlerTests), so
		/// don't change the visibility of these methods without discussing this on the sil-lsdev
		/// mailing list.
		/// </remarks>
		public static class Manager
		{
			/// <summary>
			/// Sets the available keyboard retrievers. Note that if this is called more than once,
			/// the retrievers installed previously will be closed and no longer useable. Do not
			/// pass retriever instances that have been previously passed. At least one retriever
			/// must be of type System.
			/// </summary>
			public static void SetKeyboardRetrievers(IKeyboardRetrievingAdaptor[] retrievers)
			{
				if (!(Keyboard.Controller is IKeyboardControllerImpl))
					Keyboard.Controller = new KeyboardControllerImpl();

				Instance.Keyboards.Clear(); // InitializeAdaptors below will fill it in again.

				if (KeyboardRetrievers == null)
					KeyboardRetrievers = new Dictionary<KeyboardType, IKeyboardRetrievingAdaptor>();

				foreach (var retriever in KeyboardRetrievers.Values)
					retriever.Close();

				KeyboardRetrievers.Clear();

				RegisterAvailableKeyboardRetrievers(retrievers);
			}

			/// <summary>
			/// Resets the keyboard adaptors to the default ones.
			/// </summary>
			public static void Reset()
			{
				SetKeyboardRetrievers(new IKeyboardRetrievingAdaptor[] {
#if __MonoCS__
					new XkbKeyboardRetrievingAdaptor(), new IbusKeyboardRetrievingAdaptor(),
					new UnityXkbKeyboardRetrievingAdaptor(), new UnityIbusKeyboardRetrievingAdaptor(),
					new CombinedIbusKeyboardRetrievingAdaptor()
#else
					new WinKeyboardAdaptor(), new KeymanKeyboardAdaptor(),
#endif
				});
			}

			private static void RegisterAvailableKeyboardRetrievers(IKeyboardRetrievingAdaptor[] retrievers)
			{
				foreach (var retriever in retrievers)
				{
					if (retriever.IsApplicable)
					{
						if ((retriever.Type & KeyboardType.System) == KeyboardType.System)
							KeyboardRetrievers[KeyboardType.System] = retriever;
						if ((retriever.Type & KeyboardType.OtherIm) == KeyboardType.OtherIm)
							KeyboardRetrievers[KeyboardType.OtherIm] = retriever;
					}
				}

				foreach (var retriever in retrievers)
				{
					if (!KeyboardRetrievers.ContainsValue(retriever))
						retriever.Close();
				}

				// Now that we know who can deal with the keyboards we can retrieve all available
				// keyboards, as well as add the used keyboard adaptors as error report property.
				var bldr = new StringBuilder();
				foreach (var retriever in KeyboardRetrievers.Values)
				{
					retriever.Initialize();
					retriever.RegisterAvailableKeyboards();

					if (bldr.Length > 0)
						bldr.Append(", ");
					bldr.Append(retriever.GetType().Name);
				}
				ErrorReport.AddProperty("KeyboardAdaptors", bldr.ToString());
				Logger.WriteEvent("Keyboard adaptors in use: {0}", bldr.ToString());
			}

			/// <summary>
			/// Adds a keyboard to the list of installed keyboards
			/// </summary>
			/// <param name='description'>Keyboard description object</param>
			public static void RegisterKeyboard(IKeyboardDefinition description)
			{
				if (!Instance.Keyboards.Contains(description))
					Instance.Keyboards.Add(description);
			}

			internal static void ClearAllKeyboards()
			{
				Instance.Keyboards.Clear();
			}
		}
		#endregion

		#region Class KeyboardControllerImpl
		private sealed class KeyboardControllerImpl : IKeyboardController, IKeyboardControllerImpl, IDisposable
		{
			private List<string> LanguagesAlreadyShownKeyboardNotFoundMessages { get; set; }
			private IKeyboardDefinition m_ActiveKeyboard;
			public KeyboardCollection Keyboards { get; private set; }
			public Dictionary<Control, object> EventHandlers { get; private set; }
			public event RegisterEventHandler ControlAdded;
			public event ControlEventHandler ControlRemoving;

			public KeyboardControllerImpl()
			{
				Keyboards = new KeyboardCollection();
				EventHandlers = new Dictionary<Control, object>();
				LanguagesAlreadyShownKeyboardNotFoundMessages = new List<string>();
			}

			public void UpdateAvailableKeyboards()
			{
				Keyboards.Clear();
				foreach (var retriever in KeyboardRetrievers.Values)
					retriever.UpdateAvailableKeyboards();
			}

			#region Disposable stuff
#if DEBUG
			/// <summary/>
			~KeyboardControllerImpl()
			{
				Dispose(false);
			}
#endif

			/// <summary/>
			public bool IsDisposed { get; private set; }

			/// <summary/>
			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			/// <summary/>
			private void Dispose(bool fDisposing)
			{
				System.Diagnostics.Debug.WriteLineIf(!fDisposing,
					"****** Missing Dispose() call for " + GetType() + ". *******");
				if (fDisposing && !IsDisposed)
				{
					// dispose managed and unmanaged objects
					if (KeyboardRetrievers != null)
					{
						foreach (var retriever in KeyboardRetrievers.Values)
							retriever.Close();
						KeyboardRetrievers = null;
					}
				}
				IsDisposed = true;
			}
			#endregion

			public IKeyboardDefinition DefaultKeyboard
			{
				get
				{
					return KeyboardRetrievers[KeyboardType.System].Adaptor.DefaultKeyboard;
				}
			}

			public IKeyboardDefinition GetKeyboard(string layoutNameWithLocale)
			{
				if (string.IsNullOrEmpty(layoutNameWithLocale))
					return KeyboardDescription.Zero;

				if (Keyboards.Contains(layoutNameWithLocale))
					return Keyboards[layoutNameWithLocale];

				var parts = layoutNameWithLocale.Split('|');
				if (parts.Length == 2)
				{
					// This is the way Paratext stored IDs in 7.4-7.5 while there was a temporary bug-fix in place)
					return GetKeyboard(parts[0], parts[1]);
				}

				// Handle old Palaso IDs
				parts = layoutNameWithLocale.Split('-');
				if (parts.Length > 1)
				{
					for (int i = 1; i < parts.Length; i++)
					{
						var kb = GetKeyboard(string.Join("-", parts.Take(i)), string.Join("-", parts.Skip(i)));
						if (!kb.Equals(KeyboardDescription.Zero))
							return kb;
					}
				}

				return KeyboardDescription.Zero;
			}

			public IKeyboardDefinition GetKeyboard(string layoutName, string locale)
			{
				if (string.IsNullOrEmpty(layoutName) && string.IsNullOrEmpty(locale))
					return KeyboardDescription.Zero;

				if (Keyboards.Contains(layoutName, locale))
					return Keyboards[layoutName, locale];
				return KeyboardDescription.Zero;
			}

			/// <summary>
			/// Tries to get the keyboard for the specified <paramref name="writingSystem"/>.
			/// </summary>
			/// <returns>
			/// Returns <c>KeyboardDescription.Zero</c> if no keyboard can be found.
			/// </returns>
			public IKeyboardDefinition GetKeyboard(IWritingSystemDefinition writingSystem)
			{
				if (writingSystem == null)
					return KeyboardDescription.Zero;
				return writingSystem.LocalKeyboard ?? KeyboardDescription.Zero;
			}

			public IKeyboardDefinition GetKeyboard(IInputLanguage language)
			{
				// NOTE: on Windows InputLanguage.LayoutName returns a wrong name in some cases.
				// Therefore we need this overload so that we can identify the keyboard correctly.
				return Keyboards
					.Where(keyboard => keyboard is KeyboardDescription &&
						((KeyboardDescription)keyboard).InputLanguage != null &&
						((KeyboardDescription)keyboard).InputLanguage.Equals(language))
					.DefaultIfEmpty(KeyboardDescription.Zero)
					.First();
			}

			/// <summary>
			/// Sets the keyboard.
			/// </summary>
			/// <param name='layoutName'>Keyboard layout name</param>
			public void SetKeyboard(string layoutName)
			{
				var keyboard = GetKeyboard(layoutName);
				if (keyboard.Equals(KeyboardDescription.Zero))
				{
					if (!LanguagesAlreadyShownKeyboardNotFoundMessages.Contains(layoutName))
					{
						LanguagesAlreadyShownKeyboardNotFoundMessages.Add(layoutName);
						ErrorReport.NotifyUserOfProblem(new ShowOncePerSessionBasedOnExactMessagePolicy(),
							"Could not find a keyboard ime that had a keyboard named '{0}'", layoutName);
					}
					return;
				}

				SetKeyboard(keyboard);
			}

			public void SetKeyboard(string layoutName, string locale)
			{
				SetKeyboard(GetKeyboard(layoutName, locale));
			}

			public void SetKeyboard(IWritingSystemDefinition writingSystem)
			{
				SetKeyboard(writingSystem.LocalKeyboard);
			}

			public void SetKeyboard(IInputLanguage language)
			{
				SetKeyboard(GetKeyboard(language));
			}

			public void SetKeyboard(IKeyboardDefinition keyboard)
			{
				if (keyboard != null) // This should prevent the crash from LT-15498
					keyboard.Activate();
			}

			/// <summary>
			/// Activates the keyboard of the default input language
			/// </summary>
			public void ActivateDefaultKeyboard()
			{
				SetKeyboard(DefaultKeyboard);
			}

			/// <summary>
			/// Returns everything that is installed on the system and available to be used.
			/// This would typically be used to populate a list of available keyboards in configuring a writing system.
			/// </summary>
			public IEnumerable<IKeyboardDefinition> AllAvailableKeyboards
			{
				get { return Keyboards; }
			}

			/// <summary>
			/// Creates and returns a keyboard definition object based on the layout and locale.
			/// </summary>
			public IKeyboardDefinition CreateKeyboardDefinition(string layout, string locale)
			{
				var existingKeyboard = AllAvailableKeyboards.FirstOrDefault(keyboard => keyboard.Layout == layout && keyboard.Locale == locale);
				return existingKeyboard ??
					KeyboardRetrievers[KeyboardType.System].CreateKeyboardDefinition(layout, locale);
			}

			/// <summary>
			/// Gets or sets the currently active keyboard
			/// </summary>
			public IKeyboardDefinition ActiveKeyboard
			{
				get
				{
					if (m_ActiveKeyboard == null)
					{
						m_ActiveKeyboard = KeyboardRetrievers[KeyboardType.System].Adaptor.ActiveKeyboard;
						if (m_ActiveKeyboard == null)
						{
							try
							{
								var lang = InputLanguage.CurrentInputLanguage;
								m_ActiveKeyboard = GetKeyboard(lang.LayoutName, lang.Culture.Name);
							}
							catch (CultureNotFoundException)
							{
							}
						}
						if (m_ActiveKeyboard == null)
							m_ActiveKeyboard = KeyboardDescription.Zero;
					}
					return m_ActiveKeyboard;
				}
				set { m_ActiveKeyboard = value; }
			}

			/// <summary>
			/// Figures out the system default keyboard for the specified writing system (the one to use if we have no available KnownKeyboards).
			/// The implementation may use obsolete fields such as Keyboard
			/// </summary>
			public IKeyboardDefinition DefaultForWritingSystem(IWritingSystemDefinition ws)
			{
				return LegacyForWritingSystem(ws) ?? DefaultKeyboard;
			}

			/// <summary>
			/// Finds a keyboard specified using one of the legacy fields. If such a keyboard is found, it is appropriate to
			/// automatically add it to KnownKeyboards. If one is not, a general DefaultKeyboard should NOT be added.
			/// </summary>
			/// <param name="ws"></param>
			/// <returns></returns>
			public IKeyboardDefinition LegacyForWritingSystem(IWritingSystemDefinition ws)
			{
				var legacyWs = ws as ILegacyWritingSystemDefinition;
				if (legacyWs == null)
					return DefaultKeyboard;

				return LegacyKeyboardHandling.GetKeyboardFromLegacyWritingSystem(legacyWs, this);
			}

			/// <summary>
			/// Registers the control for keyboarding. Called by KeyboardController.Register.
			/// </summary>
			public void RegisterControl(Control control, object eventHandler)
			{
				EventHandlers[control] = eventHandler;
				if (ControlAdded != null)
					ControlAdded(this, new RegisterEventArgs(control, eventHandler));
			}

			/// <summary>
			/// Unregisters the control from keyboarding. Called by KeyboardController.Unregister.
			/// </summary>
			/// <param name="control">Control.</param>
			public void UnregisterControl(Control control)
			{
				if (ControlRemoving != null)
					ControlRemoving(this, new ControlEventArgs(control));
				EventHandlers.Remove(control);
			}

			#region Legacy keyboard handling

			private static class LegacyKeyboardHandling
			{
				public static IKeyboardDefinition GetKeyboardFromLegacyWritingSystem(ILegacyWritingSystemDefinition ws,
					KeyboardControllerImpl controller)
				{
					if (!string.IsNullOrEmpty(ws.WindowsLcid))
					{
						var keyboard = HandleFwLegacyKeyboards(ws, controller);
						if (keyboard != null)
							return keyboard;
					}

					if (!string.IsNullOrEmpty(ws.Keyboard))
					{
						if (controller.Keyboards.Contains(ws.Keyboard))
							return controller.Keyboards[ws.Keyboard];

						// Palaso WinIME keyboard
						var locale = GetLocaleName(ws.Keyboard);
						var layout = GetLayoutName(ws.Keyboard);
						if (controller.Keyboards.Contains(layout, locale))
							return controller.Keyboards[layout, locale];

						// Palaso Keyman or Ibus keyboard
						var keyboard = controller.Keyboards.FirstOrDefault(kbd => kbd.Layout == layout);
						if (keyboard != null)
							return keyboard;
					}

					return null;
				}

				private static IKeyboardDefinition HandleFwLegacyKeyboards(ILegacyWritingSystemDefinition ws,
					KeyboardControllerImpl controller)
				{
					var lcid = GetLcid(ws);
					if (lcid >= 0)
					{
						try
						{
							if (string.IsNullOrEmpty(ws.Keyboard))
							{
								// FW system keyboard
								var keyboard = controller.Keyboards.FirstOrDefault(
									kbd =>
									{
										var keyboardDescription = kbd as KeyboardDescription;
										if (keyboardDescription == null || keyboardDescription.InputLanguage == null || keyboardDescription.InputLanguage.Culture == null)
											return false;
										return keyboardDescription.InputLanguage.Culture.LCID == lcid;
									});
								if (keyboard != null)
									return keyboard;
							}
							else
							{
								// FW keyman keyboard
								var culture = new CultureInfo(lcid);
								if (controller.Keyboards.Contains(ws.Keyboard, culture.Name))
									return controller.Keyboards[ws.Keyboard, culture.Name];
							}
						}
						catch (CultureNotFoundException)
						{
							// Culture specified by LCID is not supported on current system. Just ignore.
						}
					}
					return null;
				}

				private static int GetLcid(ILegacyWritingSystemDefinition ws)
				{

					int lcid;
					if (!Int32.TryParse(ws.WindowsLcid, out lcid))
						lcid = -1; // can't convert ws.WindowsLcid to a valid LCID. Just ignore.
					return lcid;
				}

				private static string GetLocaleName(string name)
				{
					var split = name.Split(new[] { '-' });
					string localeName;
					if (split.Length <= 1)
					{
						localeName = string.Empty;
					}
					else if (split.Length > 1 && split.Length <= 3)
					{
						localeName = string.Join("-", split.Skip(1).ToArray());
					}
					else
					{
						localeName = string.Join("-", split.Skip(split.Length - 2).ToArray());
					}
					return localeName;
				}

				private static string GetLayoutName(string name)
				{
					//Just cut off the length of the locale + 1 for the dash
					var locale = GetLocaleName(name);
					if (string.IsNullOrEmpty(locale))
					{
						return name;
					}
					var layoutName = name.Substring(0, name.Length - (locale.Length + 1));
					return layoutName;
				}
			}
			#endregion
		}
		#endregion

		#region Static methods and properties

		/// <summary>
		/// Gets the current keyboard controller singleton.
		/// </summary>
		private static IKeyboardControllerImpl Instance
		{
			get
			{
				return Keyboard.Controller as IKeyboardControllerImpl;
			}
		}

		/// <summary>
		/// Enables the PalasoUIWinForms keyboarding. This should be called during application startup.
		/// </summary>
		public static void Initialize()
		{
			// Note: arguably it is undesirable to install this as the public keyboard controller before we initialize it
			// (the Reset call). However, we have not in general attempted thread safety for the keyboarding code; it seems
			// highly unlikely that any but the UI thread wants to manipulate keyboards. Apart from other threads, nothing
			// has a chance to access this before we return. If initialization does not complete successfully, we clear the
			// global.
			try
			{
				Keyboard.Controller = new KeyboardControllerImpl();
				Manager.Reset();
			}
			catch (Exception e)
			{
				Console.WriteLine("Got exception {0} initalizing keyboard controller", e.GetType());
				Console.WriteLine(e.StackTrace);
				Logger.WriteEvent("Got exception {0} initalizing keyboard controller", e.GetType());
				Logger.WriteEvent(e.StackTrace);

				if (Keyboard.Controller != null)
					Keyboard.Controller.Dispose();
				Keyboard.Controller = null;
				throw;
			}
		}

		/// <summary>
		/// Ends support for the PalasoUIWinForms keyboarding
		/// </summary>
		public static void Shutdown()
		{
			if (Instance == null)
				return;

			Instance.Dispose();
			Keyboard.Controller = null;
		}

		internal static Dictionary<KeyboardType, IKeyboardRetrievingAdaptor> KeyboardRetrievers { get; private set; }

		internal static string GetKeyboardSetupApplication(out string arguments)
		{
			string program = null;
			arguments = null;
			if (!HasSecondaryKeyboardSetupApplication && KeyboardRetrievers.ContainsKey(KeyboardType.OtherIm))
				program = KeyboardRetrievers[KeyboardType.OtherIm].GetKeyboardSetupApplication(out arguments);

			if (string.IsNullOrEmpty(program))
				program = KeyboardRetrievers[KeyboardType.System].GetKeyboardSetupApplication(out arguments);

			return program;
		}

		internal static string GetSecondaryKeyboardSetupApplication(out string arguments)
		{
			string program = null;
			arguments = null;
			if (HasSecondaryKeyboardSetupApplication && KeyboardRetrievers.ContainsKey(KeyboardType.OtherIm))
				program = KeyboardRetrievers[KeyboardType.OtherIm].GetKeyboardSetupApplication(out arguments);

			return program;
		}

		/// <summary>
		/// Returns <c>true</c> if there is a secondary keyboard application available, e.g.
		/// Keyman setup dialog on Windows.
		/// </summary>
		internal static bool HasSecondaryKeyboardSetupApplication
		{
			get
			{
				return KeyboardRetrievers.ContainsKey(KeyboardType.OtherIm) &&
					KeyboardRetrievers[KeyboardType.OtherIm].IsSecondaryKeyboardSetupApplication;
			}
		}

		/// <summary>
		/// Gets the currently active keyboard
		/// </summary>
		public static IKeyboardDefinition ActiveKeyboard
		{
			get { return Instance.ActiveKeyboard; }
		}

		/// <summary>
		/// Returns <c>true</c> if KeyboardController.Initialize() got called before.
		/// </summary>
		public static bool IsInitialized { get { return Instance != null; }}

		/// <summary>
		/// Register the control for keyboarding, optionally providing an event handler for
		/// a keyboarding adapter. If <paramref ref="eventHandler"/> is <c>null</c> the
		/// default handler will be used.
		/// The application should call this method for each control that needs IME input before
		/// displaying the control, typically after calling the InitializeComponent() method.
		/// </summary>
		public static void Register(Control control, object eventHandler = null)
		{
			if (Instance == null)
				throw new ApplicationException("KeyboardController is not initialized! Please call KeyboardController.Initialize() first.");
			Instance.RegisterControl(control, eventHandler);
		}

		/// <summary>
		/// Unregister the control from keyboarding. The application should call this method
		/// prior to disposing the control so that the keyboard adapters can release unmanaged
		/// resources.
		/// </summary>
		public static void Unregister(Control control)
		{
			Instance.UnregisterControl(control);
		}

		internal static IKeyboardControllerImpl EventProvider
		{
			get { return Instance; }
		}

		// delegate used to detect input processor, like KeyMan
		private delegate bool IsUsingInputProcessorDelegate();
		private static IsUsingInputProcessorDelegate _isUsingInputProcessor;

		/// <summary>
		/// Returns true if the current input device is an Input Processor, like KeyMan.
		/// </summary>
		public static bool IsFormUsingInputProcessor(Form frm)
		{
			bool usingIP;
			if (frm.InvokeRequired)
			{
				// Set up a delegate for the invoke
				if (_isUsingInputProcessor == null)
					_isUsingInputProcessor = IsUsingInputProcessor;

				usingIP = (bool)frm.Invoke(_isUsingInputProcessor);
			}
			else
			{
				usingIP = IsUsingInputProcessor();
			}

			return usingIP;
		}

		
		private static bool IsUsingInputProcessor()
		{
#if __MonoCS__
			// not yet implemented on Linux
			return false;
#else
			TfInputProcessorProfilesClass inputProcessor;
			try
			{
				inputProcessor = new TfInputProcessorProfilesClass();
			}
			catch (InvalidCastException)
			{
				return false;
			}

			var profileMgr = inputProcessor as ITfInputProcessorProfileMgr;

			if (profileMgr == null) return false;

			var profile = profileMgr.GetActiveProfile(Guids.TfcatTipKeyboard);
			return profile.ProfileType == TfProfileType.InputProcessor;
#endif
		}
		#endregion

	}
}
