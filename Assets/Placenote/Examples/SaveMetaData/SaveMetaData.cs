﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;


using UnityEngine.XR.iOS; // Import ARKit Library

namespace SaveMetaData
{
    public class SaveMetaData : MonoBehaviour, PlacenoteListener1
    {
        // Unity ARKit Session handler
        private UnityARSessionNativeInterface mSession;

        // UI game object references
        public GameObject initPanel;
        public GameObject mappingPanel;
        public GameObject localizedPanel;

        public Text notifications;

        private bool modelsLoaded = false;

        // to hold the last saved MapID
        private string savedMapID;
        private LibPlacenote1.MapMetadata downloadedMetaData;

        void Start()
        {
            // Start ARKit using the Unity ARKit Plugin
            mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
            StartARKit();

            FeaturesVisualizer1.EnablePointcloud(); // Optional - to see the point features
            LibPlacenote1.Instance.RegisterListener(this); // Register listener for onStatusChange and OnPose
            notifications.text = "Click New Map to start";
        }


        // Add shape when button is clicked.
        public void OnNewMapClick()
        {
            notifications.text = "Mapping: Scan and Add Models";

            initPanel.SetActive(false);
            mappingPanel.SetActive(true);
            localizedPanel.SetActive(false);

            LibPlacenote1.Instance.StartSession();
        }

        // Initialize ARKit. This will be standard in all AR apps
        private void StartARKit()
        {
            Application.targetFrameRate = 60;
            ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration();
            config.planeDetection = UnityARPlaneDetection.Horizontal;
            config.alignment = UnityARAlignment.UnityARAlignmentGravity;
            config.getPointCloudData = true;
            config.enableLightEstimation = true;
            mSession.RunWithConfig(config);
        }

        // Save a map and upload it to Placenote cloud
        public void OnSaveMapClick()
        {
            mappingPanel.SetActive(false);
            initPanel.SetActive(true);
            localizedPanel.SetActive(false);

            FeaturesVisualizer1.clearPointcloud();

            if (!LibPlacenote1.Instance.Initialized())
            {
                notifications.text = "SDK not yet initialized";
                return;
            }

            // save and upload the map
            LibPlacenote1.Instance.SaveMap(
            (mapId) =>
            {
                savedMapID = mapId;
                LibPlacenote1.Instance.StopSession();
                WriteMapIDToFile(mapId);

            },
            (completed, faulted, percentage) =>
            {
                if (completed) {
                    notifications.text = "Upload Complete:" + savedMapID;

                    // upload meta data
                    LibPlacenote1.MapMetadataSettable metadata = CreateMetaDataObject();

                    LibPlacenote1.Instance.SetMetadata(savedMapID, metadata, (success) => {
                        if (success)
                        {
                            notifications.text = "Meta data successfully saved";
                        }
                        else
                        {
                            notifications.text = "Meta data failed to save";
                        }
                    });

                    GetComponent<ModelManager1>().ClearModels();

                }
                else if (faulted) {
                    notifications.text = "Upload of Map: " + savedMapID + " failed";
                }
                else {
                    notifications.text = "Upload Progress: " + percentage.ToString("F2") + "/1.0)";
                }
            }
            );


        }


        public LibPlacenote1.MapMetadataSettable CreateMetaDataObject()
        {
            LibPlacenote1.MapMetadataSettable metadata = new LibPlacenote1.MapMetadataSettable();

            metadata.name = "My test map";

            // get GPS location of device to save with map
            bool useLocation = Input.location.status == LocationServiceStatus.Running;
            LocationInfo locationInfo = Input.location.lastData;
            if (useLocation)
            {
                metadata.location = new LibPlacenote1.MapLocation();
                metadata.location.latitude = locationInfo.latitude;
                metadata.location.longitude = locationInfo.longitude;
                metadata.location.altitude = locationInfo.altitude;
            }

            JObject userdata = new JObject();
            JObject modelList = GetComponent<ModelManager1>().Models2JSON();
            userdata["modelList"] = modelList;

            metadata.userdata = userdata;
            return metadata;
        }
         

        // Load map and relocalize. Check OnStatusChange function for behaviour upon relocalization
        public void OnLoadMapClicked()
        {
            if (!LibPlacenote1.Instance.Initialized())
            {
                notifications.text = "SDK not yet initialized";
                return;
            }

            // Reading the last saved MapID from file
            savedMapID = ReadMapIDFromFile();

            if (savedMapID == null)
            {
                notifications.text = "You haven't saved a map yet";
                return;
            }

            initPanel.SetActive(false);
            mappingPanel.SetActive(false);
            localizedPanel.SetActive(true);


            LibPlacenote1.Instance.LoadMap(savedMapID,
            (completed, faulted, percentage) =>
            {
                if (completed)
                {
                    // Get the meta data as soon as the map is downloaded
                    LibPlacenote1.Instance.GetMetadata(savedMapID,(LibPlacenote1.MapMetadata obj) => 
                    {
                        if (obj!=null) {
                            downloadedMetaData = obj;

                            // Now try to localize the map
                            LibPlacenote1.Instance.StartSession();
                            notifications.text = "Trying to Localize Map: " + savedMapID;
                        }
                        else {
                            notifications.text = "Failed to download meta data";
                            return;
                        }
                    });

                }
                else if (faulted)
                {
                    notifications.text = "Failed to load ID: " + savedMapID;
                }
                else
                {
                    notifications.text = "Download Progress: " + percentage.ToString("F2") + "/1.0)";
                }
            }

            );
        }

        public void OnExitClicked()
        {
            LibPlacenote1.Instance.StopSession();
            FeaturesVisualizer1.clearPointcloud();
            GetComponent<ModelManager1>().ClearModels();

            modelsLoaded = false;

            initPanel.SetActive(true);
            mappingPanel.SetActive(false);
            localizedPanel.SetActive(false);

        }

        // Runs when a new pose is received from Placenote.    
        public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

        // Runs when LibPlacenote1 sends a status change message like Localized!
        public void OnStatusChange(LibPlacenote1.MappingStatus prevStatus, LibPlacenote1.MappingStatus currStatus)
        {
            if (currStatus == LibPlacenote1.MappingStatus.RUNNING && prevStatus == LibPlacenote1.MappingStatus.LOST)
            {
                notifications.text = "Localized!";

                if (!modelsLoaded)
                {
                    modelsLoaded = true;
                    JToken modelData = downloadedMetaData.userdata;
                    GetComponent<ModelManager1>().LoadModelsFromJSON(modelData);

                }

                // Placenote will automatically correct the camera position on localization.
            }
        }

        private void WriteMapIDToFile(string mapID)
        {
            string path = Application.persistentDataPath + "/mapID.txt";
            Debug.Log(path);
            StreamWriter writer = new StreamWriter(path, false);
            writer.WriteLine(mapID);
            writer.Close();
        }

        private string ReadMapIDFromFile()
        {
            string path = Application.persistentDataPath + "/mapID.txt";
            Debug.Log(path);

            if (System.IO.File.Exists(path))
            {
                StreamReader reader = new StreamReader(path);
                string returnValue = reader.ReadLine();

                Debug.Log(returnValue);
                reader.Close();

                return returnValue;
            }
            else
            {
                return null;
            }


        }

    }
}
