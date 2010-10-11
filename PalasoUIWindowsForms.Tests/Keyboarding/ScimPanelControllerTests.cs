using System;
using NUnit.Framework;
using Palaso.UI.WindowsForms.Keyboarding;
using System.Collections.Generic;
using System.Windows.Forms;
using Palaso.Keyboarding;

#if MONO

namespace PalasoUIWindowsForms.Tests.Keyboarding
{
	[TestFixture]
	public class ScimPanelControllerTests
	{
		private Form _window;

		private void RequiresWindowForFocus()
		{
			_window = new Form();
			TextBox box = new TextBox();
			box.Dock = DockStyle.Fill;
			_window.Controls.Add(box);

			_window.Show();
			box.Select();
			Application.DoEvents();
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void EngineAvailable_ScimIsSetUpAndConfiguredCorrectly_ReturnsTrue()
		{
			Assert.IsTrue(ScimPanelController.Singleton.EngineAvailable);
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void GetActiveKeyboard_ScimIsSetUpAndConfiguredToDefault_ReturnsEnglishKeyboard()
		{
			RequiresWindowForFocus();
			ResetKeyboardToDefault();
			Assert.AreEqual("English/Keyboard", ScimPanelController.Singleton.GetActiveKeyboard());
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void KeyboardDescriptors_ScimIsSetUpAndConfiguredToDefault_3KeyboardsReturned()
		{
			Assert.AreEqual("English/European", ScimPanelController.Singleton.KeyboardDescriptors[0].KeyboardName);
			Assert.AreEqual("RAW CODE", ScimPanelController.Singleton.KeyboardDescriptors[1].KeyboardName);
			Assert.AreEqual("English/Keyboard", ScimPanelController.Singleton.KeyboardDescriptors[2].KeyboardName);
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void HasKeyboardNamed_ScimHasKeyboard_ReturnsTrue()
		{
			Assert.IsTrue(ScimPanelController.Singleton.HasKeyboardNamed("English/Keyboard"));
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void HasKeyboardNamed_ScimDoesNotHaveKeyboard_ReturnsFalse()
		{
			Assert.IsFalse(ScimPanelController.Singleton.HasKeyboardNamed("Nonexistant Keyboard"));
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void Deactivate_ScimIsRunning_GetCurrentKeyboardReturnsEnglishKeyboard()
		{
			RequiresWindowForFocus();
			ScimPanelController.Singleton.ActivateKeyboard("English/European");
			ScimPanelController.Singleton.Deactivate();
			Assert.AreEqual("English/Keyboard", ScimPanelController.Singleton.GetActiveKeyboard());
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void ActivateKeyBoard_ScimHasKeyboard_GetCurrentKeyboardReturnsActivatedKeyboard()
		{
			RequiresWindowForFocus();
			ResetKeyboardToDefault();
			ScimPanelController.Singleton.ActivateKeyboard("English/European");
			Assert.AreEqual("English/European", ScimPanelController.Singleton.GetActiveKeyboard());
			ResetKeyboardToDefault();
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void ActivateKeyBoard_ScimDoesNotHaveKeyboard_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(
				() => ScimPanelController.Singleton.ActivateKeyboard("Nonexistant Keyboard")
			);
		}

		[Test]
		[NUnit.Framework.Category("Scim")]
		public void GetCurrentInputContext_ScimIsRunning_ReturnsContext()
		{
			const int unrealisticClientId = -2;
			const int unrealisticContextClientId = -2;

			ScimPanelController.ContextInfo currentContext;
			currentContext.frontendClient = unrealisticClientId;
			currentContext.context = unrealisticContextClientId;
			currentContext = ScimPanelController.Singleton.GetCurrentInputContext();
			Assert.AreNotEqual(unrealisticClientId, currentContext.frontendClient);
			Assert.AreNotEqual(unrealisticContextClientId, currentContext.context);
		}

		private void ResetKeyboardToDefault()
		{
			ScimPanelController.Singleton.Deactivate();
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void Deactivate_ScimIsNotRunning_DoesNotThrow()
		{
			ScimPanelController.Singleton.Deactivate();
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void ActivateKeyBoard_ScimIsNotRunning_DoesNotThrow()
		{
			ScimPanelController.Singleton.ActivateKeyboard("English/Keyboard");
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void KeyboardDescriptors_ScimIsNotRunning_ReturnsEmptyList()
		{
			List<KeyboardDescriptor> availableKeyboards = ScimPanelController.Singleton.KeyboardDescriptors;
			Assert.AreEqual(0, availableKeyboards.Count);
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void GetActiveKeyboard_ScimIsNotRunning_ReturnsEmptyString()
		{
			string activeKeyboard = ScimPanelController.Singleton.GetActiveKeyboard();
			Assert.IsEmpty(activeKeyboard);
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void EngineAvailable_ScimIsnotRunning_returnsFalse()
		{
			Assert.IsFalse(ScimPanelController.Singleton.EngineAvailable);
		}

		[Test]
		[NUnit.Framework.Category("Scim not Running")]
		public void HasKeyboardNamed_ScimIsNotRunning_ReturnsFalse()
		{
			Assert.IsFalse(ScimPanelController.Singleton.HasKeyboardNamed("English/Keyboard"));
		}
	}
}

#endif