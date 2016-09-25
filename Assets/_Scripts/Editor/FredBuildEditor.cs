using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Reflection;
using System;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using System.ComponentModel;
using UnityEngine.VR;
using System.Collections;

[InitializeOnLoad]
public class FredBuildEditor : EditorWindow
{
	const string TWO_LINES = ".*\n.*\n";

	static bool executing = false;
	static string projectDirectory;

	static FredBuildEditor ()
	{
#if UNITY_ANDROID
		CheckPasswords ();
#endif
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
		string[] paths = new string[SceneManager.sceneCount];
		for (int i = 0; i < SceneManager.sceneCount; i++) {
			Scene scene = SceneManager.GetSceneAt (i);
			if (scene.path.Length == 0) {
				throw new Exception ("Unsaved scene " + i + " (path is empty).");
			}
			paths [i] = scene.path;
			UnityEngine.Debug.Log ("- Scene " + i + ": " + paths [i] + "\n");
		}
		return paths;
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



		projectDirectory = new System.IO.DirectoryInfo (Application.dataPath).Parent.FullName;
		new Thread (new ThreadStart (InstallApk)).Start ();
	}

	static void InstallApk ()
	{
		executing = true;

		UnityEngine.Debug.Log ("$ ./reinstall.sh");
#if UNITY_EDITOR_WIN
		Execute ("\"C:\\Program Files\\Git\\git-bash.exe\"", "-lc", "pwd;./reinstall.sh;read");
#else
		Execute ("/bin/bash", "-lc", "./reinstall.sh");
#endif
		executing = false;
	}

	static void Execute (string cmd, params string[] args)
	{
		string joinedArgs = string.Join (" ", args);
		Process proc = new Process ();
		proc.StartInfo.WorkingDirectory = projectDirectory;
		proc.StartInfo.UseShellExecute = false;
		proc.StartInfo.CreateNoWindow = !true;
		proc.StartInfo.ErrorDialog = !false;
		proc.StartInfo.RedirectStandardOutput = true;
		proc.StartInfo.RedirectStandardError = true;
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

		try {
			proc.Start ();
		} catch (Win32Exception e) {
			// https://msdn.microsoft.com/en-us/library/e8zac0ca(v=vs.110).aspx
			UnityEngine.Debug.LogError ("proc.Start() exception: There was an error in opening the associated file\n" + e);
			return;
		} catch (Exception e) {
			UnityEngine.Debug.LogError ("proc.Start() exception: " + e);
			return;
		}
		proc.BeginOutputReadLine ();
		proc.BeginErrorReadLine ();

		proc.Exited += (object sender, EventArgs e) => {
			int exitCode = proc.ExitCode;

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
		};
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

	static void ClearLog ()
	{
		// Since UnityEngine.Debug.ClearDeveloperConsole() doesn't work
		// UnityEngine.Debug.ClearDeveloperConsole();
		Assembly assembly = Assembly.GetAssembly (typeof(SceneView));
		Type type = assembly.GetType ("UnityEditorInternal.LogEntries");
		MethodInfo method = type.GetMethod ("Clear");
		method.Invoke (new UnityEngine.Object (), null);
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
