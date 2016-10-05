using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR)
using UnityEngine.VR;
#endif  // UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR)

public class MyReticleInputModule : BaseInputModule
{

	public static IReticlePointer reticlePointer;

	private PointerEventData pointerData;
	private Vector2 lastHeadPose;

	private bool isActive = false;

	/// Time in seconds between the pointer down and up events sent by a trigger.
	/// Allows time for the UI elements to make their state transitions.
	private const float clickTime = 0.1f;
	// Based on default time for a button to animate to Pressed.

	/// @cond
	public override bool ShouldActivateModule ()
	{
		bool activeState = base.ShouldActivateModule ();

		activeState = activeState && VRMaster.instance.RETICLE_ENABLED;

		if (activeState != isActive) {
			isActive = activeState;

			// Activate reticle pointer
			if (reticlePointer != null) {
				if (isActive) {
					reticlePointer.OnReticlePointerEnabled ();
				}
			}
		}

		return activeState;
	}

	/// @endcond

	public override void DeactivateModule ()
	{
		DisableGazePointer ();
		base.DeactivateModule ();
		if (pointerData != null) {
			HandlePendingClick ();
			HandlePointerExitAndEnter (pointerData, null);
			pointerData = null;
		}
		eventSystem.SetSelectedGameObject (null, GetBaseEventData ());
	}

	public override bool IsPointerOverGameObject (int pointerId)
	{
		return pointerData != null && pointerData.pointerEnter != null;
	}

	public override void Process ()
	{
		// Save the previous Game Object
		GameObject gazeObjectPrevious = GetCurrentGameObject ();

		CastRayForReticle ();
		UpdateCurrentObject ();
		UpdateReticle (gazeObjectPrevious);

		bool handlePendingClickRequired = !Input.GetButton ("Fire1");
		;
		#if UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR)
		handlePendingClickRequired &= !GvrController.ClickButton;
		#endif  // UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR)

		// Handle input
		if (!Input.GetButtonDown ("Fire1") && Input.GetButton ("Fire1")) {
			HandleDrag ();
		} else if (Time.unscaledTime - pointerData.clickTime < clickTime) {
			// Delay new events until clickTime has passed.
		} else if (!pointerData.eligibleForClick && (Input.GetButtonDown ("Fire1") || GvrController.ClickButtonDown)) {
			// New trigger action.
			HandleTrigger ();
		} else if (handlePendingClickRequired) {
			// Check if there is a pending click to handle.
			HandlePendingClick ();
		}
	}

	/// @endcond

	private void CastRayForReticle ()
	{
		Vector2 headPose = NormalizedCartesianToSpherical (Camera.main.transform.rotation * Vector3.forward);

		if (pointerData == null) {
			pointerData = new PointerEventData (eventSystem);
			lastHeadPose = headPose;
		}

		// Cast a ray into the scene
		pointerData.Reset ();
		pointerData.position = GetReticlePointerPosition ();
		eventSystem.RaycastAll (pointerData, m_RaycastResultCache);
		pointerData.pointerCurrentRaycast = FindFirstRaycast (m_RaycastResultCache);
		m_RaycastResultCache.Clear ();
		pointerData.delta = headPose - lastHeadPose;
		lastHeadPose = headPose;
	}

	private void UpdateCurrentObject ()
	{
		// Send enter events and update the highlight.
		var go = pointerData.pointerCurrentRaycast.gameObject;
		HandlePointerExitAndEnter (pointerData, go);
		// Update the current selection, or clear if it is no longer the current object.
		var selected = ExecuteEvents.GetEventHandler<ISelectHandler> (go);
		if (selected == eventSystem.currentSelectedGameObject) {
			ExecuteEvents.Execute (eventSystem.currentSelectedGameObject, GetBaseEventData (),
				ExecuteEvents.updateSelectedHandler);
		} else {
			eventSystem.SetSelectedGameObject (null, pointerData);
		}
	}

	void UpdateReticle (GameObject previousGazedObject)
	{
		if (reticlePointer == null) {
			return;
		}

		Camera camera = pointerData.enterEventCamera; // Get the camera
		GameObject gazeObject = GetCurrentGameObject (); // Get the gaze target
		Vector3 intersectionPosition = GetIntersectionPosition ();
		bool isInteractive = pointerData.pointerPress != null ||
		                     ExecuteEvents.GetEventHandler<IPointerClickHandler> (gazeObject) != null;

		if (gazeObject == previousGazedObject) {
			if (gazeObject != null) {
				reticlePointer.OnReticlePointerStay (camera, gazeObject, intersectionPosition, isInteractive);
			}
		} else {
			if (previousGazedObject != null) {
				reticlePointer.OnReticlePointerExit (camera, previousGazedObject);
			}

			if (gazeObject != null) {
				reticlePointer.OnReticlePointerStart (camera, gazeObject, intersectionPosition, isInteractive);
			}
		}
	}

