using UnityEngine;
using System.Collections;
using UnityEngine.VR;
using System;
using Valve.VR;
using UnityEngine.Assertions;
using UnityEngine.Analytics;
using System.Collections.Generic;
using UnityEngine.EventSystems;

#if UNITY_EDITOR && UNITY_ANDROID
using Gvr.Internal;
#endif

public class VRMaster : MonoBehaviour
{

	protected bool DEBUG = !false;

	const float PINCH_TO_ZOOM_RATE = 0.5f;
	#if UNITY_ANDROID
	const int TOGGLE_VR_MODE_TOUCH_COUNT = 4;
	#endif

	const string DEVICE_OPENVR = "OpenVR";
	const string DEVICE_DAYDREAM = "daydream";
	const string DEVICE_CARDBOARD = "cardboard";
	const string DEVICE_NONE = "";


	public static VRMaster instance { get; private set; }

	public VRInputModule vrInputModule;

	public string errorMessage;

	VRState _vrState;

	VRState vrState {
		get {
			return _vrState;
		}
		set {
			_vrState = value;
			AnnounceVRStateChange ();
		}
	}

	float _rotationAngle;

	float rotationAngle {
		get {
			return _rotationAngle;
		}
		set {
			Analytics.CustomEvent ("rotationAngle", new Dictionary<string, object> {
				{ "unscaledTime", Time.unscaledTime.ToString () },
				{ "angle", value },
			});
			_rotationAngle = value;
			Utils.SetPreferenceRecenterAngleY (_rotationAngle);
			AnnounceVRStateChange ();
		}
	}


	public bool VIVE {
		get { return _vrState == VRState.VIVE; }
		private set { }
	}

	public bool GVR_STEREO {
		get { return _vrState == VRState.GVR_STEREO; }
		private set { }
	}

	public bool MAGIC_WINDOW {
		get { return _vrState == VRState.MAGIC_WINDOW; }
		private set { }
	}

	public bool RETICLE_ENABLED {
		get { return _vrState == VRState.GVR_STEREO || _vrState == VRState.MAGIC_WINDOW; }
		private set { }
	}



	// vr state change events

	public delegate void VRStateChangeDelegate (VRState state, float rotationAngle);

	object VRStateChangeLock = new System.Object ();

	private event VRStateChangeDelegate _VRStateChange;

	public event VRStateChangeDelegate VRStateChange {
		add {
			lock (VRStateChangeLock) {
				_VRStateChange += value;
				value (vrState, Utils.GetPreferenceRecenterAngleY ());
			}
		}
		remove {
			lock (VRStateChangeLock) {
				_VRStateChange -= value;
			}
		}
	}

	//


	void Awake ()
	{
		if (instance != null && instance != this) {
			Destroy (gameObject);
			return;
		}
		instance = this;

		if (DEBUG) {
			Debug.Log ("Awake(): " + debugInfo);
		}

		_rotationAngle = Utils.GetPreferenceRecenterAngleY ();

		#if UNITY_STANDALONE_WIN
		// Assumes VRSettings.supportedDevices == ["", "OpenVR"]
		StartCoroutine (SwitchTo (DEVICE_OPENVR));
		#endif

		#if UNITY_ANDROID
		TeardownGVR ();
		// Assumes VRSettings.supportedDevices == ["daydream", "cardboard", ""]
		if (GvrSettings.ViewerPlatform == GvrSettings.ViewerPlatformType.Cardboard) {
			Debug.Log ("GvrSettings.ViewerPlatform==" + GvrSettings.ViewerPlatform + ", VRSettings.loadedDeviceName=\"" + VRSettings.loadedDeviceName + "\"");
			// VRSettings.loadedDeviceName: "cardboard" -> ""
			StartCoroutine (SwitchTo (DEVICE_NONE));
		} else {
			StartCoroutine (SwitchTo (BestSupportedGvrDeviceName ()));
		}
		#endif

		#if !UNITY_STANDALONE_WIN && !UNITY_ANDROID
		StartCoroutine (SwitchTo (DEVICE_NONE));
		#endif
	}

	void Update ()
	{
		if (InputMaster.FredKeyDown (GameKey._ToggleVrSupported)) {
			ToggleVR ();
		}
		if (Input.GetKeyDown (GameKey.Rotate90Degrees.keyCode)) {
			if (Input.GetKey (KeyCode.LeftShift) || Input.GetKey (KeyCode.RightShift)) {
				rotationAngle = 0f;
			} else {
				Rotate90Degrees ();
			}
		}
		#if UNITY_ANDROID
		// Escape key is sent on back button tap on Android
		if (GVR_STEREO && Input.GetKeyDown (KeyCode.Escape) && GvrSettings.ViewerPlatform == GvrSettings.ViewerPlatformType.Cardboard) {
			// VRSettings.loadedDeviceName: "" -> "cardboard"
			ToggleVR ();
		}
		if (Input.touchCount == TOGGLE_VR_MODE_TOUCH_COUNT && Input.GetTouch (TOGGLE_VR_MODE_TOUCH_COUNT - 1).phase == TouchPhase.Began) {
			ToggleVR ();
		}
		PinchToZoom ();
		#endif
	}

