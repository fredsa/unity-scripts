using UnityEngine;
using System.Collections;
using System;

public class VRModeSelected : MonoBehaviour
{

	public bool[] enabledWhen = new bool[Enum.GetValues (typeof(VRState)).Length];

	void Awake ()
	{
		VRMaster.instance.VRStateChange += OnVRStateChange;
	}

	void OnDestroy ()
	{
		VRMaster.instance.VRStateChange -= OnVRStateChange;
	}

	void OnVRStateChange (VRState state, float rotationAngle)
	{
		gameObject.SetActive (enabledWhen [(int)state]);
	}

}
