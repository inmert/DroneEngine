using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class ChatterSubscriber : MonoBehaviour
{
    ROSConnection ros;

    void Start()
    {
        // Get the ROSConnection instance
        ros = ROSConnection.GetOrCreateInstance();
        // Subscribe to the "chatter" topic
        ros.Subscribe<StringMsg>("/chatter", ChatterCallback);
    }

    void ChatterCallback(StringMsg msg)
    {
        // Print received message to Unity console
        Debug.Log("Received from ROS: " + msg.data);
    }
}
