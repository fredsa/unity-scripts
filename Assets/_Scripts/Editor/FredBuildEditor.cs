using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Reflection;
using System;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class FredBuildEditor : EditorWindow
{
	static string TWO_LINES = ".*\n.*\n";

	static bool executing = false;

	static FredBuildEditor ()
	{
		CheckPasswords ();
	}

	[MenuItem ("FRED/Build %&b")]
	static void BuildGame ()
	{
		ClearLog ();
		UnityEngine.Debug.Log ("FRED/Build " + EditorUserBuildSettings.activeBuildTarget + "\n");

		CheckPasswords ();

		string binary;
		switch (EditorUserBuildSettings.activeBuildTarget) {
		case BuildTarget.Android:
			binary = PlayerSettings.bundleIdentifier + ".apk";
			break;
		case BuildTarget.StandaloneWindows:
			DirectoryInfo projectRoot = Directory.GetParent (Application.dataPath);
			string projectDirname = projectRoot.Name;
			binary = projectRoot + "/" + projectDirname + ".exe";
			break;
		default:
			throw new NotImplementedException ("Build target " + EditorUserBuildSettings.activeBuildTarget);
		}
		DateTime lastWriteTime = File.GetLastWriteTime (binary);
		UnityEngine.Debug.Log ("- Binary: " + binary + "\n");

		BuildPipeline.BuildPlayer (GetSceneNames (), binary, EditorUserBuildSettings.activeBuildTarget, BuildOptions.None);

		if (File.GetLastWriteTime (binary).Equals (lastWriteTime)) {
			UnityEngine.Debug.LogError ("Failed to build " + binary);
		} else {
			UnityEngine.Debug.Log ("Successfully built " + binary);
			if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
				UnityEngine.Debug.LogWarning ("Don't forget to use ALT-CMD-I to install.");
			}
		}
	}

	static string[] GetSceneNames ()
	{
		string[] names = new string[SceneManager.sceneCount];
		for (int i = 0; i < SceneManager.sceneCount; i++) {
			names [i] = SceneManager.GetSceneAt (i).path;
			UnityEngine.Debug.Log ("- Scene " + i + ": " + names [i] + "\n");
		}
		return names;
	}

	[MenuItem ("FRED/Install %&i")]
	static void ReinstallGame ()
	{
		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
			UnityEngine.Debug.LogError ("Build target = " + EditorUserBuildSettings.activeBuildTarget + " (=NOT " + BuildTarget.Android + ")");
			return;
		}

		if (executing) {
			UnityEngine.Debug.LogError ("Already executing !!");
			return;
		}

		ClearLog ();
		UnityEngine.Debug.Log ("FRED/Install " + EditorUserBuildSettings.activeBuildTarget + "\n");

		new Thread (new ThreadStart (InstallApk)).Start ();
	}

	static void InstallApk ()
	{
		executing = true;
		UnityEngine.Debug.Log ("$ ./reinstall.sh");
		Execute ("/bin/bash", "-lc", "./reinstall.sh");
		executing = false;
	}

	static int Execute (string cmd, params string[] args)
	{
		string joinedArgs = string.Join (" ", args);
		Process proc = new Process ();
		proc.StartInfo.UseShellExecute = false;
		proc.StartInfo.CreateNoWindow = true;
		proc.StartInfo.ErrorDialog = false;
		proc.StartInfo.RedirectStandardError = true;
		proc.StartInfo.RedirectStandardOutput = true;
		proc.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
		proc.StartInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
		proc.StartInfo.FileName = cmd;
		proc.StartInfo.Arguments = joinedArgs;

		// Show two output lines at a time in Editor
		string output = "";
		proc.OutputDataReceived += new DataReceivedEventHandler (
			(sender, evt) => {
				if (evt.Data != null) {
					output += evt.Data + "\n";
					output = StripEmptyLines (output);
					// log two lines at a time
					foreach (Match match in Regex.Matches (output, TWO_LINES, RegexOptions.Multiline)) {
						UnityEngine.Debug.Log (PrefixOutput (match.Value));
						output = output.Substring (match.Value.Length);
					}
				}
			}
		);

		// Show two output lines at a time in Editor
		string error = "";
		proc.ErrorDataReceived += new DataReceivedEventHandler (
			(sender, evt) => {
				if (evt.Data != null) {
					error += evt.Data + "\n";
					error = StripEmptyLines (error);
					// log two lines at a time
					foreach (Match match in Regex.Matches (error, TWO_LINES, RegexOptions.Multiline)) {
						UnityEngine.Debug.LogError (PrefixOutput (match.Value));
						error = error.Substring (match.Value.Length);
					}
				}
			}
		);

		proc.Start ();
		proc.BeginOutputReadLine ();
		proc.BeginErrorReadLine ();
		proc.WaitForExit ();

		var exitCode = proc.ExitCode;

		// log any remaining output
		output = StripEmptyLines (output);
		if (output.Length > 0) {
			UnityEngine.Debug.Log (PrefixOutput (output));
		}

		// log any remaining error
		error = StripEmptyLines (error);
		if (error.Length > 0) {
			UnityEngine.Debug.LogError (PrefixOutput (error));
		}

		if (exitCode == 0) {
			UnityEngine.Debug.Log ("$ " + cmd + " " + joinedArgs + "\n==> OK");
		} else {
			UnityEngine.Debug.LogError ("$ " + cmd + " " + joinedArgs + "\n==> " + exitCode);
		}

		return exitCode;
	}

	static string StripEmptyLines (string output)
	{
		output = Regex.Replace (output, "^\n+", "", RegexOptions.Multiline);
		output = Regex.Replace (output, "\n+", "\n", RegexOptions.Multiline);
		return output;
	}

	static object PrefixOutput (string output)
	{
		return ">  " + Regex.Replace (output, "\n", "\n>  ", RegexOptions.Multiline);
	}

	// Since UnityEngine.Debug.ClearDeveloperConsole() doesn't work
	static void ClearLog ()
	{
		Assembly assembly = Assembly.GetAssembly (typeof(SceneView));
		Type type = assembly.GetType ("UnityEditorInternal.LogEntries");
		MethodInfo method = type.GetMethod ("Clear");
		method.Invoke (new object (), null);
	}

	static void CheckPasswords ()
	{
		if (PlayerSettings.keystorePass.Length == 0 || PlayerSettings.keyaliasPass.Length == 0) {
			string path = System.Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments) + "/.fred-build-info";
			string password = File.ReadAllText (path);
			PlayerSettings.keystorePass = password;
			PlayerSettings.keyaliasPass = password;
		}
	}

}
