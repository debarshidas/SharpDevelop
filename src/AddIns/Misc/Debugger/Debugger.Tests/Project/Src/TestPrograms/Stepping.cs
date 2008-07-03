﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbecký" email="dsrbecky@gmail.com"/>
//     <version>$Revision$</version>
// </file>

using System;

namespace Debugger.Tests.TestPrograms
{
	public class Stepping
	{
		public static void Main()
		{
			System.Diagnostics.Debugger.Break();
			System.Diagnostics.Debug.WriteLine("1"); // Step over external code
			Sub(); // Step in internal code
			Sub2(); // Step over internal code
		}
		
		public static void Sub()
		{ // Step in noop
			System.Diagnostics.Debug.WriteLine("2"); // Step in external code
			System.Diagnostics.Debug.WriteLine("3"); // Step out
			System.Diagnostics.Debug.WriteLine("4");
		}
		
		public static void Sub2()
		{
			System.Diagnostics.Debug.WriteLine("5");
		}
	}
}

#if TEST_CODE
namespace Debugger.Tests {
	public partial class DebuggerTests
	{
		[NUnit.Framework.Test]
		public void Stepping()
		{
			StartTest("Stepping.cs");
			
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepOver(); // Debugger.Break
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepOver(); // Debug.WriteLine 1
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepInto(); // Method Sub
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepInto(); // '{'
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepInto(); // Debug.WriteLine 2
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepOut(); // Method Sub
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepOver(); // Method Sub
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			process.SelectedStackFrame.StepOver(); // Method Sub2
			ObjectDumpToString("NextStatement", process.SelectedStackFrame.NextStatement);
			
			EndTest();
		}
	}
}
#endif

#if EXPECTED_OUTPUT
<?xml version="1.0" encoding="utf-8"?>
<DebuggerTests>
  <Test
    name="Stepping.cs">
    <ProcessStarted />
    <ModuleLoaded>mscorlib.dll (No symbols)</ModuleLoaded>
    <ModuleLoaded>Stepping.exe (Has symbols)</ModuleLoaded>
    <ModuleLoaded>System.dll (No symbols)</ModuleLoaded>
    <DebuggingPaused>Break</DebuggingPaused>
    <NextStatement>Stepping.cs:16,4-16,40</NextStatement>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:17,4-17,44</NextStatement>
    <ModuleLoaded>System.Configuration.dll (No symbols)</ModuleLoaded>
    <ModuleLoaded>System.Xml.dll (No symbols)</ModuleLoaded>
    <LogMessage>1\r\n</LogMessage>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:18,4-18,10</NextStatement>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:23,3-23,4</NextStatement>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:24,4-24,44</NextStatement>
    <LogMessage>2\r\n</LogMessage>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:25,4-25,44</NextStatement>
    <LogMessage>3\r\n</LogMessage>
    <LogMessage>4\r\n</LogMessage>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:18,4-18,10</NextStatement>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:19,4-19,11</NextStatement>
    <LogMessage>5\r\n</LogMessage>
    <DebuggingPaused>StepComplete</DebuggingPaused>
    <NextStatement>Stepping.cs:20,3-20,4</NextStatement>
    <ProcessExited />
  </Test>
</DebuggerTests>
#endif // EXPECTED_OUTPUT