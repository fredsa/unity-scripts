using UnityEngine;
using System;

/// Draws a circular reticle in front of any object that the user gazes at.
/// The circle dilates if the object is clickable.
[RequireComponent (typeof(Renderer))]
public class MyReticle : MonoBehaviour, IReticlePointer
{

	// Minimum distance of the reticle (in meters).
	private const float reticleMinDistance = 0.45f;

	// Maximum distance of the reticle (in meters).
	private float reticleMaxDistance = 10.0f;

	// Minimum inner angle of the reticle (in degrees).
	private float reticleMinInnerAngle;

	// Minimum outer angle of the reticle (in degrees).
	private float reticleMinOuterAngle;


	public enum ReticleType
	{
		INTERACTIVE,
		TINY_CIRCLE,
		LARGE_CIRCLE,
		UNKNOWN,
	}

	// Current inner angle of the reticle (in degrees).
	private float _reticleInnerAngle;

	// Current outer angle of the reticle (in degrees).
	private float _reticleOuterAngle;

	// Angle at which to expand the reticle when intersecting with an object
	// (in degrees).
	private float _reticleGrowthAngle;

	private ReticleType reticleType {
		set {
			switch (value) {
			case ReticleType.INTERACTIVE:
			case ReticleType.UNKNOWN:
				reticleMinInnerAngle = .5f;
				reticleMinOuterAngle = .8f;
				_reticleGrowthAngle = .5f;
				break;
			case ReticleType.TINY_CIRCLE:
				reticleMinInnerAngle = 0f;
				reticleMinOuterAngle = .2f;
				_reticleGrowthAngle = 0f;
				break;
			case ReticleType.LARGE_CIRCLE:
				reticleMinInnerAngle = 0f;
				reticleMinOuterAngle = .5f;
				_reticleGrowthAngle = 0f;
				break;
			default:
				throw new NotImplementedException ();
			}
		}
	}

	/// Number of segments making the reticle circle.
	public int reticleSegments = 20;

	/// Growth speed multiplier for the reticle/
	public float reticleGrowthSpeed = 8.0f;

	// Private members
	private Material materialComp;
	private GameObject targetObj;

	// Current distance of the reticle (in meters).
	private float reticleDistanceInMeters;

	// Current inner and outer diameters of the reticle,
	// before distance multiplication.
	private float reticleInnerDiameter = 0.0f;
	private float reticleOuterDiameter = 0.0f;

	MeshRenderer meshRenderer;

	void Awake ()
	{
		meshRenderer = GetComponent<MeshRenderer> ();
		reticleDistanceInMeters = reticleMaxDistance;
	}

	void Start ()
	{
		reticleType = ReticleType.INTERACTIVE;

		CreateReticleVertices ();

		materialComp = gameObject.GetComponent<Renderer> ().material;
	}

	void OnEnable ()
	{
		VRMaster.instance.VRStateChange += OnVRStateChange;
		GameMaster.instance.GameStateChange += OnGameStateChange;
		MyReticleInputModule.reticlePointer = this;
	}

	void OnDisable ()
	{
		VRMaster.instance.VRStateChange -= OnVRStateChange;
		GameMaster.instance.GameStateChange -= OnGameStateChange;
		if (MyReticleInputModule.reticlePointer == (IReticlePointer)this) {
			MyReticleInputModule.reticlePointer = null;
		}
	}

	void OnGameStateChange (GameState gameState)
	{
		reticleMaxDistance = (gameState == GameState.SELECTING_GAME_STYLE || gameState == GameState.PLACING_SHIPS) ? 2f : 12f;
	}

	void OnVRStateChange (VRState vrState)
	{
		meshRenderer.enabled = VRMaster.instance.RETICLE_ENABLED;
	}

	#if UNITY_ANDROID
	void Update ()
	{
		if (meshRenderer.enabled) {
			transform.localRotation = GvrController.State == GvrConnectionState.Connected ? GvrController.Orientation : Camera.main.transform.localRotation;
			UpdateDiameters ();
		}
	}
	#endif

	/// This is called when the 'BaseInputModule' system should be enabled.
	public void OnReticlePointerEnabled ()
	{

	}

	/// This is called when the 'BaseInputModule' system should be disabled.
	public void OnReticlePointerDisabled ()
	{

	}

	/// Called when the user is looking on a valid GameObject. This can be a 3D
	/// or UI element.
	///
	/// The camera is the event camera, the target is the object
	/// the user is looking at, and the intersectionPosition is the intersection
	/// point of the ray sent from the camera on the object.
	public void OnReticlePointerStart (Camera camera, GameObject targetObject, Vector3 intersectionPosition,
	                                   bool isInteractive)
	{
		LaserInteractable li = targetObject.GetComponent<LaserInteractable> ();
		reticleType = li != null ? li.GetReticleGrowthAngle () : MyReticle.ReticleType.UNKNOWN;
		SetGazeTarget (intersectionPosition, isInteractive);
	}

	/// Called every frame the user is still looking at a valid GameObject. This
	/// can be a 3D or UI element.
	///
	/// The camera is the event camera, the target is the object the user is
	/// looking at, and the intersectionPosition is the intersection point of the
	/// ray sent from the camera on the object.
	public void OnReticlePointerStay (Camera camera, GameObject targetObject, Vector3 intersectionPosition,
	                                  bool isInteractive)
	{
		SetGazeTarget (intersectionPosition, isInteractive);
	}

