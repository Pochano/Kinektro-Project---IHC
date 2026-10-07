using UnityEngine;

public class KinectSourceProvider : MonoBehaviour
{
    [Header("Arrastra aqui ambas fuentes de datos")]
    public KinectUdpReceiver realSource;
    public MockKinectSource mockSource;

    [Header("Marca esta casilla para usar el mock; destildala para usar el Kinect real")]
    public bool useMock = false;

    public IJointDataSource GetActiveSource()
    {
        if (useMock)
        {
            return mockSource;
        }
        else
        {
            return realSource;
        }
    }
}