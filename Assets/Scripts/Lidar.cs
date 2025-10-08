using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System.Collections.Generic;

public class RPLIDARVisualizer : MonoBehaviour
{
    [Header("ROS Settings")]
    [Tooltip("The ROS topic for laser scan data")]
    public string scanTopic = "/scan";
    
    [Header("Visualization Settings")]
    [Tooltip("Material for the laser points")]
    public Material pointMaterial;
    
    [Tooltip("Size of each laser point")]
    public float pointSize = 0.05f;
    
    [Tooltip("Color of valid laser points")]
    public Color pointColor = Color.red;
    
    [Tooltip("Color of points at max range (invalid)")]
    public Color maxRangeColor = new Color(1f, 0f, 0f, 0.3f);
    
    [Tooltip("Draw lines connecting points")]
    public bool drawLines = true;
    
    [Tooltip("Line width")]
    public float lineWidth = 0.02f;
    
    [Tooltip("Color of connecting lines")]
    public Color lineColor = new Color(0f, 1f, 0f, 0.5f);
    
    [Header("Filtering")]
    [Tooltip("Minimum range to display (meters)")]
    public float minDisplayRange = 0.1f;
    
    [Tooltip("Maximum range to display (meters)")]
    public float maxDisplayRange = 12f;
    
    [Tooltip("Scale factor for visualization")]
    public float scaleFactor = 1f;

    private ROSConnection ros;
    private List<GameObject> pointObjects = new List<GameObject>();
    private LineRenderer lineRenderer;
    private LaserScanMsg lastScan;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<LaserScanMsg>(scanTopic, LaserScanCallback);
        
        // Create line renderer for connecting points
        if (drawLines)
        {
            GameObject lineObj = new GameObject("LaserScanLines");
            lineObj.transform.SetParent(transform);
            lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = pointMaterial != null ? pointMaterial : new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = false;
        }
        
        Debug.Log($"Subscribed to {scanTopic}");
    }

    void LaserScanCallback(LaserScanMsg scan)
    {
        lastScan = scan;
    }

    void Update()
    {
        if (lastScan != null)
        {
            VisualizeScan(lastScan);
        }
    }

    void VisualizeScan(LaserScanMsg scan)
    {
        int numRanges = scan.ranges.Length;
        
        // Clear old points if count changed
        if (pointObjects.Count != numRanges)
        {
            ClearPoints();
            CreatePoints(numRanges);
        }
        
        List<Vector3> validPoints = new List<Vector3>();
        
        for (int i = 0; i < numRanges; i++)
        {
            float range = scan.ranges[i];
            float angle = scan.angle_min + (i * scan.angle_increment);
            
            // Check if range is valid
            bool isValid = range >= scan.range_min && range <= scan.range_max 
                          && range >= minDisplayRange && range <= maxDisplayRange
                          && !float.IsInfinity(range) && !float.IsNaN(range);
            
            if (isValid)
            {
                // Convert polar to Cartesian (ROS uses right-hand rule, Z-up)
                // Unity uses left-hand rule, Y-up, so we convert:
                // ROS: x forward, y left, z up
                // Unity: z forward, x right, y up
                float x = range * Mathf.Cos(angle);
                float y = range * Mathf.Sin(angle);
                
                // Convert to Unity coordinates
                Vector3 point = new Vector3(-y, 0, x) * scaleFactor;
                
                pointObjects[i].transform.localPosition = point;
                pointObjects[i].SetActive(true);
                
                // Color based on range
                float rangeNormalized = (range - scan.range_min) / (scan.range_max - scan.range_min);
                Color color = Color.Lerp(pointColor, maxRangeColor, rangeNormalized);
                
                Renderer renderer = pointObjects[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = color;
                }
                
                validPoints.Add(point);
            }
            else
            {
                pointObjects[i].SetActive(false);
            }
        }
        
        // Update line renderer
        if (drawLines && lineRenderer != null && validPoints.Count > 1)
        {
            lineRenderer.positionCount = validPoints.Count + 1;
            lineRenderer.SetPositions(validPoints.ToArray());
            // Close the loop
            lineRenderer.SetPosition(validPoints.Count, validPoints[0]);
            lineRenderer.enabled = true;
        }
        else if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    void CreatePoints(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = $"LaserPoint_{i}";
            point.transform.SetParent(transform);
            point.transform.localScale = Vector3.one * pointSize;
            
            // Create or assign material
            Renderer renderer = point.GetComponent<Renderer>();
            if (pointMaterial != null)
            {
                renderer.material = pointMaterial;
            }
            else
            {
                renderer.material = new Material(Shader.Find("Standard"));
            }
            renderer.material.color = pointColor;
            
            // Remove collider to improve performance
            Collider collider = point.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            
            point.SetActive(false);
            pointObjects.Add(point);
        }
    }

    void ClearPoints()
    {
        foreach (GameObject point in pointObjects)
        {
            Destroy(point);
        }
        pointObjects.Clear();
    }

    void OnDestroy()
    {
        ClearPoints();
    }

    void OnDrawGizmos()
    {
        // Draw a circle representing max range
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        DrawCircle(transform.position, maxDisplayRange * scaleFactor, 64);
        
        // Draw origin
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }

    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(0, 0, radius);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(radius * Mathf.Sin(angle), 0, radius * Mathf.Cos(angle));
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}