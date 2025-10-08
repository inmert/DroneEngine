using UnityEngine;
using UnityEngine.UIElements;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System;
using System.Collections.Concurrent;

/// <summary>
/// OPTIMIZED RealSense D455 camera feed receiver
/// Uses threading and texture pooling for better performance
/// </summary>
public class RealSenseFeedReceiver : MonoBehaviour
{
    [Header("ROS Topics")]
    [SerializeField] private string depthTopic = "/realsense/depth/compressed";
    [SerializeField] private string irTopic = "/realsense/infrared/compressed";
    [SerializeField] private string colorTopic = "/realsense/color/compressed";
    
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;
    
    [Header("Performance Settings")]
    [SerializeField] private bool skipFrames = false; // Enable if still laggy
    [SerializeField] private int frameSkipCount = 1; // Process every Nth frame
    
    // UI Elements
    private VisualElement depthFeedDisplay;
    private VisualElement irFeedDisplay;
    private VisualElement colorFeedDisplay;
    private Label depthStatus;
    private Label irStatus;
    private Label colorStatus;
    private Label sensorsOnlineCount;
    private Label sensorFps;
    
    private Button depthConnectBtn;
    private Button irConnectBtn;
    private Button colorConnectBtn;
    
    // Textures for displaying camera feeds
    private Texture2D depthTexture;
    private Texture2D irTexture;
    private Texture2D colorTexture;
    
    // Connection states
    private bool depthConnected = false;
    private bool irConnected = false;
    private bool colorConnected = false;
    
    // FPS calculation
    private float lastFrameTime;
    private int frameCount;
    private float fps = 0f;
    
    // Frame skipping
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
        
