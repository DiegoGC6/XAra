﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Class that constructs a pointcloud mesh from the map retrieved from a LibPlacenote mapping/localization session
/// </summary>
public class FeaturesVisualizer1 : MonoBehaviour, PlacenoteListener1
{
	private static FeaturesVisualizer1 sInstance;
	[SerializeField] Material mPtCloudMat;
	[SerializeField] GameObject mMap;

	void Awake ()
	{
		sInstance = this;
	}

	void Start ()
	{
        // This is required for OnPose and OnStatusChange to be triggered
        LibPlacenote1.Instance.RegisterListener (this);
	}

	void Update ()
	{
	}

	/// <summary>
	/// Enable rendering of pointclouds collected from LibPlacenote for every half second
	/// </summary>
	/// <remarks>
	/// NOTE: to avoid the static instance being null, please call this in Start() function in your MonoBehaviour
	/// </remarks>
	public static void EnablePointcloud ()
	{
		if (sInstance.mMap == null) {
			Debug.LogWarning (
				"Map game object reference is null, please initialize in editor.Skipping pointcloud visualization"
			);
			return;
		}
		sInstance.InvokeRepeating ("DrawMap", 0f, 0.1f);
	}

	/// <summary>
	/// Disable rendering of pointclouds collected from LibPlacenote
	/// </summary>
	public static void DisablePointcloud ()
	{
		sInstance.CancelInvoke ();
		clearPointcloud ();
	}


	/// <summary>
	///  Clear currently rendering feature/landmark pointcloud
	/// </summary>
	public static void clearPointcloud() 
	{
		MeshFilter mf = sInstance.mMap.GetComponent<MeshFilter> ();
		mf.mesh.Clear ();
	}

	public void OnPose (Matrix4x4 outputPose, Matrix4x4 arkitPose)
	{
	}

	public void OnStatusChange (LibPlacenote1.MappingStatus prevStatus, LibPlacenote1.MappingStatus currStatus)
	{
		if (currStatus == LibPlacenote1.MappingStatus.WAITING) {
			Debug.Log ("Session stopped, resetting pointcloud mesh.");
			clearPointcloud ();
		}
	}

	public void DrawMap ()
	{
		if (LibPlacenote1.Instance.GetStatus () != LibPlacenote1.MappingStatus.RUNNING) {
			return;
		}

		LibPlacenote1.PNFeaturePointUnity[] map = LibPlacenote1.Instance.GetMap ();

		if (map == null) {
			return;
		}

		Vector3[] points = new Vector3[map.Length];
		Color[] colors = new Color[map.Length];
		for (int i = 0; i < map.Length; ++i) {

			points [i].x = map [i].point.x;
			points [i].y = map [i].point.y;
			points [i].z = -map [i].point.z;
			colors [i].r = 1 - map [i].measCount / 10f;
			colors [i].b = 0;
			colors [i].g = map [i].measCount / 10f;

			if (map [i].measCount < 4) {
				colors [i].a = 0;
			} else {
				colors [i].a = 0.2f + 0.8f * (map [i].measCount / 10f);
			}
		}

		// Need to update indicies too!
		int[] indices = new int[map.Length];
		for (int i = 0; i < map.Length; ++i) {
			indices [i] = i;
		}

		// Create GameObject container with mesh components for the loaded mesh.
		MeshFilter mf = mMap.GetComponent<MeshFilter> ();
		if (mf == null) {
			mf = mMap.AddComponent<MeshFilter> ();
			mf.mesh = new Mesh ();
		}
		mf.mesh.Clear ();
		mf.mesh.vertices = points;
		mf.mesh.colors = colors;
		mf.mesh.SetIndices (indices, MeshTopology.Points, 0);

		MeshRenderer mr = mMap.GetComponent<MeshRenderer> ();
		if (mr == null) {
			mr = mMap.AddComponent<MeshRenderer> ();
		} 
		mr.material = mPtCloudMat;
	}

	#if UNITY_EDITOR 
	public static void DrawMapEditor(SpatialCapture.PNLandmark1[] map, Transform parent) {
	//public static void DrawMapEditor(UnityEngine.XR.iOS.SpatialCapture.PNLandmark1[] map, Transform parent) {
		GameObject[] points = new GameObject[map.Length];
		CombineInstance[] combinedPoints = new CombineInstance[map.Length];

		Vector3 scale = new Vector3 (0.01f, 0.01f, 0.01f);

		for (int i = 0; i < map.Length; ++i) {
			if (map [i].measCount > 4) {
				Vector3 position = new Vector3 (map [i].point.x, map [i].point.y, -map [i].point.z);
				points [i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				points [i].transform.position = position;
				points [i].transform.localScale = scale;
				if (parent != null) {
					points [i].transform.SetParent (parent);
				}

				Renderer point_rend = points [i].GetComponent<Renderer> ();
				var tempMaterial = new Material(point_rend.sharedMaterial);


				Color ptColor = new Color ();
				ptColor.r = 1 - map [i].measCount / 10f;
				ptColor.b = 0;
				ptColor.g = map [i].measCount / 10f;

				tempMaterial.color = ptColor;
				point_rend.sharedMaterial = tempMaterial;
			}
		}
	}
	#endif

}
