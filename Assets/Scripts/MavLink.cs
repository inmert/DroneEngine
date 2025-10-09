using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Receives IMU data from MAVLink bridge and applies rotation to 3D model
/// Attach this script to your FBX model in Unity
/// </summary>
public class IMUReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private int listenPort = 25001;
    
    [Header("Rotation Settings")]
    [SerializeField] private bool useRoll = true;
    [SerializeField] private bool usePitch = true;
    [SerializeField] private bool useYaw = true;
    [SerializeField] private float smoothing = 5f;
    [SerializeField] private bool invertRoll = false;
    [SerializeField] private bool invertPitch = false;
    [SerializeField] private bool invertYaw = false;
    
    [Header("Coordinate System")]
    [Tooltip("ArduCopter uses NED (North-East-Down), Unity uses left-handed Y-up")]
    [SerializeField] private bool convertFromNED = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Private variables
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;
    
    private Vector3 targetRotation = Vector3.zero;
    private Vector3 currentRotation = Vector3.zero;
    private Vector3 acceleration = Vector3.zero;
    private Vector3 gyroscope = Vector3.zero;
    
    private float lastUpdateTime = 0f;
    private int packetsReceived = 0;
    
    [System.Serializable]
    private class IMUData
    {
        public double timestamp;
        public AttitudeData attitude;
        public RawIMUData imu;
    }
    
    [System.Serializable]
    private class AttitudeData
    {
        public float roll;
        public float pitch;
        public float yaw;
        public float rollspeed;
        public float pitchspeed;
        public float yawspeed;
    }
    
    [System.Serializable]
    private class RawIMUData
    {
        public float accel_x;
        public float accel_y;
        public float accel_z;
        public float gyro_x;
        public float gyro_y;
        public float gyro_z;
    }
    
    void Start()
    {
        StartUDPListener();
    }
    
    void StartUDPListener()
    {
        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log($"IMU Receiver started on port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start UDP listener: {e.Message}");
        }
    }
    
    void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                string jsonData = Encoding.UTF8.GetString(data);
                
                // Parse JSON data
                IMUData imuData = JsonUtility.FromJson<IMUData>(jsonData);
                
                if (imuData != null && imuData.attitude != null)
                {
                    // Update target rotation
                    float roll = useRoll ? imuData.attitude.roll : 0f;
                    float pitch = usePitch ? imuData.attitude.pitch : 0f;
                    float yaw = useYaw ? imuData.attitude.yaw : 0f;
                    
                    // Apply inversions
                    if (invertRoll) roll = -roll;
                    if (invertPitch) pitch = -pitch;
                    if (invertYaw) yaw = -yaw;
                    
                    // Convert from NED to Unity coordinate system
                    if (convertFromNED)
                    {
                        // ArduCopter NED: X=North, Y=East, Z=Down
                        // Unity: X=Right, Y=Up, Z=Forward
                        // Roll: rotation around X (forward) axis
                        // Pitch: rotation around Y (right) axis  
                        // Yaw: rotation around Z (up) axis
                        targetRotation = new Vector3(-pitch, yaw, -roll);
                    }
                    else
                    {
                        targetRotation = new Vector3(pitch, yaw, roll);
                    }
                    
                    // Update acceleration and gyroscope data
                    if (imuData.imu != null)
                    {
                        acceleration = new Vector3(
                            imuData.imu.accel_x,
                            imuData.imu.accel_y,
                            imuData.imu.accel_z
                        );
                        
                        gyroscope = new Vector3(
                            imuData.imu.gyro_x,
                            imuData.imu.gyro_y,
                            imuData.imu.gyro_z
                        );
                    }
                    
                    lastUpdateTime = Time.time;
                    packetsReceived++;
                }
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogWarning($"Error receiving data: {e.Message}");
                }
            }
        }
    }
    
    void Update()
    {
        // Smoothly interpolate to target rotation
        currentRotation = Vector3.Lerp(currentRotation, targetRotation, Time.deltaTime * smoothing);
        transform.rotation = Quaternion.Euler(currentRotation);
        
        // Debug info
        if (showDebugInfo && Time.frameCount % 30 == 0) // Update every ~0.5 seconds
        {
            float timeSinceUpdate = Time.time - lastUpdateTime;
            if (timeSinceUpdate > 1f)
            {
                Debug.LogWarning($"No data received for {timeSinceUpdate:F1} seconds");
            }
        }
    }
    
    void OnGUI()
    {
        if (showDebugInfo)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Box("IMU Data Monitor");
            GUILayout.Label($"Packets: {packetsReceived}");
            GUILayout.Label($"Roll: {targetRotation.z:F2}°");
            GUILayout.Label($"Pitch: {targetRotation.x:F2}°");
            GUILayout.Label($"Yaw: {targetRotation.y:F2}°");
            GUILayout.Label($"Accel: {acceleration.magnitude:F2} m/s²");
            GUILayout.Label($"Last Update: {Time.time - lastUpdateTime:F2}s ago");
            GUILayout.EndArea();
        }
    }
    
    void OnApplicationQuit()
    {
        StopUDPListener();
    }
    
    void OnDestroy()
    {
        StopUDPListener();
    }
    
    void StopUDPListener()
    {
        isRunning = false;
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
        
        if (udpClient != null)
        {
            udpClient.Close();
        }
        
        Debug.Log("IMU Receiver stopped");
    }
}