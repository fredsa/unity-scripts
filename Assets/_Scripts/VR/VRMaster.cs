using UnityEngine;
using System.Collections;
using UnityEngine.VR;
using System;
using Valve.VR;
using UnityEngine.Assertions;
using UnityEngine.Analytics;
using System.Collections.Generic;

#if UNITY_EDITOR
using Gvr.Internal;
#endif

public class VRMaster : MonoBehaviour
{

	protected bool DEBUG = false;

	const float PINCH_TO_ZOOM_RATE = 0.5f;

	const string DEVICE_OPENVR = "OpenVR";
	const string DEVICE_DAYDREAM = "daydream";
	const string DEVICE_CARDBOARD = "cardboard";
	const string DEVICE_NONE = "";


	public static VRMaster instance { get; private set; }

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

	public delegate void VRStateChangeDelegate (VRState state);

	object VRStateChangeLock = new System.Object ();

	private event VRStateChangeDelegate _VRStateChange;

	public event VRStateChangeDelegate VRStateChange {
		add {
			lock (VRStateChangeLock) {
				_VRStateChange += value;
				value (vrState);
			}
		}
		remove {
			lock (VRStateChangeLock) {
				_VRStateChange -= value;
			}
		}
	}



	void Awake ()
	{
		if (instance != null && instance != this) {
			Destroy (gameObject);
			return;
		}
		instance = this;

		if (DEBUG) {
			Debug.Log ("Awake(): " + state);
		}

		#if UNITY_ANDROID && UNITY_EDITOR
		TeardownGVR ();
		SetupGVRController ();
		#endif
		_vrState = GetVrState ();
		if (DEBUG) {
			Debug.Log ("Awake(): _vrState -> " + _vrState);
		}

		SetAndAnnounceVrState ();

		#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_STANDALONE_WIN
		if (VRSettings.loadedDeviceName == DEVICE_NONE) {
			ToggleVR ();
		}
		#endif

	}