	/// Called when the user's look no longer intersects an object previously
	/// intersected with a ray projected from the camera.
	/// This is also called just before **OnReticlePointerDisabled** and may have have any of
	/// the values set as **null**.
	///
	/// The camera is the event camera and the target is the object the user
	/// previously looked at.
	public void OnReticlePointerExit (Camera camera, GameObject targetObject)
	{
		reticleDistanceInMeters = reticleMaxDistance;
		_reticleInnerAngle = reticleMinInnerAngle;
		_reticleOuterAngle = reticleMinOuterAngle;
	}

	/// Called when a trigger event is initiated. This is practically when
	/// the user begins pressing the trigger.
	public void OnReticlePointerTriggerStart (Camera camera)
	{
		// Put your reticle trigger start logic here :)
	}

	/// Called when a trigger event is finished. This is practically when
	/// the user releases the trigger.
	public void OnReticlePointerTriggerEnd (Camera camera)
	{
		// Put your reticle trigger end logic here :)
	}

	public void GetPointerRadius (out float innerRadius, out float outerRadius)
	{
		float min_inner_angle_radians = Mathf.Deg2Rad * reticleMinInnerAngle;
		float max_inner_angle_radians = Mathf.Deg2Rad * (reticleMinInnerAngle + _reticleGrowthAngle);

		innerRadius = 2.0f * Mathf.Tan (min_inner_angle_radians);
		outerRadius = 2.0f * Mathf.Tan (max_inner_angle_radians);
	}

	private void CreateReticleVertices ()
	{
		Mesh mesh = new Mesh ();
		gameObject.AddComponent<MeshFilter> ();
		GetComponent<MeshFilter> ().mesh = mesh;

		int segments_count = reticleSegments;
		int vertex_count = (segments_count + 1) * 2;

		#region Vertices

		Vector3[] vertices = new Vector3[vertex_count];

		const float kTwoPi = Mathf.PI * 2.0f;
		int vi = 0;
		for (int si = 0; si <= segments_count; ++si) {
			// Add two vertices for every circle segment: one at the beginning of the
			// prism, and one at the end of the prism.
			float angle = (float)si / (float)(segments_count) * kTwoPi;

			float x = Mathf.Sin (angle);
			float y = Mathf.Cos (angle);

			vertices [vi++] = new Vector3 (x, y, 0.0f); // Outer vertex.
			vertices [vi++] = new Vector3 (x, y, 1.0f); // Inner vertex.
		}
		#endregion

		#region Triangles
		int indices_count = (segments_count + 1) * 3 * 2;
		int[] indices = new int[indices_count];

		int vert = 0;
		int idx = 0;
		for (int si = 0; si < segments_count; ++si) {
			indices [idx++] = vert + 1;
			indices [idx++] = vert;
			indices [idx++] = vert + 2;

			indices [idx++] = vert + 1;
			indices [idx++] = vert + 2;
			indices [idx++] = vert + 3;

			vert += 2;
		}
		#endregion

		mesh.vertices = vertices;
		mesh.triangles = indices;
		mesh.RecalculateBounds ();
		mesh.Optimize ();
	}

	private void UpdateDiameters ()
	{
		reticleDistanceInMeters =
			Mathf.Clamp (reticleDistanceInMeters, reticleMinDistance, reticleMaxDistance);

		if (_reticleInnerAngle < reticleMinInnerAngle) {
			_reticleInnerAngle = reticleMinInnerAngle;
		}

		if (_reticleOuterAngle < reticleMinOuterAngle) {
			_reticleOuterAngle = reticleMinOuterAngle;
		}

		float inner_half_angle_radians = Mathf.Deg2Rad * _reticleInnerAngle * 0.5f;
		float outer_half_angle_radians = Mathf.Deg2Rad * _reticleOuterAngle * 0.5f;

		float inner_diameter = 2.0f * Mathf.Tan (inner_half_angle_radians);
		float outer_diameter = 2.0f * Mathf.Tan (outer_half_angle_radians);

		reticleInnerDiameter =
			Mathf.Lerp (reticleInnerDiameter, inner_diameter, Time.deltaTime * reticleGrowthSpeed);
		reticleOuterDiameter =
			Mathf.Lerp (reticleOuterDiameter, outer_diameter, Time.deltaTime * reticleGrowthSpeed);

		materialComp.SetFloat ("_InnerDiameter", reticleInnerDiameter * reticleDistanceInMeters);
		materialComp.SetFloat ("_OuterDiameter", reticleOuterDiameter * reticleDistanceInMeters);
		materialComp.SetFloat ("_DistanceInMeters", reticleDistanceInMeters);
	}

	private void SetGazeTarget (Vector3 target, bool interactive)
	{
		Vector3 targetLocalPosition = transform.InverseTransformPoint (target);

		reticleDistanceInMeters =
			Mathf.Clamp (targetLocalPosition.z, reticleMinDistance, reticleMaxDistance);
		if (interactive) {
			_reticleInnerAngle = reticleMinInnerAngle + _reticleGrowthAngle;
			_reticleOuterAngle = reticleMinOuterAngle + _reticleGrowthAngle;
		} else {
			_reticleInnerAngle = reticleMinInnerAngle;
			_reticleOuterAngle = reticleMinOuterAngle;
		}
	}
}
