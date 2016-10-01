using UnityEngine;
using System.Collections;

// adapter from https://gist.github.com/flarb/3252857
using System;


public class AndroidUtils : MonoBehaviour
{

	const int ROTATION_0 = 0x00000000;
	const int ROTATION_180 = 0x00000002;
	const int ROTATION_270 = 0x00000003;
	const int ROTATION_90 = 0x00000001;

	static AndroidJavaObject mConfig;
	static AndroidJavaObject mWindowManager;

	public static Quaternion GetDisplayRotationFix ()
	{
		int rotation = GetDisplayRotationDegrees ();
		return Quaternion.Euler (0f, 0f, -rotation);
	}

	public static int GetDisplayRotationDegrees ()
	{
		if (!Application.isMobilePlatform) {
			return 0;
		}

		if ((mWindowManager == null) || (mConfig == null)) {
			using (AndroidJavaObject activity = new AndroidJavaClass ("com.unity3d.player.UnityPlayer").
				GetStatic<AndroidJavaObject> ("currentActivity")) {
				mWindowManager = activity.Call<AndroidJavaObject> ("getSystemService", "window");
				mConfig = activity.Call<AndroidJavaObject> ("getResources").Call<AndroidJavaObject> ("getConfiguration");
			}
		}

		int lRotation = mWindowManager.Call<AndroidJavaObject> ("getDefaultDisplay").Call<int> ("getRotation");

		switch (lRotation) {
		case ROTATION_0:
			return 0;
		case ROTATION_90:
			return 90;
		case ROTATION_180:
			return 180;
		case ROTATION_270:
			return 270;
		default:
			throw new NotImplementedException ();
		}
	}
}