	void Update ()
	{
		if (Utils.FredKeyDown (GameKey._ToggleVrSupported)) {
			ToggleVR ();
		}
		#if UNITY_ANDROID
		if (GVR_STEREO && Input.GetKeyDown (GameKey._Escape.keyCode)) {
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

	bool SafeIsHmdPresent ()
	{
		try {
			if (OpenVR.IsHmdPresent ()) {
				return true;
			} else {
				return false;
			}
		} catch (Exception /*ignored*/) {
			return false;
		}
	}

	public void ToggleVR ()
	{
		if (DEBUG) {
			Debug.Log ("ToggleVR(): " + state);
		}
		switch (vrState) {
		case VRState.MONOSCOPIC:
		case VRState.MAGIC_WINDOW:
#if UNITY_ANDROID
			if (Application.isMobilePlatform) {
				StartCoroutine (SwitchTo (BestSupportedGvrDeviceName ()));
			} else {
				Debug.LogWarning ("VR not possible: " + Application.platform);
			}
#elif UNITY_STANDALONE_WIN
			if (SafeIsHmdPresent ()) {
				StartCoroutine (SwitchTo (DEVICE_OPENVR));
			} else {
				Debug.LogWarning ("VR not possible: HMD not present");
			}
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
			if (DEBUG) {
				Debug.Log ("******* LoadDeviceByName(\"" + desiredDeviceName + "\")...");
			}
			VRSettings.LoadDeviceByName (desiredDeviceName);
			yield return new WaitForEndOfFrame (); // required to load device
			if (DEBUG) {
				Debug.Log ("SwitchToGVR(\"" + desiredDeviceName + "\"): " + state);
			}
			while (VRSettings.loadedDeviceName != desiredDeviceName) {
				Debug.LogWarning ("Waiting an extra frame for VR device to load...");
				yield return new WaitForEndOfFrame (); // second wait needed if Coroutine() started from Update()
				if (DEBUG) {
					Debug.Log ("SwitchToGVR(\"" + desiredDeviceName + "\"): " + state);
				}
			}
		}

		bool shouldEnable = desiredDeviceName != DEVICE_NONE;
		if (VRSettings.enabled != shouldEnable) {
			if (DEBUG) {
				Debug.Log ("******* VRSettings.enabled = " + shouldEnable);
			}
			VRSettings.enabled = shouldEnable;
			while (VRSettings.enabled != shouldEnable) {
				Debug.LogWarning ("Waiting an extra frame for VRSettings.enabled = " + shouldEnable);
				yield return new WaitForEndOfFrame (); // required for None device to become active
			}
			if (DEBUG) {
				Debug.Log ("SwitchToGVR(\"" + desiredDeviceName + "\"): " + state);
			}
		}

		#if UNITY_STANDALONE
		if (desiredDeviceName != DEVICE_OPENVR) {
			ShutdownOpenVR ();
		}
		#endif

		if (desiredDeviceName != DEVICE_DAYDREAM && desiredDeviceName != DEVICE_CARDBOARD) {
			TeardownGVR ();
		}

		if (desiredDeviceName == DEVICE_DAYDREAM) {
			SetupGVRController ();
		}

		if (DEBUG) {
			Debug.Log ("******* Camera.main.ResetFieldOfView() / Camera.main.ResetAspect ()");
		}
		Camera.main.ResetFieldOfView ();
		Camera.main.ResetAspect ();

		SetAndAnnounceVrState ();
	}

	void TeardownGVR ()
	{
		foreach (GvrController gvrController in GameObject.FindObjectsOfType<GvrController>()) {
			Debug.LogWarning ("TeardownGVR(): - gvrController=" + gvrController);
			Destroy (gvrController.gameObject);
		}
		#if UNITY_EDITOR
		foreach (EmulatorConfig emulatorconfig in GameObject.FindObjectsOfType<EmulatorConfig>()) {
			Debug.LogWarning ("TeardownGVR(): - emulatorconfig=" + emulatorconfig);
			Destroy (emulatorconfig.gameObject);
		}
		#endif
		#if !UNITY_HAS_GOOGLEVR || UNITY_EDITOR
		foreach (GvrPreRender gvrPreRender in GameObject.FindObjectsOfType<GvrPreRender>()) {
			Debug.LogWarning ("TeardownGVR(): - gvrPreRender=" + gvrPreRender);
			Destroy (gvrPreRender.gameObject);
		}
		foreach (GvrPostRender gvrPostRender in GameObject.FindObjectsOfType<GvrPostRender>()) {
			Debug.LogWarning ("TeardownGVR(): - gvrPostRender=" + gvrPostRender);
			Destroy (gvrPostRender.gameObject);
		}
		#endif
		foreach (GvrHead gvrHead in GameObject.FindObjectsOfType<GvrHead>()) {
			Debug.LogWarning ("TeardownGVR(): - gvrHead=" + gvrHead);
			Destroy (gvrHead.gameObject);
		}
		foreach (GvrEye gvrEye in GameObject.FindObjectsOfType<GvrEye>()) {
			Debug.LogWarning ("TeardownGVR(): - gvrEye=" + gvrEye);
			Destroy (gvrEye.gameObject);
		}
		foreach (StereoController stereoController in GameObject.FindObjectsOfType<StereoController>()) {
			Debug.LogWarning ("TeardownGVR(): - stereoController=" + stereoController);
			Destroy (stereoController.gameObject);
		}
		foreach (GvrViewer gvrViewer in GameObject.FindObjectsOfType<GvrViewer>()) {
			Debug.LogWarning ("TeardownGVR(): gvrViewer=" + gvrViewer);
			Destroy (gvrViewer.gameObject);
		}
	}

	void SetupGVRController ()
	{
		Debug.Log ("SetupGVRController()");
		GvrViewer.Create ();
		#if UNITY_EDITOR
		GvrViewer.Instance.VRModeEnabled = false;
		#endif
		GvrViewer.Instance.gameObject.AddComponent<GvrController> ();
		#if UNITY_EDITOR
		GvrViewer.Instance.gameObject.AddComponent<EmulatorConfig> ();
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
			Debug.Log ("ShutdownOpenVR(): " + state);
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
		Debug.Log ("_vrState -> " + _vrState);
		Analytics.CustomEvent ("VRStateChange", new Dictionary<string, object> {
			{ "unscaledTime", Time.unscaledTime.ToString () },
			{ "_vrState", _vrState.ToString () },
		});
		if (_VRStateChange != null) {
			_VRStateChange (_vrState);
		}
	}

	string state {
		get {
			string t = "VRSettings(enabled=" + VRSettings.enabled + ",loadedDeviceName=" + VRSettings.loadedDeviceName + ",supportedDevices=[" + String.Join (", ", VRSettings.supportedDevices) + "]), " +
			           "VRDevice(isPresent=" + VRDevice.isPresent + ",model=" + VRDevice.model + ",refreshRate=" + VRDevice.refreshRate + ")";
			#if UNITY_STANDALONE
			t += "\n" +
			"SteamVR(active=" + SteamVR.active + ",usingNativeSupport=" + SteamVR.usingNativeSupport + ")," +
			"FindObjectOfType<SteamVR_Render>()=" + GameObject.FindObjectOfType<SteamVR_Render> () + ",";
			try {
				t += "OpenVR(IsHmdPresent()=" + OpenVR.IsHmdPresent () + ",IsRuntimeInstalled()=" + OpenVR.IsRuntimeInstalled () + ")";
			} catch (Exception e) {
				t += "OpenVR(" + e + ")";
			}
			#endif
			return t;
		}
	}

	// invoked from cardboard icon
	public void CarboardIconTapped ()
	{
		ToggleVR ();
	}

}
