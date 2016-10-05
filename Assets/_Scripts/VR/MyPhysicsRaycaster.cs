using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MyPhysicsRaycaster : PhysicsRaycaster
{

	public bool debugRaycast;

	VRState vrState;

	protected override void OnEnable ()
	{
		base.OnEnable ();
		VRMaster.instance.VRStateChange += OnVRStateChange;
	}

	protected override void OnDisable ()
	{
		base.OnDisable ();
		VRMaster.instance.VRStateChange -= OnVRStateChange;
	}

	void OnVRStateChange (VRState vrState)
	{
		this.vrState = vrState;
	}

	bool shouldRaycast ()
	{
		return vrState == VRState.MONOSCOPIC || (vrState == VRState.MAGIC_WINDOW && !Application.isEditor);
	}

	public override void Raycast (PointerEventData eventData, System.Collections.Generic.List<RaycastResult> resultAppendList)
	{
		if (!shouldRaycast ()) {
			return;
		}
		List<RaycastResult> newList = new List<RaycastResult> ();
		base.Raycast (eventData, newList);
		if (debugRaycast) {
//			Debug.DrawRay (eventCamera.transform.position, eventCamera.transform.forward * 100f, newList.Count > 0 ? Color.green : Color.red);
			foreach (RaycastResult r in newList) {
				Debug.DrawRay (r.worldPosition, r.worldNormal * 100f, Color.blue);
			}
		}
		resultAppendList.AddRange (newList);
	}

}
