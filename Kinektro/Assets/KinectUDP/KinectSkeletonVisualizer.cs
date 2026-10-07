using System.Collections.Generic;
using UnityEngine;

public class KinectSkeletonVisualizer : MonoBehaviour
{
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    public float jointSize = 0.08f;
    public Color jointColor = Color.cyan;
    public Color boneColor = Color.yellow;

    private readonly Dictionary<string, Transform> jointSpheres = new Dictionary<string, Transform>();

    private readonly List<(string, string)> bones = new List<(string, string)>
    {
        ("SpineBase", "SpineMid"),
        ("SpineMid", "SpineShoulder"),
        ("SpineShoulder", "Neck"),
        ("Neck", "Head"),

        ("SpineShoulder", "ShoulderLeft"),
        ("ShoulderLeft", "ElbowLeft"),
        ("ElbowLeft", "WristLeft"),
        ("WristLeft", "HandLeft"),
        ("HandLeft", "HandTipLeft"),
        ("WristLeft", "ThumbLeft"),

        ("SpineShoulder", "ShoulderRight"),
        ("ShoulderRight", "ElbowRight"),
        ("ElbowRight", "WristRight"),
        ("WristRight", "HandRight"),
        ("HandRight", "HandTipRight"),
        ("WristRight", "ThumbRight"),

        ("SpineBase", "HipLeft"),
        ("HipLeft", "KneeLeft"),
        ("KneeLeft", "AnkleLeft"),
        ("AnkleLeft", "FootLeft"),

        ("SpineBase", "HipRight"),
        ("HipRight", "KneeRight"),
        ("KneeRight", "AnkleRight"),
        ("AnkleRight", "FootRight"),
    };

    private readonly Dictionary<(string, string), LineRenderer> boneLines = new Dictionary<(string, string), LineRenderer>();

    void Start()
    {
        if (sourceProvider != null)
        {
            dataSource = sourceProvider.GetActiveSource();
        }

        if (dataSource == null)
        {
            Debug.LogError("KinectSkeletonVisualizer: no se pudo obtener una fuente de datos valida desde el SourceProvider.");
        }

        InitializeVisuals();
    }

    void InitializeVisuals()
    {
        foreach (string jointName in GetAllJointNames())
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Joint_" + jointName;
            sphere.transform.SetParent(transform);
            sphere.transform.localScale = Vector3.one * jointSize;
            sphere.GetComponent<Renderer>().material.color = jointColor;
            Destroy(sphere.GetComponent<Collider>());
            sphere.SetActive(false);
            jointSpheres[jointName] = sphere.transform;
        }

        foreach (var bone in bones)
        {
            GameObject lineObj = new GameObject("Bone_" + bone.Item1 + "_" + bone.Item2);
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = boneColor;
            lr.endColor = boneColor;
            lr.enabled = false;
            boneLines[bone] = lr;
        }
    }

    void Update()
    {
        if (dataSource == null) return;

        foreach (var kvp in jointSpheres)
        {
            if (dataSource.TryGetJointPosition(kvp.Key, out Vector3 pos))
            {
                kvp.Value.position = pos;
                kvp.Value.gameObject.SetActive(true);
            }
        }

        foreach (var bone in bones)
        {
            if (dataSource.TryGetJointPosition(bone.Item1, out Vector3 posA) &&
                dataSource.TryGetJointPosition(bone.Item2, out Vector3 posB))
            {
                LineRenderer lr = boneLines[bone];
                lr.SetPosition(0, posA);
                lr.SetPosition(1, posB);
                lr.enabled = true;
            }
        }
    }

    private IEnumerable<string> GetAllJointNames()
    {
        yield return "SpineBase";
        yield return "SpineMid";
        yield return "Neck";
        yield return "Head";
        yield return "ShoulderLeft";
        yield return "ElbowLeft";
        yield return "WristLeft";
        yield return "HandLeft";
        yield return "ShoulderRight";
        yield return "ElbowRight";
        yield return "WristRight";
        yield return "HandRight";
        yield return "HipLeft";
        yield return "KneeLeft";
        yield return "AnkleLeft";
        yield return "FootLeft";
        yield return "HipRight";
        yield return "KneeRight";
        yield return "AnkleRight";
        yield return "FootRight";
        yield return "SpineShoulder";
        yield return "HandTipLeft";
        yield return "ThumbLeft";
        yield return "HandTipRight";
        yield return "ThumbRight";
    }
}