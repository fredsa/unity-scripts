using UnityEngine;

public interface IReticlePointer
{
	void OnReticlePointerEnabled ();

	void OnReticlePointerDisabled ();

	void OnReticlePointerStart (Camera camera, GameObject targetObject, Vector3 intersectionPosition,
	                            bool isInteractive);

	void OnReticlePointerStay (Camera camera, GameObject targetObject, Vector3 intersectionPosition,
	                           bool isInteractive);

	void OnReticlePointerExit (Camera camera, GameObject targetObject);

	void OnReticlePointerTriggerStart (Camera camera);

	void OnReticlePointerTriggerEnd (Camera camera);

	void GetPointerRadius (out float innerRadius, out float outerRadius);
}