	private void HandleDrag ()
	{
		bool moving = pointerData.IsPointerMoving ();

		if (moving && pointerData.pointerDrag != null && !pointerData.dragging) {
			ExecuteEvents.Execute (pointerData.pointerDrag, pointerData,
				ExecuteEvents.beginDragHandler);
			pointerData.dragging = true;
		}

		// Drag notification
		if (pointerData.dragging && moving && pointerData.pointerDrag != null) {
			// Before doing drag we should cancel any pointer down state
			// And clear selection!
			if (pointerData.pointerPress != pointerData.pointerDrag) {
				ExecuteEvents.Execute (pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);

				pointerData.eligibleForClick = false;
				pointerData.pointerPress = null;
				pointerData.rawPointerPress = null;
			}
			ExecuteEvents.Execute (pointerData.pointerDrag, pointerData, ExecuteEvents.dragHandler);
		}
	}

	private void HandlePendingClick ()
	{
		if (!pointerData.eligibleForClick && !pointerData.dragging) {
			return;
		}

		if (reticlePointer != null) {
			Camera camera = pointerData.enterEventCamera;
			reticlePointer.OnReticlePointerTriggerEnd (camera);
		}

		var go = pointerData.pointerCurrentRaycast.gameObject;

		// Send pointer up and click events.
		ExecuteEvents.Execute (pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);
		if (pointerData.eligibleForClick) {
			ExecuteEvents.Execute (pointerData.pointerPress, pointerData, ExecuteEvents.pointerClickHandler);
		} else if (pointerData.dragging) {
			ExecuteEvents.ExecuteHierarchy (go, pointerData, ExecuteEvents.dropHandler);
			ExecuteEvents.Execute (pointerData.pointerDrag, pointerData, ExecuteEvents.endDragHandler);
		}

		// Clear the click state.
		pointerData.pointerPress = null;
		pointerData.rawPointerPress = null;
		pointerData.eligibleForClick = false;
		pointerData.clickCount = 0;
		pointerData.clickTime = 0;
		pointerData.pointerDrag = null;
		pointerData.dragging = false;
	}

	private void HandleTrigger ()
	{
		var go = pointerData.pointerCurrentRaycast.gameObject;

		// Send pointer down event.
		pointerData.pressPosition = pointerData.position;
		pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
		pointerData.pointerPress =
			ExecuteEvents.ExecuteHierarchy (go, pointerData, ExecuteEvents.pointerDownHandler)
		?? ExecuteEvents.GetEventHandler<IPointerClickHandler> (go);

		// Save the drag handler as well
		pointerData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler> (go);
		if (pointerData.pointerDrag != null) {
			ExecuteEvents.Execute (pointerData.pointerDrag, pointerData, ExecuteEvents.initializePotentialDrag);
		}

		// Save the pending click state.
		pointerData.rawPointerPress = go;
		pointerData.eligibleForClick = true;
		pointerData.delta = Vector2.zero;
		pointerData.dragging = false;
		pointerData.useDragThreshold = true;
		pointerData.clickCount = 1;
		pointerData.clickTime = Time.unscaledTime;

		if (reticlePointer != null) {
			reticlePointer.OnReticlePointerTriggerStart (pointerData.enterEventCamera);
		}
	}

	private Vector2 NormalizedCartesianToSpherical (Vector3 cartCoords)
	{
		cartCoords.Normalize ();
		if (cartCoords.x == 0)
			cartCoords.x = Mathf.Epsilon;
		float outPolar = Mathf.Atan (cartCoords.z / cartCoords.x);
		if (cartCoords.x < 0)
			outPolar += Mathf.PI;
		float outElevation = Mathf.Asin (cartCoords.y);
		return new Vector2 (outPolar, outElevation);
	}

	GameObject GetCurrentGameObject ()
	{
		if (pointerData != null && pointerData.enterEventCamera != null) {
			return pointerData.pointerCurrentRaycast.gameObject;
		}

		return null;
	}

	Vector3 GetIntersectionPosition ()
	{
		// Check for camera
		Camera cam = pointerData.enterEventCamera;
		if (cam == null) {
			return Vector3.zero;
		}

		float intersectionDistance = pointerData.pointerCurrentRaycast.distance + cam.nearClipPlane;
		Vector3 intersectionPosition = cam.transform.position + cam.transform.forward * intersectionDistance;

		return intersectionPosition;
	}

	void DisableGazePointer ()
	{
		if (reticlePointer == null) {
			return;
		}

		GameObject currentGameObject = GetCurrentGameObject ();
		if (currentGameObject) {
			Camera camera = pointerData.enterEventCamera;
			reticlePointer.OnReticlePointerExit (camera, currentGameObject);
		}

		reticlePointer.OnReticlePointerDisabled ();
	}

	private Vector2 GetReticlePointerPosition ()
	{
		int viewportWidth = Screen.width;
		int viewportHeight = Screen.height;
		#if UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR) && UNITY_ANDROID
		// GVR native integration is supported.
		if (VRSettings.enabled) {
			viewportWidth = VRSettings.eyeTextureWidth;
			viewportHeight = VRSettings.eyeTextureHeight;
		}
		#endif  // UNITY_HAS_GOOGLEVR && (UNITY_ANDROID || UNITY_EDITOR) && UNITY_ANDROID

		return new Vector2 (0.5f * viewportWidth, 0.5f * viewportHeight);
	}
}