        Debug.Log("RealSense Feed Receiver initialized - OPTIMIZED MODE");
    }
    
    void InitializeUI()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
        
        var root = uiDocument.rootVisualElement;
        
        // Get display elements
        depthFeedDisplay = root.Q<VisualElement>("depth-feed-display");
        irFeedDisplay = root.Q<VisualElement>("ir-feed-display");
        colorFeedDisplay = root.Q<VisualElement>("color-feed-display");
        
        // Get status labels
        depthStatus = root.Q<Label>("depth-status");
        irStatus = root.Q<Label>("ir-status");
        colorStatus = root.Q<Label>("color-status");
        sensorsOnlineCount = root.Q<Label>("sensors-online-count");
        sensorFps = root.Q<Label>("sensor-fps");
        
        // Get buttons
        depthConnectBtn = root.Q<Button>("depth-connect-btn");
        irConnectBtn = root.Q<Button>("ir-connect-btn");
        colorConnectBtn = root.Q<Button>("color-connect-btn");
        
        // Connect all/disconnect all buttons
        var connectAllBtn = root.Q<Button>("connect-all-btn");
        var disconnectAllBtn = root.Q<Button>("disconnect-all-btn");
        
        // Setup button callbacks
        depthConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleDepthConnection());
        irConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleIRConnection());
        colorConnectBtn?.RegisterCallback<ClickEvent>(evt => ToggleColorConnection());
        connectAllBtn?.RegisterCallback<ClickEvent>(evt => ConnectAll());
        disconnectAllBtn?.RegisterCallback<ClickEvent>(evt => DisconnectAll());
        
        // Initialize textures with filtering for better performance
        depthTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        depthTexture.filterMode = FilterMode.Bilinear;
        
        irTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        irTexture.filterMode = FilterMode.Bilinear;
        
        colorTexture = new Texture2D(640, 480, TextureFormat.RGB24, false);
        colorTexture.filterMode = FilterMode.Bilinear;
        
        // Set initial connection state to true
        depthConnected = true;
        irConnected = true;
        colorConnected = true;
        UpdateConnectionUI();
        
        lastFrameTime = Time.time;
    }
    
    void SubscribeToTopics()
    {
        // Subscribe to compressed image topics
        ros.Subscribe<CompressedImageMsg>(depthTopic, OnDepthImageReceived);
        ros.Subscribe<CompressedImageMsg>(irTopic, OnIRImageReceived);
        ros.Subscribe<CompressedImageMsg>(colorTopic, OnColorImageReceived);
    }
    
    void OnDepthImageReceived(CompressedImageMsg msg)
    {
        if (!depthConnected) return;
        
        // Frame skipping if enabled
        if (skipFrames)
        {
            depthFrameCounter++;
            if (depthFrameCounter % frameSkipCount != 0) return;
        }
        
        // Queue the data for processing in Update()
        if (depthQueue.Count < 2) // Limit queue size
        {
            depthQueue.Enqueue(msg.data);
        }
    }
    
    void OnIRImageReceived(CompressedImageMsg msg)
    {
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
            try
            {
                // Load image data into texture
                texture.LoadImage(imageData);
                
                // Only apply if display is visible
                if (display != null && display.style.backgroundImage.value == null || 
                    display.style.backgroundImage.value.texture != texture)
                {
                    UpdateFeedDisplay(display, texture);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing image: {e.Message}");
            }
        }
    }
    
    void UpdateFeedDisplay(VisualElement display, Texture2D texture)
    {
        if (display != null && texture != null)
        {
            // Clear placeholder text only once
            if (display.childCount > 0)
            {
                display.Clear();
            }
            
            // Set background image
            display.style.backgroundImage = new StyleBackground(texture);
            display.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }
    }
    
    void UpdateFPS()
    {
        frameCount++;
        float currentTime = Time.time;
        
        if (currentTime - lastFrameTime >= 0.5f) // Update twice per second
        {
            fps = frameCount / (currentTime - lastFrameTime);
            frameCount = 0;
            lastFrameTime = currentTime;
            
            if (sensorFps != null)
            {
                sensorFps.text = $"{fps:F0} FPS";
            }
        }
    }
    
    void ToggleDepthConnection()
    {
        depthConnected = !depthConnected;
        if (!depthConnected)
        {
            depthFeedDisplay?.Clear();
            depthFeedDisplay.style.backgroundImage = null;
            depthQueue = new ConcurrentQueue<byte[]>(); // Clear queue
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
        }
        UpdateConnectionUI();
    }
    
    void ConnectAll()
    {
        depthConnected = true;
        irConnected = true;
        colorConnected = true;
        UpdateConnectionUI();
    }
    
    void DisconnectAll()
    {
        depthConnected = false;
        irConnected = false;
        colorConnected = false;
        
        depthFeedDisplay?.Clear();
        irFeedDisplay?.Clear();
        colorFeedDisplay?.Clear();
        
        depthFeedDisplay.style.backgroundImage = null;
        irFeedDisplay.style.backgroundImage = null;
        colorFeedDisplay.style.backgroundImage = null;
        
        // Clear all queues
        depthQueue = new ConcurrentQueue<byte[]>();
        irQueue = new ConcurrentQueue<byte[]>();
        colorQueue = new ConcurrentQueue<byte[]>();
        
        UpdateConnectionUI();
    }
    
    void UpdateConnectionUI()
    {
        // Update status indicators
        UpdateStatusLabel(depthStatus, depthConnected);
        UpdateStatusLabel(irStatus, irConnected);
        UpdateStatusLabel(colorStatus, colorConnected);
        
        // Update button text
        if (depthConnectBtn != null)
            depthConnectBtn.text = depthConnected ? "DISCONNECT" : "CONNECT";
        if (irConnectBtn != null)
            irConnectBtn.text = irConnected ? "DISCONNECT" : "CONNECT";
        if (colorConnectBtn != null)
            colorConnectBtn.text = colorConnected ? "DISCONNECT" : "CONNECT";
        
        // Update button colors
        UpdateButtonStyle(depthConnectBtn, depthConnected);
        UpdateButtonStyle(irConnectBtn, irConnected);
        UpdateButtonStyle(colorConnectBtn, colorConnected);
        
        // Update sensor count
        int connectedCount = (depthConnected ? 1 : 0) + (irConnected ? 1 : 0) + (colorConnected ? 1 : 0);
        if (sensorsOnlineCount != null)
        {
            sensorsOnlineCount.text = $"{connectedCount}/3";
        }
    }
    
    void UpdateStatusLabel(Label label, bool connected)
    {
        if (label != null)
        {
            label.text = connected ? "● CONNECTED" : "● DISCONNECTED";
            label.style.color = connected ? 
                new Color(0, 1, 0.5f) : 
                new Color(1, 0.4f, 0.4f);
        }
    }
    
    void UpdateButtonStyle(Button button, bool connected)
    {
        if (button == null) return;
        
        if (connected)
        {
            // Disconnect style (red)
            button.style.backgroundColor = new Color(1f, 0.4f, 0.4f, 0.15f);
            button.style.borderTopColor = new Color(1f, 0.4f, 0.4f);
            button.style.borderBottomColor = new Color(1f, 0.4f, 0.4f);
            button.style.borderLeftColor = new Color(1f, 0.4f, 0.4f);
            button.style.borderRightColor = new Color(1f, 0.4f, 0.4f);
            button.style.color = new Color(1f, 0.4f, 0.4f);
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
    
    void OnDestroy()
    {
        // Cleanup textures
        if (depthTexture != null) Destroy(depthTexture);
        if (irTexture != null) Destroy(irTexture);
        if (colorTexture != null) Destroy(colorTexture);
    }
}