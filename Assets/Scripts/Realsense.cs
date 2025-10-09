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
    [SerializeField] private bool useCompressedTopics = false; // Set to true if using compressed
    
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
    private bool lidarConnected = false; // LiDAR not implemented yet
    
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
    private ConcurrentQueue<byte[]> depthQueue = new ConcurrentQueue<byte[]>();
    private ConcurrentQueue<byte[]> irQueue = new ConcurrentQueue<byte[]>();
    private ConcurrentQueue<byte[]> colorQueue = new ConcurrentQueue<byte[]>();
    
    private ROSConnection ros;
    
    void Start()
    {
        // Get ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        
        // Initialize UI
        InitializeUI();
        
        // Subscribe to ROS topics
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
        Debug.Log($"UI Root found: {root != null}");
        
        // Get Tab Elements
        sensorsTabBtn = root.Q<Button>("sensors-tab-btn");
        flightTabBtn = root.Q<Button>("flight-tab-btn");
        sensorsTab = root.Q<VisualElement>("sensors-tab");
        flightTab = root.Q<VisualElement>("flight-tab");
        
        Debug.Log($"Tabs found - Sensors: {sensorsTabBtn != null}, Flight: {flightTabBtn != null}");
        Debug.Log($"Tab panels - Sensors: {sensorsTab != null}, Flight: {flightTab != null}");
        
        // Get Feed Displays
        depthFeedDisplay = root.Q<VisualElement>("depth-feed-display");
        irFeedDisplay = root.Q<VisualElement>("ir-feed-display");
        colorFeedDisplay = root.Q<VisualElement>("color-feed-display");
        lidarFeedDisplay = root.Q<VisualElement>("lidar-feed-display");
        
        Debug.Log($"Displays found - Depth: {depthFeedDisplay != null}, IR: {irFeedDisplay != null}, Color: {colorFeedDisplay != null}, Lidar: {lidarFeedDisplay != null}");
        
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
        
        // Initialize textures with bilinear filtering
        depthTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        depthTexture.filterMode = FilterMode.Bilinear;
        
        irTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        irTexture.filterMode = FilterMode.Bilinear;
        
        colorTexture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        colorTexture.filterMode = FilterMode.Bilinear;
        
        // Update initial UI state
        UpdateConnectionUI();
    }
    
    void SubscribeToTopics()
    {
        ros.Subscribe<CompressedImageMsg>(depthTopic, OnDepthImageReceived);
        ros.Subscribe<CompressedImageMsg>(irTopic, OnIRImageReceived);
        ros.Subscribe<CompressedImageMsg>(colorTopic, OnColorImageReceived);
        
        Debug.Log($"Subscribed to topics:\n- {depthTopic}\n- {irTopic}\n- {colorTopic}");
    }
    
    #region ROS Callbacks
    
    void OnDepthImageReceived(CompressedImageMsg msg)
    {
        Debug.Log($"Depth image received - Size: {msg.data.Length} bytes, Connected: {depthConnected}");
        
        if (!depthConnected) return;
        
        if (skipFrames)
        {
            depthFrameCounter++;
            if (depthFrameCounter % frameSkipCount != 0) return;
        }
        
        if (depthQueue.Count < 2)
        {
            depthQueue.Enqueue(msg.data);
            Debug.Log($"Depth image queued - Queue size: {depthQueue.Count}");
        }
        else
        {
            Debug.LogWarning("Depth queue full, skipping frame");
        }
    }
    
    void OnIRImageReceived(CompressedImageMsg msg)
    {
        Debug.Log($"IR image received - Size: {msg.data.Length} bytes, Connected: {irConnected}");
        
        if (!irConnected) return;
        
        if (skipFrames)
        {
            irFrameCounter++;
            if (irFrameCounter % frameSkipCount != 0) return;
        }
        
        if (irQueue.Count < 2)
        {
            irQueue.Enqueue(msg.data);
        }
    }
    
    void OnColorImageReceived(CompressedImageMsg msg)
    {
        Debug.Log($"Color image received - Size: {msg.data.Length} bytes, Connected: {colorConnected}");
        
        if (!colorConnected) return;
        
        if (skipFrames)
        {
            colorFrameCounter++;
            if (colorFrameCounter % frameSkipCount != 0) return;
        }
        
        if (colorQueue.Count < 2)
        {
            colorQueue.Enqueue(msg.data);
        }
    }
    
    #endregion
    
    void Update()
    {
        // Process queued images on main thread
        ProcessImageQueue(depthQueue, depthTexture, depthFeedDisplay);
        ProcessImageQueue(irQueue, irTexture, irFeedDisplay);
        ProcessImageQueue(colorQueue, colorTexture, colorFeedDisplay);
        
        UpdateFPS();
    }
    
    void ProcessImageQueue(ConcurrentQueue<byte[]> queue, Texture2D texture, VisualElement display)
    {
        if (queue.TryDequeue(out byte[] imageData))
        {
            Debug.Log($"Processing image - Data size: {imageData.Length}, Display null: {display == null}, Texture null: {texture == null}");
            
            try
            {
                bool loaded = texture.LoadImage(imageData);
                Debug.Log($"Texture LoadImage result: {loaded}");
                
                if (loaded)
                {
                    UpdateFeedDisplay(display, texture);
                    Debug.Log("Feed display updated successfully");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing image: {e.Message}\n{e.StackTrace}");
            }
        }
    }
    
    void UpdateFeedDisplay(VisualElement display, Texture2D texture)
    {
        if (display == null)
        {
            Debug.LogError("Display element is null!");
            return;
        }
        
        if (texture == null)
        {
            Debug.LogError("Texture is null!");
            return;
        }
        
        Debug.Log($"Updating display - Texture size: {texture.width}x{texture.height}, Display children: {display.childCount}");
        
        // Clear placeholder text on first image
        if (display.childCount > 0)
        {
            display.Clear();
            Debug.Log("Cleared placeholder text");
        }
        
        display.style.backgroundImage = new StyleBackground(texture);
        display.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        
        Debug.Log("Background image set successfully");
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
                
                // Color code FPS
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
            depthQueue = new ConcurrentQueue<byte[]>();
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
            irQueue = new ConcurrentQueue<byte[]>();
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
            colorQueue = new ConcurrentQueue<byte[]>();
            RestorePlaceholder(colorFeedDisplay, "RGB CAMERA", "1920 × 1080");
        }
        UpdateConnectionUI();
    }
    
    void ToggleLidarConnection()
    {
        lidarConnected = !lidarConnected;
        UpdateConnectionUI();
        Debug.Log("LiDAR toggle - not yet implemented");
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
        
        // Clear displays
        depthFeedDisplay?.Clear();
        irFeedDisplay?.Clear();
        colorFeedDisplay?.Clear();
        
        depthFeedDisplay.style.backgroundImage = null;
        irFeedDisplay.style.backgroundImage = null;
        colorFeedDisplay.style.backgroundImage = null;
        
        // Restore placeholders
        RestorePlaceholder(depthFeedDisplay, "DEPTH CAMERA", "640 × 480");
        RestorePlaceholder(irFeedDisplay, "INFRARED", "640 × 480");
        RestorePlaceholder(colorFeedDisplay, "RGB CAMERA", "1920 × 1080");
        
        // Clear queues
        depthQueue = new ConcurrentQueue<byte[]>();
        irQueue = new ConcurrentQueue<byte[]>();
        colorQueue = new ConcurrentQueue<byte[]>();
        
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
        // Update status indicators
        UpdateStatusLabel(depthStatus, depthConnected);
        UpdateStatusLabel(irStatus, irConnected);
        UpdateStatusLabel(colorStatus, colorConnected);
        UpdateStatusLabel(lidarStatus, lidarConnected);
        
        // Update button text and styles
        UpdateButtonConnection(depthConnectBtn, depthConnected);
        UpdateButtonConnection(irConnectBtn, irConnected);
        UpdateButtonConnection(colorConnectBtn, colorConnected);
        UpdateButtonConnection(lidarConnectBtn, lidarConnected);
        
        // Update sensor count
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
        
        // Update main status indicator
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
            // Disconnect style (red)
            button.style.backgroundColor = new Color(1f, 0.39f, 0.39f, 0.15f);
            button.style.borderTopColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderBottomColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderLeftColor = new Color(1f, 0.39f, 0.39f);
            button.style.borderRightColor = new Color(1f, 0.39f, 0.39f);
            button.style.color = new Color(1f, 0.39f, 0.39f);
        }
        else
        {
            // Connect style (green)
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
        // Cleanup textures
        if (depthTexture != null) Destroy(depthTexture);
        if (irTexture != null) Destroy(irTexture);
        if (colorTexture != null) Destroy(colorTexture);
    }
}