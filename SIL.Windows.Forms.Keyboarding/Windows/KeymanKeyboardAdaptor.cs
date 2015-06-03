// Copyright (c) 2014 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)

#if !__MonoCS__
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Keyman7Interop;
using SIL.Keyboarding;
using SIL.Reporting;

namespace SIL.Windows.Forms.Keyboarding.Windows
{
	/// <summary>
	/// Class for handling Keyman keyboards not associated with a Windows language
	/// </summary>
	internal class KeymanKeyboardAdaptor : IKeyboardAdaptor
	{
		#region IKeyboardAdaptor Members

		public void Initialize()
		{
			CheckDisposed();
			UpdateAvailableKeyboards();
		}

		public void UpdateAvailableKeyboards()
		{
			CheckDisposed();
			Dictionary<string, KeymanKeyboardDescription> curKeyboards = KeyboardController.Instance.Keyboards.OfType<KeymanKeyboardDescription>().ToDictionary(kd => kd.Id);
			// Try the Keyman 7/8 interface
			try
			{
				var keyman = new TavultesoftKeymanClass();
				UpdateKeyboards(curKeyboards, keyman.Keyboards.OfType<IKeymanKeyboard>().Select(kb => kb.Name), false);
			}
			catch (Exception)
			{				
				// Keyman 7 isn't installed or whatever.
			}

			// Try the Keyman 6 interface
			try
			{
				var keymanLink = new KeymanLink.KeymanLink();
				if (keymanLink.Initialize())
					UpdateKeyboards(curKeyboards, keymanLink.Keyboards.Select(kb => kb.KbdName), true);
			}
			catch (Exception)
			{
				// Keyman 6 isn't installed or whatever.
			}

			foreach (KeymanKeyboardDescription keyboard in curKeyboards.Values)
				keyboard.SetIsAvailable(false);
		}

		private void UpdateKeyboards(Dictionary<string, KeymanKeyboardDescription> curKeyboards, IEnumerable<string> availableKeyboardNames, bool isKeyman6)
		{
			foreach (string keyboardName in availableKeyboardNames)
			{
				KeymanKeyboardDescription existingKeyboard;
				if (curKeyboards.TryGetValue(keyboardName, out existingKeyboard))
				{
					if (!existingKeyboard.IsAvailable)
					{
						existingKeyboard.SetIsAvailable(true);
						existingKeyboard.IsKeyman6 = isKeyman6;
						if (existingKeyboard.Format == KeyboardFormat.Unknown)
							existingKeyboard.Format = KeyboardFormat.CompiledKeyman;
					}
					curKeyboards.Remove(keyboardName);
				}
				else
				{
					KeyboardController.Instance.Keyboards.Add(new KeymanKeyboardDescription(keyboardName, isKeyman6, this, true) {Format = KeyboardFormat.CompiledKeyman});
				}
			}
		}

		public bool ActivateKeyboard(KeyboardDescription keyboard)
		{
			CheckDisposed();
			var keymanKbdDesc = (KeymanKeyboardDescription)keyboard;
			if (keymanKbdDesc.IsKeyman6)
			{
				try
				{
					var keymanLink = new KeymanLink.KeymanLink();
					if (!keymanLink.Initialize())
					{
						ErrorReport.NotifyUserOfProblem("Keyman6 could not be activated.");
						return false;
					}
					keymanLink.SelectKeymanKeyboard(keyboard.Id);
				}
				catch (Exception)
				{
					return false;
				}
			}
			else
			{
				try
				{
					var keyman = new TavultesoftKeymanClass();
					int oneBasedIndex = keyman.Keyboards.IndexOf(keyboard.Id);

					if (oneBasedIndex < 1)
					{
						ErrorReport.NotifyUserOfProblem("The keyboard '{0}' could not be activated using Keyman 7.",
							keyboard.Id);
						return false;
					}
					keyman.Control.ActiveKeyboard = keyman.Keyboards[oneBasedIndex];
				}
				catch (Exception)
				{
					// Keyman 7 not installed?
					return false;
				}
			}

			KeyboardController.Instance.ActiveKeyboard = keyboard;
			return true;
		}

