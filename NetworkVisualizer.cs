using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Networking;

public class NetworkVisualizer : MonoBehaviour
{
    // Initializing parameters and values
    public TextMeshPro networkInfoText;       
    public TextMeshPro networkLogText;        
    public Camera mainCamera;                 
    public GameObject downloadCone;           
    public GameObject uploadCone;            

    private float downloadSpeedSum = 0f;
    private int downloadSpeedCount = 0;
    private float uploadSpeedSum = 0f;
    private int uploadSpeedCount = 0;
    private bool isMeasuring = true;
    private float holdStartTime;
    private bool isHolding = false;
    private List<string> logEntries = new List<string>(); // Store last 10 networking log entries

    private Coroutine logUpdateCoroutine; // Coroutine to update logs while holding

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Set initial font sizes
        networkInfoText.fontSize = 5;       
        networkLogText.fontSize = 4;        

        StartCoroutine(UpdateNetworkInfo());
    }

  
    private void Update()
    {
        HandleInput();
    }
    // Checks if I am tapping the screen or holding it. If more than 3 seconds, it counts as holding, otherwise count as tapping
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            holdStartTime = Time.time;
            isHolding = true;
        }
        // If holding the screen
        if (Input.GetMouseButton(0))
        {
            if (isHolding && Time.time - holdStartTime >= 3.0f)
            {
                // Show network packet log in front of the camera
                PositionLogInFrontOfCamera();

                if (logUpdateCoroutine == null)
                {
                    logUpdateCoroutine = StartCoroutine(ShowLogsWhileHolding());
                }

                isHolding = false; // Prevent re-triggering 
            }
        }
        // If identified as tapping the screen
        if (Input.GetMouseButtonUp(0))
        {
            if (isHolding && Time.time - holdStartTime < 3.0f)
            {
                // Show speed logs in front of the camera
                CenterTextInFrontOfCamera(networkInfoText);
            }

            // Stop updating logs when holding ends
            if (logUpdateCoroutine != null)
            {
                StopCoroutine(logUpdateCoroutine);
                logUpdateCoroutine = null;
            }

            isHolding = false;
        }
    }
    // The speed log that shows at the center of the screen when I tab
    private void CenterTextInFrontOfCamera(TextMeshPro textMeshPro)
    {
        // Update network info text with the latest speeds
        textMeshPro.text = $"Download Speed (Left Cylinder): {downloadSpeedSum / Math.Max(downloadSpeedCount, 1):F2} KB/s\n" +
                           $"Upload Speed (Right Cylinder): {uploadSpeedSum / Math.Max(uploadSpeedCount, 1):F2} KB/s";

        // Position text in front of the camera
        textMeshPro.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 3.0f;
        textMeshPro.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }
    
    // To show the network logging data on the center of the screen
    private void PositionLogInFrontOfCamera()
    {
        
        networkLogText.transform.position = mainCamera.transform.position + mainCamera.transform.forward * 3.2f;
        networkLogText.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }
    // Display the latest packet logs while I am holding the screen
    private IEnumerator ShowLogsWhileHolding()
    {
        while (true)
        {
            networkLogText.text = string.Join("\n", logEntries);
            yield return new WaitForSeconds(1.0f); // Update logs every second
        }
    }
    // Updating the uplink and downlink speed in real-time
    private IEnumerator UpdateNetworkInfo()
    {
        while (isMeasuring)
        {
            // Measure download and upload speeds
            yield return StartCoroutine(MeasureDownloadSpeedCoroutine());
            yield return StartCoroutine(MeasureUploadSpeedCoroutine());

            // Calculate real-time speeds
            float averageDownloadSpeed = downloadSpeedCount > 0 ? downloadSpeedSum / downloadSpeedCount : 0;
            float averageUploadSpeed = uploadSpeedCount > 0 ? uploadSpeedSum / uploadSpeedCount : 0;

            // Update the speed text in real-time
            networkInfoText.text = $"Download Speed (Right Cylinder): {averageDownloadSpeed:F2} KB/s\n" +
                                $"Upload Speed (Left Cylinder): {averageUploadSpeed:F2} KB/s";

            // Update cone heights based on the speeds
            UpdateConeHeights(averageDownloadSpeed, averageUploadSpeed);

            // Update data log in real-time
            networkLogText.text = string.Join("\n", logEntries);

            yield return new WaitForSeconds(1); // Refresh every second
        }
    }


    // Function to update the relative heights of the cones that uses height to represent uplink/downlink speed
    private void UpdateConeHeights(float downloadSpeed, float uploadSpeed)
    {
        // Normalize speeds for cone height adjustment
        float downloadHeight = Mathf.Clamp(downloadSpeed / 50.0f, 0.1f, 10.0f); 
        float uploadHeight = Mathf.Clamp(uploadSpeed / 50.0f, 0.1f, 10.0f);

        // Update cone heights
        if (downloadCone != null)
        {
            Vector3 downloadScale = downloadCone.transform.localScale;
            downloadCone.transform.localScale = new Vector3(downloadScale.x, downloadHeight, downloadScale.z);

            // Change color based on download speed
            Renderer renderer = downloadCone.GetComponent<Renderer>();
            renderer.material.color = GetSpeedColor(downloadSpeed);
        }

        if (uploadCone != null)
        {
            Vector3 uploadScale = uploadCone.transform.localScale;
            uploadCone.transform.localScale = new Vector3(uploadScale.x, uploadHeight, uploadScale.z);

            // Change color based on upload speed
            Renderer renderer = uploadCone.GetComponent<Renderer>();
            renderer.material.color = GetSpeedColor(uploadSpeed);
        }
    }

    private Color GetSpeedColor(float speed)
    {
        // Define color based on speed ranges
        if (speed < 10.0f)
            return Color.red; // Slow speed: Red
        else if (speed < 30.0f)
            return Color.yellow; // Moderate speed: Yellow
        else
            return Color.green; // Fast speed: Green
    }

    // Measuring the downlink speed by getting web content from google.com
    private IEnumerator MeasureDownloadSpeedCoroutine()
    {
        string url = "https://www.google.com";
        UnityWebRequest request = UnityWebRequest.Get(url);

        DateTime startTime = DateTime.UtcNow;
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            DateTime endTime = DateTime.UtcNow;
            float downloadSizeKB = request.downloadedBytes / 1024f;
            double downloadTime = (endTime - startTime).TotalSeconds;

            float downloadSpeed = (float)(downloadSizeKB / downloadTime);
            downloadSpeedSum += downloadSpeed;
            downloadSpeedCount++;

            LogPacketData("Download", request, downloadSpeed, request.downloadHandler.data);
        }
    }
    // Measuring the uplink speed by generating post request to httpbin.org
    private IEnumerator MeasureUploadSpeedCoroutine()
    {
        string url = "https://httpbin.org/post";
        UnityWebRequest request = new UnityWebRequest(url, "POST");

        byte[] dataToUpload = new byte[1024 * 500]; // 500KB of data
        request.uploadHandler = new UploadHandlerRaw(dataToUpload);
        request.downloadHandler = new DownloadHandlerBuffer();

        DateTime startTime = DateTime.UtcNow;
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            DateTime endTime = DateTime.UtcNow;
            float uploadSizeKB = dataToUpload.Length / 1024f;
            double uploadTime = (endTime - startTime).TotalSeconds;

            float uploadSpeed = (float)(uploadSizeKB / uploadTime);
            uploadSpeedSum += uploadSpeed;
            uploadSpeedCount++;

            LogPacketData("Upload", request, uploadSpeed, dataToUpload);
        }
    }

    // Logging the network packect data back to the textmesh pro object, which includes timestamp, raw data byte, where communication was made, etc.
    private void LogPacketData(string direction, UnityWebRequest request, float speed, byte[] rawData)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string rawDataPreview = BitConverter.ToString(rawData, 0, Math.Min(50, rawData.Length)) + (rawData.Length > 50 ? "..." : "");
        string logEntry = $"[{timestamp}] {direction}\nURL: {request.url}\nStatus: {request.responseCode}\n" +
                          $"Speed: {speed:F2} KB/s\nRaw Data: {rawDataPreview}";

        logEntries.Insert(0, logEntry); // Add new entry at the top
        if (logEntries.Count > 10)
        {
            logEntries.RemoveAt(logEntries.Count - 1); // Remove the oldest entry if more than 10 entrys is collected
        }
    }
}
