using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class MyGazeRaycaster : BaseRaycaster
{
	const float MAX_DISTANCE = 1000f;

	public bool debugRaycast;
	public LayerMask layerMask;

	private Vector2 centerOfViewPort = new Vector2 (.5f, .5f);

	bool shouldRaycast ()
	{
		return VRMaster.instance.GAZE_ENABLED;
	}

	#region implemented abstract members of BaseRaycaster

	public override void Raycast (PointerEventData eventData, System.Collections.Generic.List<RaycastResult> resultAppendList)
	{
		if (!shouldRaycast ()) {
			return;
		}
		Ray ray = Camera.main.ViewportPointToRay (centerOfViewPort);
		RaycastHit hitInfo;
		Physics.Raycast (ray, out hitInfo, MAX_DISTANCE, layerMask);

		if (debugRaycast) {
			Debug.DrawRay (ray.origin, ray.direction * MAX_DISTANCE, hitInfo.collider == null ? Color.white : Color.green);
		}

		if (hitInfo.collider != null) {
			RaycastResult result = new RaycastResult ();

			result.Clear ();
			result.distance = hitInfo.distance;
			result.gameObject = hitInfo.collider.gameObject;
			result.module = this;
			result.screenPosition = new Vector2 (centerOfViewPort.x * Screen.width, centerOfViewPort.y * Screen.height);

			// Always zero for 3D objects
			// http://docs.unity3d.com/ScriptReference/EventSystems.RaycastResult-sortingLayer.html
			result.sortingLayer = 0;
			// http://docs.unity3d.com/ScriptReference/EventSystems.RaycastResult-sortingOrder.html
			result.sortingOrder = 0;

			result.worldNormal = hitInfo.normal;
			result.worldPosition = hitInfo.point;

			resultAppendList.Add (result);
		}
	}

	public override Camera eventCamera {
		get {
			return Camera.main;
		}
	}

	#endregion

}
