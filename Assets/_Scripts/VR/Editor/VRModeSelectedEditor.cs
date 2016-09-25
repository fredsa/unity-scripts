﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System;

[CustomEditor (typeof(VRModeSelected))]
[CanEditMultipleObjects]
public class VRModeSelectedEditor : Editor
{

	public override void OnInspectorGUI ()
	{
		//DrawDefaultInspector ();

		VRModeSelected controller = (VRModeSelected)target;

		bool somethingChecked = false;
		for (int i = 0; i < controller.enabledWhen.Length; i++) {
			controller.enabledWhen [i] = EditorGUILayout.Toggle (((VRState)i).ToString (), controller.enabledWhen [i]);
			somethingChecked |= controller.enabledWhen [i];
		}
		if (!somethingChecked) {
			Debug.LogWarning (controller.name + ": " + typeof(VRModeSelected) + " must be enabled for at least one " + typeof(VRState), controller);
		}

		serializedObject.ApplyModifiedProperties ();
	}

}