		public void DeactivateKeyboard(KeyboardDescription keyboard)
		{
			CheckDisposed();
			try
			{
				if (((KeymanKeyboardDescription) keyboard).IsKeyman6)
				{
					var keymanLink = new KeymanLink.KeymanLink();
					if (keymanLink.Initialize())
						keymanLink.SelectKeymanKeyboard(null, false);
				}
				else
				{
					var keyman = new TavultesoftKeymanClass();
					keyman.Control.ActiveKeyboard = null;
				}
			}
			catch (Exception)
			{
				// Keyman not installed?
			}
		}

		/// <summary>
		/// Creates and returns a keyboard definition object based on the ID.
		/// Note that this method is used when we do NOT have a matching available keyboard.
		/// Therefore we can presume that the created one is NOT available.
		/// </summary>
		public KeyboardDescription CreateKeyboardDefinition(string id)
		{
			CheckDisposed();
			return new KeymanKeyboardDescription(id, false, this, false);
		}

		/// <summary>
		/// Gets the default keyboard of the system.
		/// </summary>
		public KeyboardDescription DefaultKeyboard
		{
			get
			{
				throw new NotImplementedException(
					"Keyman keyboards that are not associated with a language are never the system default.");
			}
		}

		/// <summary>
		/// Implementation is not required because this is not the primary (Type System) adapter.
		/// </summary>
		public KeyboardDescription ActiveKeyboard
		{
			get { throw new NotImplementedException(); }
		}

		/// <summary>
		/// The type of keyboards this adaptor handles: system or other (like Keyman, ibus...)
		/// </summary>
		public KeyboardAdaptorType Type
		{
			get
			{
				CheckDisposed();
				return KeyboardAdaptorType.OtherIm;
			}
		}

		public bool CanHandleFormat(KeyboardFormat format)
		{
			CheckDisposed();
			switch (format)
			{
				case KeyboardFormat.CompiledKeyman:
				case KeyboardFormat.Keyman:
					return true;
			}
			return false;
		}

		#endregion

		#region IDisposable & Co. implementation
		// Region last reviewed: never

		/// <summary>
		/// Check to see if the object has been disposed.
		/// All public Properties and Methods should call this
		/// before doing anything else.
		/// </summary>
		public void CheckDisposed()
		{
			if (IsDisposed)
				throw new ObjectDisposedException(String.Format("'{0}' in use after being disposed.", GetType().Name));
		}

		/// <summary>
		/// See if the object has been disposed.
		/// </summary>
		public bool IsDisposed { get; private set; }

		/// <summary>
		/// Finalizer, in case client doesn't dispose it.
		/// Force Dispose(false) if not already called (i.e. m_isDisposed is true)
		/// </summary>
		/// <remarks>
		/// In case some clients forget to dispose it directly.
		/// </remarks>
		~KeymanKeyboardAdaptor()
		{
			Dispose(false);
			// The base class finalizer is called automatically.
		}

		/// <summary>
		///
		/// </summary>
		/// <remarks>Must not be virtual.</remarks>
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SupressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Executes in two distinct scenarios.
		///
		/// 1. If disposing is true, the method has been called directly
		/// or indirectly by a user's code via the Dispose method.
		/// Both managed and unmanaged resources can be disposed.
		///
		/// 2. If disposing is false, the method has been called by the
		/// runtime from inside the finalizer and you should not reference (access)
		/// other managed objects, as they already have been garbage collected.
		/// Only unmanaged resources can be disposed.
		/// </summary>
		/// <param name="disposing"></param>
		/// <remarks>
		/// If any exceptions are thrown, that is fine.
		/// If the method is being done in a finalizer, it will be ignored.
		/// If it is thrown by client code calling Dispose,
		/// it needs to be handled by fixing the bug.
		///
		/// If subclasses override this method, they should call the base implementation.
		/// </remarks>
		protected virtual void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** " + GetType().Name + " 'disposing' is false. ******************");
			// Must not be run more than once.
			if (IsDisposed)
				return;

			if (disposing)
			{
				// Dispose managed resources here.
			}

			// Dispose unmanaged resources here, whether disposing is true or false.

			IsDisposed = true;
		}

		#endregion IDisposable & Co. implementation
	}
}
#endif
