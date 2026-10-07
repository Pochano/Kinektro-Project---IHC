using UnityEngine;

public interface IJointDataSource
{
    bool TryGetJointPosition(string jointName, out Vector3 pos);
    string GetHandState(string handJointName); // "Open", "Closed", "Lasso", "Unknown", "NotTracked"
}