	// https://unity3d.com/learn/tutorials/topics/mobile-touch/pinch-zoom
	void PinchToZoom ()
	{
		if (Input.touchCount != 2) {
			return;
		}

		// Store both touches.
		Touch touch0 = Input.GetTouch (0);
		Touch touch1 = Input.GetTouch (1);

		// Find the position in the previous frame of each touch.
		Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
		Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

		// Find the magnitude of the vector (the distance) between the touches in each frame.
		float deltaPrevTouch = (touch0PrevPos - touch1PrevPos).magnitude;
		float deltaTouch = (touch0.position - touch1.position).magnitude;

		// Find the difference in the distances between each frame.
		float deltaDIff = deltaPrevTouch - deltaTouch;

		// Otherwise change the field of view based on the change in distance between the touches.
		Camera.main.fieldOfView += deltaDIff * PINCH_TO_ZOOM_RATE;

		// Clamp field of view to resonable range
		Camera.main.fieldOfView = Mathf.Clamp (Camera.main.fieldOfView, 20f, 90f);
	}

	public void ToggleVR ()
	{
		if (DEBUG) {
			Debug.Log ("ToggleVR(): " + debugInfo);
		}
		switch (vrState) {
		case VRState.MONOSCOPIC:
		case VRState.MAGIC_WINDOW:
#if UNITY_ANDROID
			if (Application.isMobilePlatform) {
				StartCoroutine (SwitchTo (BestSupportedGvrDeviceName ()));
			} else {
				//Debug.LogWarning ("VR not possible: " + Application.platform);
				GvrViewer.Instance.VRModeEnabled = !GvrViewer.Instance.VRModeEnabled;
			}
#elif UNITY_STANDALONE_WIN
			StartCoroutine (SwitchTo (DEVICE_OPENVR));
#endif
			break;
		case VRState.VIVE:
			StartCoroutine (SwitchTo (DEVICE_NONE));
			break;
		case VRState.GVR_STEREO:
			StartCoroutine (SwitchTo (DEVICE_NONE));
			break;
		default:
			throw new NotImplementedException ("vrState=" + vrState);
		}
	}

