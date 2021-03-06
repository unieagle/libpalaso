﻿// Copyright (c) 2014 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using Palaso.PlatformUtilities;

namespace Palaso.Tests.PlatformUtilities
{
	[TestFixture]
	public class PlatformTests
	{
		[Test]
		[Platform(Exclude="Net")]
		public void IsMono_Mono()
		{
			Assert.That(Platform.IsMono, Is.True);
		}

		[Test]
		[Platform(Include="Net")]
		public void IsMono_Net()
		{
			Assert.That(Platform.IsMono, Is.False);
		}

		[Test]
		[Platform(Exclude="Net")]
		public void IsDotnet_Mono()
		{
			Assert.That(Platform.IsDotNet, Is.False);
		}

		[Test]
		[Platform(Include="Net")]
		public void IsDotnet_Net()
		{
			Assert.That(Platform.IsDotNet, Is.True);
		}

#if SYSTEM_MAC
		[Test]
		public void IsLinux_Mac()
		{
			Assert.That(Platform.IsLinux, Is.False);
		}
#else
		[Test]
		[Platform(Include="Linux")]
		public void IsLinux_Linux()
		{
			Assert.That(Platform.IsLinux, Is.True);
		}
#endif

		[Test]
		[Platform(Include="Win")]
		public void IsLinux_Windows()
		{
			Assert.That(Platform.IsLinux, Is.False);
		}

#if SYSTEM_MAC
		[Test]
		public void IsWindows_Mac()
		{
			Assert.That(Platform.IsWindows, Is.False);
		}
#else
		[Test]
		[Platform(Include="Linux")]
		public void IsWindows_Linux()
		{
			Assert.That(Platform.IsWindows, Is.False);
		}
#endif

		[Test]
		[Platform(Include="Win")]
		public void IsWindows_Windows()
		{
			Assert.That(Platform.IsWindows, Is.True);
		}

#if SYSTEM_MAC
		[Test]
		public void IsMac_Mac()
		{
			Assert.That(Platform.IsMac, Is.True);
		}
#else
		[Test]
		[Platform(Include="Linux")]
		public void IsMac_Linux()
		{
			Assert.That(Platform.IsMac, Is.False);
		}
#endif

		[Test]
		[Platform(Include="Win")]
		public void IsMac_Windows()
		{
			Assert.That(Platform.IsMac, Is.False);
		}

#if SYSTEM_MAC
		[Test]
		public void IsUnix_Mac()
		{
			Assert.That(Platform.IsUnix, Is.True);
		}
#else
		[Test]
		[Platform(Include="Linux")]
		public void IsUnix_Linux()
		{
			Assert.That(Platform.IsUnix, Is.True);
		}
#endif

		[Test]
		[Platform(Include="Win")]
		public void IsUnix_Windows()
		{
			Assert.That(Platform.IsUnix, Is.False);
		}

		[Test]
		[Platform(Include = "Win", Reason = "Windows specific test")]
		public void DesktopEnvironment_Windows()
		{
			// SUT
			Assert.That(Platform.DesktopEnvironment, Is.EqualTo("Win32NT"));
		}

		[Platform(Include = "Linux", Reason = "Linux specific test")]
		[TestCase("Unity", null, "ubuntu", Result = "unity", TestName = "Unity")]
		[TestCase("Unity", "/usr/share/ubuntu:/usr/share/gnome:/usr/local/share/:/usr/share/",
			"ubuntu", Result = "unity", TestName = "Unity with dataDir")]
		[TestCase("GNOME", null, "gnome-shell", Result = "gnome",
			TestName = "Gnome shell")]
		[TestCase("GNOME", null, "cinnamon", Result = "cinnamon",
			TestName = "Wasta 12")]
		[TestCase("x-cinnamon", null, "cinnamon", Result = "x-cinnamon",
			TestName = "Wasta 14")]
		[TestCase(null, "/usr/share/ubuntu:/usr/share/kde:/usr/local/share/:/usr/share/",
			"kde-plasma", Result = "kde", TestName = "KDE on Ubuntu 12_04")]
		[TestCase("XFCE", null, "xubuntu", Result = "xfce", TestName = "XFCE")]
		[TestCase("foo", null, null, Result = "foo", TestName = "Only XDG_CURRENT_DESKTOP set")]
		[TestCase(null, "/usr/share/ubuntu:/usr/share/kde:/usr/local/share/:/usr/share/", null,
			Result = "kde", TestName = "Only XDG_DATA_DIRS set")]
		[TestCase(null, null, "something", Result = "something", TestName = "Only GDMSESSION set")]
		[TestCase(null, null, null, Result = "", TestName = "Nothing set")]
		public string DesktopEnvironment_SimulateDesktops(string currDesktop,
			string dataDirs, string gdmSession)
		{
			// See http://askubuntu.com/a/227669 for actual values on different systems

			// Setup
			Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", currDesktop);
			Environment.SetEnvironmentVariable("XDG_DATA_DIRS", dataDirs);
			Environment.SetEnvironmentVariable("GDMSESSION", gdmSession);

			// SUT
			return Platform.DesktopEnvironment;
		}

		[Test]
		[Platform(Include = "Win", Reason = "Windows specific test")]
		public void DesktopEnvironmentInfoString_Windows()
		{
			// SUT
			Assert.That(Platform.DesktopEnvironmentInfoString, Is.Empty);
		}

		[Platform(Include = "Linux", Reason = "Linux specific test")]
		[TestCase("Unity", null, "ubuntu", null, Result = "unity (ubuntu)", TestName = "Unity")]
		[TestCase("Unity", "/usr/share/ubuntu:/usr/share/gnome:/usr/local/share/:/usr/share/",
			"ubuntu", null, Result = "unity (ubuntu)", TestName = "Unity with dataDir")]
		[TestCase("Unity", null, "ubuntu", "session-1",
			Result = "unity (ubuntu [display server: Mir])", TestName = "Unity with Mir")]
		[TestCase("GNOME", null, "gnome-shell", null, Result = "gnome (gnome-shell)",
			TestName = "Gnome shell")]
		[TestCase("GNOME", null, "cinnamon", null, Result = "cinnamon (cinnamon)",
			TestName = "Wasta 12")]
		[TestCase("x-cinnamon", null, "cinnamon", null, Result = "x-cinnamon (cinnamon)",
			TestName = "Wasta 14")]
		[TestCase(null, "/usr/share/ubuntu:/usr/share/kde:/usr/local/share/:/usr/share/",
			"kde-plasma", null, Result = "kde (kde-plasma)", TestName = "KDE on Ubuntu 12_04")]
		[TestCase(null, null, null, null, Result = " (not set)", TestName = "Nothing set")]
		public string DesktopEnvironmentInfoString_SimulateDesktopEnvironments(string currDesktop,
			string dataDirs, string gdmSession, string mirServerName)
		{
			// See http://askubuntu.com/a/227669 for actual values on different systems

			// Setup
			Environment.SetEnvironmentVariable("XDG_CURRENT_DESKTOP", currDesktop);
			Environment.SetEnvironmentVariable("XDG_DATA_DIRS", dataDirs);
			Environment.SetEnvironmentVariable("GDMSESSION", gdmSession);
			Environment.SetEnvironmentVariable("MIR_SERVER_NAME", mirServerName);

			// SUT
			return Platform.DesktopEnvironmentInfoString;
		}

	}
}

