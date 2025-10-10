using UnityEngine;
using UnityEngine.UIElements;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System;
using System.Collections.Concurrent;

/// <summary>
/// Complete DroneEngine UI Controller with RealSense D455 integration
/// Includes tab management, sensor feeds, and optimized performance
/// </summary>
public class DroneEngineController : MonoBehaviour
{
    [Header("ROS Topics")]
    [SerializeField] private string depthTopic = "/camera/camera/depth/image_rect_raw";
    [SerializeField] private string irTopic = "/camera/camera/infra1/image_rect_raw";
    [SerializeField] private string colorTopic = "/camera/camera/color/image_raw";
    [SerializeField] private bool useCompressedTopics = false;
    
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;
    
    [Header("Performance Settings")]
    [SerializeField] private bool skipFrames = false;
    [SerializeField] private int frameSkipCount = 1;
    
    // UI Elements - Tabs
    private Button sensorsTabBtn;
    private Button flightTabBtn;
    private VisualElement sensorsTab;
    private VisualElement flightTab;
    
    // UI Elements - Sensor Feeds
    private VisualElement depthFeedDisplay;
    private VisualElement irFeedDisplay;
    private VisualElement colorFeedDisplay;
    private VisualElement lidarFeedDisplay;
    
    // UI Elements - Status Labels
    private Label depthStatus;
    private Label irStatus;
    private Label colorStatus;
    private Label lidarStatus;
    private Label sensorsOnlineCount;
    private Label sensorFps;
    private Label statusIndicator;
    
    // UI Elements - Buttons
    private Button depthConnectBtn;
    private Button irConnectBtn;
    private Button colorConnectBtn;
    private Button lidarConnectBtn;
    private Button connectAllBtn;
    private Button disconnectAllBtn;
    
    // Connection states
    private bool depthConnected = true;
    private bool irConnected = true;
    private bool colorConnected = true;
    private bool lidarConnected = false;
    
    // Textures for camera feeds
    private Texture2D depthTexture;
    private Texture2D irTexture;
    private Texture2D colorTexture;
    
    // FPS calculation
    private float lastFrameTime;
    private int frameCount;
    private float fps = 0f;
    
    // Frame skipping counters
    private int depthFrameCounter = 0;
    private int irFrameCounter = 0;
    private int colorFrameCounter = 0;
    
    // Thread-safe queues for image data
    private ConcurrentQueue<ImageData> depthQueue = new ConcurrentQueue<ImageData>();
    private ConcurrentQueue<ImageData> irQueue = new ConcurrentQueue<ImageData>();
    private ConcurrentQueue<ImageData> colorQueue = new ConcurrentQueue<ImageData>();
    
    private ROSConnection ros;
    
    private struct ImageData
    {
        public byte[] data;
        public int width;
        public int height;
        public string encoding;
    }
    
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        InitializeUI();
        SubscribeToTopics();
        lastFrameTime = Time.time;
        