	IEnumerator SwitchTo (string desiredDeviceName)
	{
		vrState = VRState.SWITCHING;

		if (VRSettings.loadedDeviceName != desiredDeviceName) {
			if (desiredDeviceName == DEVICE_OPENVR) {
				try {
					if (!OpenVR.IsRuntimeInstalled ()) {
						SetFatalErrorUserMessage ("Make sure Steam and SteamVR are installed.");
						yield break;
					}
					if (!OpenVR.IsHmdPresent ()) {
						SetFatalErrorUserMessage ("Make sure your VR headset is plugged in and working correctly.");
						yield break;
					}
				} catch (Exception e) {
					SetFatalErrorUserMessage ("Unexpected OpenVR exception: " + e);
					yield break;
				}
			}
			if (DEBUG) {
				Debug.Log ("******* LoadDeviceByName(\"" + desiredDeviceName + "\")...");
			}
			VRSettings.LoadDeviceByName (desiredDeviceName);
			yield return new WaitForEndOfFrame (); // required to load device
			if (DEBUG) {
				Debug.Log ("SwitchTo(\"" + desiredDeviceName + "\"): " + debugInfo);
			}
			if (desiredDeviceName == DEVICE_OPENVR) {
				// throws exception if HMD not present
				OpenVR.IsHmdPresent ();
				OpenVR.IsRuntimeInstalled ();
			}
			if (VRSettings.loadedDeviceName != desiredDeviceName) {
				Debug.LogWarning ("Waiting an extra frame for VR device to load...");
				Analytics.CustomEvent ("LoadDeviceByName-WaitExtraFrame", new Dictionary<string,object> {
					{ "unscaledTime", Time.unscaledTime.ToString () },
					{ "loadedDeviceName", VRSettings.loadedDeviceName },
					{ "desiredDeviceName", desiredDeviceName },
					#if UNITY_STANDALONE_WIN
					{ "OpenVRState", GetOpenVRState () },
					#endif
				});
				yield return new WaitForEndOfFrame (); // second wait needed if Coroutine() started from Update()
				if (DEBUG) {
					Debug.Log ("SwitchTo(\"" + desiredDeviceName + "\"): " + debugInfo);
				}
			}
			if (VRSettings.loadedDeviceName != desiredDeviceName) {
				Analytics.CustomEvent ("LoadDeviceByName-FailedToLoad", new Dictionary<string,object> {
					{ "unscaledTime", Time.unscaledTime.ToString () },
					{ "loadedDeviceName", VRSettings.loadedDeviceName },
					{ "desiredDeviceName", desiredDeviceName },
					#if UNITY_STANDALONE_WIN
					{ "OpenVRState", GetOpenVRState () },
					#endif
				});
				SetFatalErrorUserMessage ("Failed to load VR device " + desiredDeviceName + " after waiting an extra frame");
				yield break;
//				Application.Quit ();
//				throw new Exception ("Failed to load VR device " + desiredDeviceName);
			}
		}

		bool shouldEnable = desiredDeviceName != DEVICE_NONE;
		if (VRSettings.enabled != shouldEnable) {
			if (DEBUG) {
				Debug.Log ("******* VRSettings.enabled = " + shouldEnable);
			}
			VRSettings.enabled = shouldEnable;
			if (VRSettings.enabled != shouldEnable) {
				Debug.LogWarning ("Waiting an extra frame for VRSettings.enabled = " + shouldEnable);
				yield return new WaitForEndOfFrame (); // required for None device to become active
			}
			if (VRSettings.enabled != shouldEnable) {
				SetFatalErrorUserMessage ("`VRSettings.enabled != " + shouldEnable + "` after waiting an extra frame");
				yield break;
//				Application.Quit ();
//				throw new Exception ("VRSettings.enabled != " + shouldEnable);
			}
			if (DEBUG) {
				Debug.Log ("SwitchTo(\"" + desiredDeviceName + "\"): " + debugInfo);
			}
		}

		if (desiredDeviceName == DEVICE_OPENVR) {
			// Referencing SteamVR_Render.instance creates [SteamVR] game object and SteamVR_Render component
			if (SteamVR_Render.instance == null) {
				Debug.LogWarning ("Waiting an extra frame for SteamVR_Render.instance != null");
				yield return new WaitForEndOfFrame ();
			}
			if (SteamVR_Render.instance == null) {
				SetFatalErrorUserMessage ("`SteamVR_Render.instance == null` after waiting an extra frame");
				yield break;
			}
		}

		#if UNITY_STANDALONE
		if (desiredDeviceName != DEVICE_OPENVR) {
			TeardownOpenVR ();
		}
		#endif

		if (desiredDeviceName != DEVICE_DAYDREAM && desiredDeviceName != DEVICE_CARDBOARD) {
			TeardownGVR ();
		}

		#if UNITY_ANDROID
		vrInputModule.rayTransform = Camera.main.transform;
		#endif

		if (Application.isEditor || desiredDeviceName == DEVICE_DAYDREAM || desiredDeviceName == DEVICE_CARDBOARD) {
			SetupGVRController ();
		}

		if (DEBUG) {
			Debug.Log ("******* Camera.main.ResetFieldOfView() / Camera.main.ResetAspect ()");
		}
		Camera.main.ResetFieldOfView ();
		Camera.main.ResetAspect ();
		FixRenderScale ();

		SetAndAnnounceVrState ();
	}

	void TeardownGVR ()
	{
		foreach (GvrViewer gvrViewer in GameObject.FindObjectsOfType<GvrViewer>()) {
			Destroy (gvrViewer.gameObject);
		}
	}

	void SetupGVRController ()
	{
		Debug.Log ("SetupGVRController()");
		GvrViewer.Create ();
		#if UNITY_ANDROID && UNITY_EDITOR
		GvrViewer.Instance.VRModeEnabled = false;
		GvrViewer.Instance.ScreenSize = GvrProfile.ScreenSizes.Nexus6;
		GvrViewer.Instance.gameObject.AddComponent<EmulatorConfig> ();
		#endif
		#if UNITY_ANDROID
		GvrViewer.Instance.gameObject.AddComponent<GvrController> ();
		#endif
	}

