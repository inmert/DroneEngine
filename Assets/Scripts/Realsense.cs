using UnityEngine;
using UnityEngine.UIElements;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class CameraFeedController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;
    
    [Header("ROS Topics")]
    [SerializeField] private string depthTopicName = "/camera/depth/image_rect_raw";
    [SerializeField] private string irTopicName = "/camera/infra1/image_rect_raw";
    [SerializeField] private string colorTopicName = "/camera/color/image_raw";
    
    [Header("Camera Settings")]
    [SerializeField] private int targetWidth = 640;
    [SerializeField] private int targetHeight = 480;
    
    // UI Elements
    private VisualElement depthFeedDisplay;
    private VisualElement irFeedDisplay;
    private VisualElement colorFeedDisplay;
    private Label sensorFpsLabel;
    
    // Textures for camera feeds
    private Texture2D depthTexture;
    private Texture2D irTexture;
    private Texture2D colorTexture;
    
    // ROS Connection
    private ROSConnection ros;
    
    // FPS Tracking
    private float lastUpdateTime;
    private int frameCount;
    private float currentFPS;
    
    void Start()
    {
        // Get ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        
        // Subscribe to ROS topics
        ros.Subscribe<ImageMsg>(depthTopicName, DepthImageCallback);
        ros.Subscribe<ImageMsg>(irTopicName, IRImageCallback);
        ros.Subscribe<ImageMsg>(colorTopicName, ColorImageCallback);
        
        // Initialize UI
        InitializeUI();
        
        // Initialize textures
        InitializeTextures();
        
        Debug.Log("Camera Feed Controller initialized. Subscribed to ROS topics.");
    }
    
    void InitializeUI()
    {
        var root = uiDocument.rootVisualElement;
        
        // Get feed display elements
        depthFeedDisplay = root.Q<VisualElement>("depth-feed-display");
        irFeedDisplay = root.Q<VisualElement>("ir-feed-display");
        colorFeedDisplay = root.Q<VisualElement>("color-feed-display");
        sensorFpsLabel = root.Q<Label>("sensor-fps");
        
        if (depthFeedDisplay == null || irFeedDisplay == null || colorFeedDisplay == null)
        {
            Debug.LogError("Could not find camera feed display elements in UI!");
        }
    }
    
    void InitializeTextures()
    {
        depthTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        irTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        colorTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
        
        depthTexture.filterMode = FilterMode.Point;
        irTexture.filterMode = FilterMode.Point;
        colorTexture.filterMode = FilterMode.Point;
    }
    
    void DepthImageCallback(ImageMsg imageMsg)
    {
        ProcessImageMessage(imageMsg, depthTexture, depthFeedDisplay, true);
        UpdateFPS();
    }
    
    void IRImageCallback(ImageMsg imageMsg)
    {
        ProcessImageMessage(imageMsg, irTexture, irFeedDisplay, false);
    }
    
    void ColorImageCallback(ImageMsg imageMsg)
    {
        ProcessImageMessage(imageMsg, colorTexture, colorFeedDisplay, false);
    }
    
    void ProcessImageMessage(ImageMsg imageMsg, Texture2D texture, VisualElement displayElement, bool isDepth)
    {
        if (imageMsg == null || imageMsg.data == null || imageMsg.data.Length == 0)
        {
            Debug.LogWarning("Received empty image message");
            return;
        }
        
        // Resize texture if dimensions don't match
        if (texture.width != (int)imageMsg.width || texture.height != (int)imageMsg.height)
        {
            texture.Reinitialize((int)imageMsg.width, (int)imageMsg.height);
        }
        
        // Convert image data based on encoding
        Color32[] pixels = ConvertImageData(imageMsg, isDepth);
        
        if (pixels != null)
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            
            // Update UI element with texture
            UpdateDisplayElement(displayElement, texture);
        }
    }
    
    Color32[] ConvertImageData(ImageMsg imageMsg, bool isDepth)
    {
        int width = (int)imageMsg.width;
        int height = (int)imageMsg.height;
        Color32[] pixels = new Color32[width * height];
        
        string encoding = imageMsg.encoding;
        
        try
        {
            if (encoding == "rgb8")
            {
                // RGB8 encoding
                for (int i = 0; i < width * height; i++)
                {
                    int dataIndex = i * 3;
                    // Flip vertically for Unity (ROS images are top-down, Unity is bottom-up)
                    int row = i / width;
                    int col = i % width;
                    int flippedIndex = (height - 1 - row) * width + col;
                    
                    pixels[flippedIndex] = new Color32(
                        imageMsg.data[dataIndex],
                        imageMsg.data[dataIndex + 1],
                        imageMsg.data[dataIndex + 2],
                        255
                    );
                }
            }
            else if (encoding == "bgr8")
            {
                // BGR8 encoding (common for OpenCV)
                for (int i = 0; i < width * height; i++)
                {
                    int dataIndex = i * 3;
                    int row = i / width;
                    int col = i % width;
                    int flippedIndex = (height - 1 - row) * width + col;
                    
                    pixels[flippedIndex] = new Color32(
                        imageMsg.data[dataIndex + 2], // R
                        imageMsg.data[dataIndex + 1], // G
                        imageMsg.data[dataIndex],     // B
                        255
                    );
                }
            }
            else if (encoding == "mono8")
            {
                // Grayscale encoding (IR cameras)
                for (int i = 0; i < width * height; i++)
                {
                    int row = i / width;
                    int col = i % width;
                    int flippedIndex = (height - 1 - row) * width + col;
                    
                    byte value = imageMsg.data[i];
                    pixels[flippedIndex] = new Color32(value, value, value, 255);
                }
            }
            else if (encoding == "16UC1" || encoding == "mono16")
            {
                // 16-bit depth data
                for (int i = 0; i < width * height; i++)
                {
                    int dataIndex = i * 2;
                    int row = i / width;
                    int col = i % width;
                    int flippedIndex = (height - 1 - row) * width + col;
                    
                    // Convert 16-bit depth to 8-bit grayscale for visualization
                    ushort depthValue = (ushort)(imageMsg.data[dataIndex] | (imageMsg.data[dataIndex + 1] << 8));
                    
                    // Normalize depth value (adjust these values based on your camera's range)
                    float normalizedDepth = Mathf.Clamp01(depthValue / 5000.0f);
                    
                    // Apply colormap for depth visualization
                    if (isDepth)
                    {
                        Color depthColor = GetDepthColor(normalizedDepth);
                        pixels[flippedIndex] = depthColor;
                    }
                    else
                    {
                        byte value = (byte)(normalizedDepth * 255);
                        pixels[flippedIndex] = new Color32(value, value, value, 255);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Unsupported image encoding: {encoding}");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error converting image data: {e.Message}");
            return null;
        }
        
        return pixels;
    }
    
    Color32 GetDepthColor(float normalizedDepth)
    {
        // Apply a color gradient for depth visualization (blue = close, red = far)
        float hue = (1.0f - normalizedDepth) * 0.7f; // 0.7 = blue to red range
        Color color = Color.HSVToRGB(hue, 1.0f, 1.0f);
        return color;
    }
    
    void UpdateDisplayElement(VisualElement element, Texture2D texture)
    {
        if (element == null || texture == null) return;
        
        // Remove placeholder labels
        element.Clear();
        
        // Set texture as background
        element.style.backgroundImage = new StyleBackground(texture);
    }
    
    void UpdateFPS()
    {
        frameCount++;
        float currentTime = Time.time;
        
        if (currentTime - lastUpdateTime >= 1.0f)
        {
            currentFPS = frameCount / (currentTime - lastUpdateTime);
            frameCount = 0;
            lastUpdateTime = currentTime;
            
            if (sensorFpsLabel != null)
            {
                sensorFpsLabel.text = $"{Mathf.RoundToInt(currentFPS)} FPS";
            }
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from ROS topics
        if (ros != null)
        {
            ros.Unsubscribe(depthTopicName);
            ros.Unsubscribe(irTopicName);
            ros.Unsubscribe(colorTopicName);
        }
        
        // Destroy textures
        if (depthTexture != null) Destroy(depthTexture);
        if (irTexture != null) Destroy(irTexture);
        if (colorTexture != null) Destroy(colorTexture);
    }
}