        Debug.Log("DroneEngine Controller initialized - All systems operational");
    }
    
    void InitializeUI()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("UIDocument not found!");
                return;
            }
        }
        
        var root = uiDocument.rootVisualElement;
        
        // Get Tab Elements
        sensorsTabBtn = root.Q<Button>("sensors-tab-btn");
        flightTabBtn = root.Q<Button>("flight-tab-btn");
        sensorsTab = root.Q<VisualElement>("sensors-tab");
        flightTab = root.Q<VisualElement>("flight-tab");
        
        // Get Feed Displays
        depthFeedDisplay = root.Q<VisualElement>("depth-feed-display");
        irFeedDisplay = root.Q<VisualElement>("ir-feed-display");
        colorFeedDisplay = root.Q<VisualElement>("color-feed-display");
        lidarFeedDisplay = root.Q<VisualElement>("lidar-feed-display");
        
        // Get Status Elements
        depthStatus = root.Q<Label>("depth-status");
        irStatus = root.Q<Label>("ir-status");
        colorStatus = root.Q<Label>("color-status");
        lidarStatus = root.Q<Label>("lidar-status");
        sensorsOnlineCount = root.Q<Label>("sensors-online-count");
        sensorFps = root.Q<Label>("sensor-fps");
        statusIndicator = root.Q<Label>("status-indicator");
        
        // Get Buttons
        depthConnectBtn = root.Q<Button>("depth-connect-btn");
        irConnectBtn = root.Q<Button>("ir-connect-btn");
        colorConnectBtn = root.Q<Button>("color-connect-btn");
        lidarConnectBtn = root.Q<Button>("lidar-connect-btn");
        connectAllBtn = root.Q<Button>("connect-all-btn");
        disconnectAllBtn = root.Q<Button>("disconnect-all-btn");
        
        // Setup Tab Switching
        sensorsTabBtn?.RegisterCallback<ClickEvent>(evt => SwitchTab(true));
        flightTabBtn?.RegisterCallback<ClickEvent>(evt => SwitchTab(false));
        
        // Setup Individual Connect Buttons
        depthConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleDepthConnection());
        irConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleIRConnection());
        colorConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleColorConnection());
        lidarConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleLidarConnection());
        
        // Setup Connect/Disconnect All
        connectAllBtn?.RegisterCallback<ClickEvent>(evt => ConnectAll());
        disconnectAllBtn?.RegisterCallback<ClickEvent>(evt => DisconnectAll());
        
        // Initialize textures
        depthTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        depthTexture.filterMode = FilterMode.Bilinear;
        
        irTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        irTexture.filterMode = FilterMode.Bilinear;
        
        colorTexture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        colorTexture.filterMode = FilterMode.Bilinear;
        
        UpdateConnectionUI();
    }
    
    void SubscribeToTopics()
    {
        if (useCompressedTopics)
        {
            ros.Subscribe<CompressedImageMsg>(depthTopic + "/compressed", OnDepthImageCompressed);
            ros.Subscribe<CompressedImageMsg>(irTopic + "/compressed", OnIRImageCompressed);
            ros.Subscribe<CompressedImageMsg>(colorTopic + "/compressed", OnColorImageCompressed);
            Debug.Log("Subscribed to COMPRESSED image topics");
        }
        else
        {
            ros.Subscribe<ImageMsg>(depthTopic, OnDepthImageRaw);
            ros.Subscribe<ImageMsg>(irTopic, OnIRImageRaw);
            ros.Subscribe<ImageMsg>(colorTopic, OnColorImageRaw);
            Debug.Log("Subscribed to RAW image topics");
        }
        
        Debug.Log($"Topics:\n- {depthTopic}\n- {irTopic}\n- {colorTopic}");
    }
    
    #region ROS Callbacks - Raw Images
    
    void OnDepthImageRaw(ImageMsg msg)
    {
        if (!depthConnected) return;
        
        if (skipFrames)
        {
            depthFrameCounter++;
            if (depthFrameCounter % frameSkipCount != 0) return;
        }
        
        if (depthQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = (int)msg.width,
                height = (int)msg.height,
                encoding = msg.encoding
            };
            depthQueue.Enqueue(imgData);
        }
    }
    
    void OnIRImageRaw(ImageMsg msg)
    {
        if (!irConnected) return;
        
        if (skipFrames)
        {
            irFrameCounter++;
            if (irFrameCounter % frameSkipCount != 0) return;
        }
        
        if (irQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = (int)msg.width,
                height = (int)msg.height,
                encoding = msg.encoding
            };
            irQueue.Enqueue(imgData);
        }
    }
    
    void OnColorImageRaw(ImageMsg msg)
    {
        if (!colorConnected) return;
        
        if (skipFrames)
        {
            colorFrameCounter++;
            if (colorFrameCounter % frameSkipCount != 0) return;
        }
        
        if (colorQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = (int)msg.width,
                height = (int)msg.height,
                encoding = msg.encoding
            };
            colorQueue.Enqueue(imgData);
        }
    }
    
    #endregion
    
    #region ROS Callbacks - Compressed Images
    
    void OnDepthImageCompressed(CompressedImageMsg msg)
    {
        if (!depthConnected) return;
        
        if (skipFrames)
        {
            depthFrameCounter++;
            if (depthFrameCounter % frameSkipCount != 0) return;
        }
        
        if (depthQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = 640,
                height = 480,
                encoding = "compressed"
            };
            depthQueue.Enqueue(imgData);
        }
    }
    
    void OnIRImageCompressed(CompressedImageMsg msg)
    {
        if (!irConnected) return;
        
        if (skipFrames)
        {
            irFrameCounter++;
            if (irFrameCounter % frameSkipCount != 0) return;
        }
        
        if (irQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = 640,
                height = 480,
                encoding = "compressed"
            };
            irQueue.Enqueue(imgData);
        }
    }
    
    void OnColorImageCompressed(CompressedImageMsg msg)
    {
        if (!colorConnected) return;
        
        if (skipFrames)
        {
            colorFrameCounter++;
            if (colorFrameCounter % frameSkipCount != 0) return;
        }
        
        if (colorQueue.Count < 2)
        {
            ImageData imgData = new ImageData
            {
                data = msg.data,
                width = 1920,
                height = 1080,
                encoding = "compressed"
            };
            colorQueue.Enqueue(imgData);
        }
    }
    
    #endregion
    
    void Update()
    {
        ProcessImageQueue(depthQueue, depthTexture, depthFeedDisplay);
        ProcessImageQueue(irQueue, irTexture, irFeedDisplay);
        ProcessImageQueue(colorQueue, colorTexture, colorFeedDisplay);
        
        UpdateFPS();
    }
    
    void ProcessImageQueue(ConcurrentQueue<ImageData> queue, Texture2D texture, VisualElement display)
    {
        if (queue.TryDequeue(out ImageData imgData))
        {
            try
            {
                if (imgData.encoding == "compressed")
                {
                    texture.LoadImage(imgData.data);
                    texture.Apply();
                }
                else
                {
                    byte[] rgbData = ConvertRawToRGB(imgData);
                    
                    if (texture.width != imgData.width || texture.height != imgData.height)
                    {
                        texture.Reinitialize(imgData.width, imgData.height);
                    }
                    
                    texture.LoadRawTextureData(rgbData);
                    texture.Apply();
                }
                
                UpdateFeedDisplay(display, texture);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing image: {e.Message}");
            }
        }
    }
    
    byte[] ConvertRawToRGB(ImageData imgData)
    {
        int pixelCount = imgData.width * imgData.height;
        byte[] rgb = new byte[pixelCount * 3];
        
        if (imgData.encoding == "mono8")
        {
            for (int i = 0; i < pixelCount; i++)
            {
                byte value = imgData.data[i];
                rgb[i * 3] = value;
                rgb[i * 3 + 1] = value;
                rgb[i * 3 + 2] = value;
            }
        }
        else if (imgData.encoding == "16UC1")
        {
            for (int i = 0; i < pixelCount; i++)
            {
                ushort value = (ushort)((imgData.data[i * 2 + 1] << 8) | imgData.data[i * 2]);
                byte normalized = (byte)(value / 256);
                rgb[i * 3] = normalized;
                rgb[i * 3 + 1] = normalized;
                rgb[i * 3 + 2] = normalized;
            }
        }
        else if (imgData.encoding == "rgb8")
        {
            Array.Copy(imgData.data, rgb, rgb.Length);
        }
        else if (imgData.encoding == "bgr8")
        {
            for (int i = 0; i < pixelCount; i++)
            {
                rgb[i * 3] = imgData.data[i * 3 + 2];
                rgb[i * 3 + 1] = imgData.data[i * 3 + 1];
                rgb[i * 3 + 2] = imgData.data[i * 3];
            }
        }
        else if (imgData.encoding == "rgba8" || imgData.encoding == "bgra8")
        {
            bool isBGR = imgData.encoding == "bgra8";
            for (int i = 0; i < pixelCount; i++)
            {
                if (isBGR)
                {
                    rgb[i * 3] = imgData.data[i * 4 + 2];
                    rgb[i * 3 + 1] = imgData.data[i * 4 + 1];
                    rgb[i * 3 + 2] = imgData.data[i * 4];
                }
                else
                {
                    rgb[i * 3] = imgData.data[i * 4];
                    rgb[i * 3 + 1] = imgData.data[i * 4 + 1];
                    rgb[i * 3 + 2] = imgData.data[i * 4 + 2];
                }
            }
        }
        else
        {
            Debug.LogWarning($"Unsupported encoding: {imgData.encoding}");
            Array.Copy(imgData.data, rgb, Mathf.Min(rgb.Length, imgData.data.Length));
        }
        
        return rgb;
    }
    
    void UpdateFeedDisplay(VisualElement display, Texture2D texture)
    {
        if (display == null || texture == null) return;
        
        if (display.childCount > 0)
        {
            display.Clear();
        }
        
        display.style.backgroundImage = new StyleBackground(texture);
        display.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
    }
    
    void UpdateFPS()
    {
        frameCount++;
        float currentTime = Time.time;
        
        if (currentTime - lastFrameTime >= 0.5f)
        {
            fps = frameCount / (currentTime - lastFrameTime);
            frameCount = 0;
            lastFrameTime = currentTime;
            
            if (sensorFps != null)
            {
                sensorFps.text = $"{fps:F0} FPS";
                
                if (fps >= 25)
                    sensorFps.style.color = new Color(0.39f, 0.71f, 1f);
                else if (fps >= 15)
                    sensorFps.style.color = new Color(1f, 0.78f, 0.39f);
                else
                    sensorFps.style.color = new Color(1f, 0.39f, 0.39f);
            }
        }
    }
    
    #region Tab Switching
    
    void SwitchTab(bool showSensors)
    {
        if (showSensors)
        {
            sensorsTab.style.display = DisplayStyle.Flex;
            flightTab.style.display = DisplayStyle.None;
            
            SetButtonStyle(sensorsTabBtn, new Color(0.39f, 0.71f, 1f, 0.2f), new Color(0.39f, 0.71f, 1f, 1f));
            SetButtonStyle(flightTabBtn, new Color(0.2f, 0.22f, 0.27f, 0.6f), new Color(1f, 1f, 1f, 0.15f));
        }
        else
        {
            sensorsTab.style.display = DisplayStyle.None;
            flightTab.style.display = DisplayStyle.Flex;
            
            SetButtonStyle(flightTabBtn, new Color(0.39f, 0.71f, 1f, 0.2f), new Color(0.39f, 0.71f, 1f, 1f));
            SetButtonStyle(sensorsTabBtn, new Color(0.2f, 0.22f, 0.27f, 0.6f), new Color(1f, 1f, 1f, 0.15f));
        }
    }
    
    void SetButtonStyle(Button btn, Color bgColor, Color borderColor)
    {
        if (btn == null) return;
        
        btn.style.backgroundColor = bgColor;
        btn.style.borderTopColor = borderColor;
        btn.style.borderBottomColor = borderColor;
        btn.style.borderLeftColor = borderColor;
        btn.style.borderRightColor = borderColor;
        btn.style.color = borderColor;
    }
    
    #endregion
    
    #region Connection Toggle
    
    void ToggleDepthConnection()
    {
        depthConnected = !depthConnected;
        if (!depthConnected)
        {
            depthFeedDisplay?.Clear();
            depthFeedDisplay.style.backgroundImage = null;
            depthQueue = new ConcurrentQueue<ImageData>();
            RestorePlaceholder(depthFeedDisplay, "DEPTH CAMERA", "640 × 480");
        }
        UpdateConnectionUI();
    }
    
    void ToggleIRConnection()
    {
        irConnected = !irConnected;
        if (!irConnected)
        {
            irFeedDisplay?.Clear();
            irFeedDisplay.style.backgroundImage = null;
            irQueue = new ConcurrentQueue<ImageData>();
            RestorePlaceholder(irFeedDisplay, "INFRARED", "640 × 480");
        }
        UpdateConnectionUI();
    }
    
    void ToggleColorConnection()
    {
        colorConnected = !colorConnected;
        if (!colorConnected)
        {
            colorFeedDisplay?.Clear();
            colorFeedDisplay.style.backgroundImage = null;
            colorQueue = new ConcurrentQueue<ImageData>();
            RestorePlaceholder(colorFeedDisplay, "RGB CAMERA", "1920 × 1080");
        }
        UpdateConnectionUI();
    }
    
    void ToggleLidarConnection()
    {
        lidarConnected = !lidarConnected;
        UpdateConnectionUI();
    }
    
    void ConnectAll()
    {
        depthConnected = true;
        irConnected = true;
        colorConnected = true;
        lidarConnected = true;
        UpdateConnectionUI();
    }
    
    void DisconnectAll()
    {
        depthConnected = false;
        irConnected = false;
        colorConnected = false;
        lidarConnected = false;
        
        depthFeedDisplay?.Clear();
        irFeedDisplay?.Clear();
        colorFeedDisplay?.Clear();
        
        depthFeedDisplay.style.backgroundImage = null;
        irFeedDisplay.style.backgroundImage = null;
        colorFeedDisplay.style.backgroundImage = null;
        
        RestorePlaceholder(depthFeedDisplay, "DEPTH CAMERA", "640 × 480");
        RestorePlaceholder(irFeedDisplay, "INFRARED", "640 × 480");
        RestorePlaceholder(colorFeedDisplay, "RGB CAMERA", "1920 × 1080");
        
        depthQueue = new ConcurrentQueue<ImageData>();
        irQueue = new ConcurrentQueue<ImageData>();
        colorQueue = new ConcurrentQueue<ImageData>();
        
        UpdateConnectionUI();
    }
    
    void RestorePlaceholder(VisualElement display, string mainText, string subText)
    {
        if (display == null) return;
        
        var mainLabel = new Label(mainText);
        mainLabel.style.color = new Color(0.39f, 0.71f, 1f, 0.4f);
        mainLabel.style.fontSize = 14;
        mainLabel.style.letterSpacing = 2;
        mainLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        
        var subLabel = new Label(subText);
        subLabel.style.color = new Color(0.39f, 0.71f, 1f, 0.3f);
        subLabel.style.fontSize = 10;
        subLabel.style.marginTop = 8;
        
        display.Add(mainLabel);
        display.Add(subLabel);
    }
    
    #endregion
    
    #region UI Updates
    
    void UpdateConnectionUI()
    {
        UpdateStatusLabel(depthStatus, depthConnected);
        UpdateStatusLabel(irStatus, irConnected);
        UpdateStatusLabel(colorStatus, colorConnected);
        UpdateStatusLabel(lidarStatus, lidarConnected);
        
        UpdateButtonConnection(depthConnectBtn, depthConnected);
        UpdateButtonConnection(irConnectBtn, irConnected);
        UpdateButtonConnection(colorConnectBtn, colorConnected);
        UpdateButtonConnection(lidarConnectBtn, lidarConnected);
        
        int connectedCount = (depthConnected ? 1 : 0) + (irConnected ? 1 : 0) + 
                           (colorConnected ? 1 : 0) + (lidarConnected ? 1 : 0);
        
        if (sensorsOnlineCount != null)
        {
            sensorsOnlineCount.text = $"{connectedCount}/4";
            
            if (connectedCount == 4)
                sensorsOnlineCount.style.color = new Color(0f, 1f, 0.5f);
            else if (connectedCount > 0)
                sensorsOnlineCount.style.color = new Color(1f, 0.78f, 0.39f);
            else
                sensorsOnlineCount.style.color = new Color(1f, 0.39f, 0.39f);
        }
        
        if (statusIndicator != null)
        {
            statusIndicator.style.color = connectedCount > 0 ? 
                new Color(0f, 1f, 0.5f) : new Color(1f, 0.39f, 0.39f);
        }
    }
    
    void UpdateStatusLabel(Label label, bool connected)
    {
        if (label != null)
        {
            label.text = connected ? "● CONNECTED" : "● DISCONNECTED";
            label.style.color = connected ? 
                new Color(0f, 1f, 0.5f) : 
                new Color(1f, 0.39f, 0.39f);
        }
    }
    
    void UpdateButtonConnection(Button button, bool connected)
    {
        if (button == null) return;
        
        button.text = connected ? "DISCONNECT" : "CONNECT";
        
        if (connected)
        {
            button.style.backgroundColor = new Color(1f, 0.39f, 0.39f, 0.15f);
            button.style.borderTopColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderBottomColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderLeftColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderRightColor = new Color(1f, 0.39f, 0.39f);
            button.style.color = new Color(1f, 0.39f, 0.39f);
        }
        else
        {
            button.style.backgroundColor = new Color(0f, 1f, 0.5f, 0.15f);
            button.style.borderTopColor = new Color(0f, 1f, 0.5f);
            button.style.borderBottomColor = new Color(0f, 1f, 0.5f);
            button.style.borderLeftColor = new Color(0f, 1f, 0.5f);
            button.style.borderRightColor = new Color(0f, 1f, 0.5f);
            button.style.color = new Color(0f, 1f, 0.5f);
        }
    }
    
    #endregion
    
    void OnDestroy()
    {
        if (depthTexture != null) Destroy(depthTexture);
        if (irTexture != null) Destroy(irTexture);
        if (colorTexture != null) Destroy(colorTexture);
    }
}