	void TeardownOpenVR ()
	{
		SteamVR_Render steamVR_RenderInstance = SteamVR_Render.instance;

		if (steamVR_RenderInstance != null) {
			if (DEBUG) {
				Debug.Log ("******* Destroy (steamVR_RenderInstance.gameObject)");
			}
			Destroy (steamVR_RenderInstance.gameObject);
		}

		if (SteamVR.active) {
			Debug.LogError ("SteamVR.active");
			if (DEBUG) {
				Debug.Log ("******* SteamVR.SafeDispose()");
			}
			SteamVR.SafeDispose ();
		}

		if (DEBUG) {
			Debug.Log ("******* SteamVR.enabled = false");
		}
		SteamVR.enabled = false;

		if (DEBUG) {
			Debug.Log ("ShutdownOpenVR(): " + debugInfo);
		}
	}

	string BestSupportedGvrDeviceName ()
	{
		if (Application.isEditor) {
			return DEVICE_NONE;
		}
		foreach (string deviceName in VRSettings.supportedDevices) {
			if (deviceName == DEVICE_DAYDREAM) {
				return deviceName;
			}
			if (deviceName == DEVICE_CARDBOARD) {
				return deviceName;
			}
		}
		throw new NotImplementedException ("VRSettings.supportedDevices=" + VRSettings.supportedDevices);
	}

	void SetAndAnnounceVrState ()
	{
		// cause new state to be announced
		vrState = GetVrState ();
	}

	VRState GetVrState ()
	{
		switch (VRSettings.loadedDeviceName) {
		case DEVICE_OPENVR:
			return VRState.VIVE;
		case DEVICE_DAYDREAM:
		case DEVICE_CARDBOARD:
			return VRState.GVR_STEREO;
		case DEVICE_NONE:
#if UNITY_ANDROID
			return VRState.MAGIC_WINDOW;
#else
			return VRState.MONOSCOPIC;
#endif
		default:
			throw new NotImplementedException ("VRSettings.loadedDeviceName=" + VRSettings.loadedDeviceName);
		}
	}

	void AnnounceVRStateChange ()
	{
		Debug.Log ("_vrState -> " + _vrState + " @ " + rotationAngle);
		Analytics.CustomEvent ("VRStateChange", new Dictionary<string, object> {
			{ "unscaledTime", Time.unscaledTime.ToString () },
			{ "_vrState", _vrState.ToString () },
		});
		if (_VRStateChange != null) {
			_VRStateChange (_vrState, Utils.GetPreferenceRecenterAngleY ());
		}
	}

	public string debugInfo {
		get {
			string t = "VRSettings{enabled=" + VRSettings.enabled + ",loadedDeviceName=" + VRSettings.loadedDeviceName + ",supportedDevices=[" + String.Join (", ", VRSettings.supportedDevices) + "]},\n" +
			           "VRDevice{isPresent=" + VRDevice.isPresent + ",model=" + VRDevice.model + ",refreshRate=" + VRDevice.refreshRate + "}\n";
			#if UNITY_STANDALONE
			t +=
				"SteamVR{active=" + SteamVR.active + ",usingNativeSupport=" + SteamVR.usingNativeSupport + "},\n" +
			"FindObjectOfType<SteamVR_Render>()=" + GameObject.FindObjectOfType<SteamVR_Render> () + "},\n" +
			"OpenVR{" +	GetOpenVRState () + "}\n";
			#endif
			return t;
		}
	}

	string GetOpenVRState ()
	{
		try {
			return "IsHmdPresent()=" + OpenVR.IsHmdPresent () + ",IsRuntimeInstalled()=" + OpenVR.IsRuntimeInstalled ();
		} catch (Exception e) {
			return e.ToString ();
		}
	}

	// invoked from cardboard icon
	public void CarboardIconTapped ()
	{
		ToggleVR ();
	}

	void FixRenderScale ()
	{
		#if UNITY_ANDROID
		float oldRenderScale = VRSettings.renderScale;
		VRSettings.renderScale = .6f; // GVR defaults to .7f
		if (DEBUG) {
			Debug.Log ("******* VRSettings.renderScale = " + oldRenderScale.ToString (".00000") + " -> " + VRSettings.renderScale.ToString (".00000"));
		}
		#endif
	}

	void SetFatalErrorUserMessage (string errorReason)
	{
		Debug.LogError (errorReason);
		if (errorReason != errorMessage && (errorReason == "" || errorMessage == "")) {
			if (errorReason != "") {
				Analytics.CustomEvent ("VRMaster-FatalError", new Dictionary<string, object> {
					{ "unscaledTime", Time.unscaledTime.ToString () },
					{ "errorReason", errorReason },
				});
			}
			errorMessage = errorReason;
		}
		_vrState = VRState.ERROR;
	}

	// invoked from menu room setup button
	public void Rotate90Degrees ()
	{
		rotationAngle = Mathf.RoundToInt ((Utils.GetPreferenceRecenterAngleY () + 90f) / 90f) * 90f % 360f;
	}
}
